using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Infrastructure.Data;
using PedagangPulsa.Infrastructure.Suppliers;
using PedagangPulsa.Infrastructure.Suppliers.DTOs;

namespace PedagangPulsa.Application.Services;

public class TransactionService
{
    private readonly AppDbContext _context;
    private readonly ISupplierAdapterFactory _adapterFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(
        AppDbContext context,
        ISupplierAdapterFactory adapterFactory,
        ILoggerFactory loggerFactory)
    {
        _context = context;
        _adapterFactory = adapterFactory;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<TransactionService>();
    }

    /// <summary>
    /// Create a new transaction (entry point)
    /// </summary>
    public async Task<(Transaction? Transaction, string ErrorMessage)> CreateTransactionAsync(
        Guid userId,
        Guid productId,
        string destination,
        decimal amount,
        string? notes = null)
    {
        // Get user with balance
        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return (null, "User not found");
        }

        if (user.Balance == null)
        {
            return (null, "User balance not found");
        }

        if (user.Balance.ActiveBalance < amount)
        {
            return (null, "Insufficient balance");
        }

        // Hold balance
        var holdResult = await HoldBalanceAsync(userId, amount, "PurchaseHold", Guid.NewGuid());
        if (!holdResult)
        {
            return (null, "Failed to hold balance");
        }

        // Create transaction
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            ReferenceId = Guid.NewGuid().ToString(),
            UserId = userId,
            ProductId = productId,
            Destination = destination,
            SellPrice = amount,
            Status = TransactionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Transaction {TransactionId} created for user {UserId}", transaction.Id, userId);

        return (transaction, string.Empty);
    }

    /// <summary>
    /// Hold balance (move from active to held)
    /// </summary>
    public async Task<bool> HoldBalanceAsync(Guid userId, decimal amount, string type, Guid refId)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.Balance)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user?.Balance == null)
            {
                _logger.LogError("User or balance not found for user {UserId}", userId);
                return false;
            }

            if (user.Balance.ActiveBalance < amount)
            {
                _logger.LogWarning("Insufficient active balance for user {UserId}", userId);
                return false;
            }

            var activeBefore = user.Balance.ActiveBalance;
            var heldBefore = user.Balance.HeldBalance;

            // Hold balance
            user.Balance.ActiveBalance -= amount;
            user.Balance.HeldBalance += amount;
            user.Balance.UpdatedAt = DateTime.UtcNow;

            // Create balance ledger entry
            var ledger = new BalanceLedger
            {
                UserId = userId,
                Type = BalanceTransactionType.PurchaseHold,
                Amount = amount,
                ActiveBefore = activeBefore,
                ActiveAfter = user.Balance.ActiveBalance,
                HeldBefore = heldBefore,
                HeldAfter = user.Balance.HeldBalance,
                Notes = $"Hold for transaction {refId}",
                RefId = refId,
                RefType = "Transaction",
                CreatedAt = DateTime.UtcNow
            };

            _context.BalanceLedgers.Add(ledger);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Balance held for user {UserId}: {Amount}", userId, amount);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error holding balance for user {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Debit held balance (after successful transaction)
    /// </summary>
    public async Task<bool> DebitHeldBalanceAsync(Guid userId, decimal amount, string? notes = null)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.Balance)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user?.Balance == null)
            {
                return false;
            }

            if (user.Balance.HeldBalance < amount)
            {
                _logger.LogWarning("Insufficient held balance for user {UserId}", userId);
                return false;
            }

            // Debit held balance
            user.Balance.HeldBalance -= amount;
            user.Balance.UpdatedAt = DateTime.UtcNow;

            // Create balance ledger entry
            var ledger = new BalanceLedger
            {
                UserId = userId,
                Type = BalanceTransactionType.PurchaseDebit,
                Amount = amount,
                ActiveBefore = user.Balance.ActiveBalance,
                ActiveAfter = user.Balance.ActiveBalance,
                Notes = notes ?? "Purchase completed",
                RefType = "Transaction",
                CreatedAt = DateTime.UtcNow
            };

            _context.BalanceLedgers.Add(ledger);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Held balance debited for user {UserId}: {Amount}", userId, amount);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error debiting held balance for user {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Release held balance (after failed transaction)
    /// </summary>
    public async Task<bool> ReleaseHeldBalanceAsync(Guid userId, decimal amount, string? notes = null)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.Balance)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user?.Balance == null)
            {
                return false;
            }

            if (user.Balance.HeldBalance < amount)
            {
                _logger.LogWarning("Insufficient held balance to release for user {UserId}", userId);
                return false;
            }

            var activeBefore = user.Balance.ActiveBalance;
            var heldBefore = user.Balance.HeldBalance;

            // Release held balance back to active
            user.Balance.HeldBalance -= amount;
            user.Balance.ActiveBalance += amount;
            user.Balance.UpdatedAt = DateTime.UtcNow;

            // Create balance ledger entry
            var ledger = new BalanceLedger
            {
                UserId = userId,
                Type = BalanceTransactionType.PurchaseRelease,
                Amount = amount,
                ActiveBefore = activeBefore,
                ActiveAfter = user.Balance.ActiveBalance,
                HeldBefore = heldBefore,
                HeldAfter = user.Balance.HeldBalance,
                Notes = notes ?? "Transaction failed - balance released",
                RefType = "Transaction",
                CreatedAt = DateTime.UtcNow
            };

            _context.BalanceLedgers.Add(ledger);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Held balance released for user {UserId}: {Amount}", userId, amount);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing held balance for user {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Process transaction (background job)
    /// </summary>
    public async Task<bool> ProcessTransactionAsync(Guid transactionId)
    {
        _logger.LogInformation("Processing transaction {TransactionId}", transactionId);

        var transaction = await _context.Transactions
            .Include(t => t.Product)
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == transactionId);

        if (transaction == null)
        {
            _logger.LogError("Transaction {TransactionId} not found", transactionId);
            return false;
        }

        if (transaction.Status != TransactionStatus.Pending)
        {
            _logger.LogWarning("Transaction {TransactionId} is not pending", transactionId);
            return false;
        }

        // Get supplier routing list for this product
        var supplierProducts = await _context.SupplierProducts
            .Include(sp => sp.Supplier)
            .Where(sp => sp.ProductId == transaction.ProductId && sp.IsActive)
            .OrderBy(sp => sp.Seq)
            .ToListAsync();

        if (!supplierProducts.Any())
        {
            _logger.LogError("No active suppliers found for product {ProductId}", transaction.ProductId);
            await FailTransactionAsync(transaction, "No suppliers available for this product");
            return false;
        }

        // Try each supplier in sequence
        foreach (var supplierProduct in supplierProducts)
        {
            var attemptResult = await TrySupplierAsync(transaction, supplierProduct);

            if (attemptResult.Success)
            {
                await SuccessTransactionAsync(transaction, supplierProduct.SupplierId, attemptResult);
                return true;
            }

            // Log failed attempt and continue to next supplier
            _logger.LogWarning("Supplier {SupplierId} failed for transaction {TransactionId}: {Message}",
                supplierProduct.SupplierId, transactionId, attemptResult.Message);
        }

        // All suppliers failed
        await FailTransactionAsync(transaction, "All suppliers failed");
        return false;
    }

    private async Task<SupplierPurchaseResult> TrySupplierAsync(Transaction transaction, SupplierProduct supplierProduct)
    {
        // Create attempt record
        var attempt = new TransactionAttempt
        {
            Id = 0, // Database will generate this
            TransactionId = transaction.Id,
            SupplierId = supplierProduct.SupplierId,
            SupplierProductId = supplierProduct.Id,
            Seq = supplierProduct.Seq,
            Status = AttemptStatus.Processing,
            AttemptedAt = DateTime.UtcNow
        };

        _context.TransactionAttempts.Add(attempt);
        await _context.SaveChangesAsync();

        // Create supplier adapter
        var adapter = _adapterFactory.CreateAdapter("DIGIFLAZZ", _loggerFactory);
        if (adapter == null)
        {
            attempt.Status = AttemptStatus.Failed;
            attempt.ErrorMessage = "Failed to create supplier adapter";
            attempt.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return new SupplierPurchaseResult
            {
                Success = false,
                Message = "Failed to create supplier adapter"
            };
        }

        // Build purchase request
        var request = new SupplierPurchaseRequest
        {
            SupplierId = supplierProduct.SupplierId,
            SupplierUsername = supplierProduct.Supplier?.Name ?? "",
            SupplierApiKey = supplierProduct.Supplier?.MemberId ?? "",
            SupplierApiSecret = supplierProduct.Supplier?.Password,
            SupplierApiUrl = supplierProduct.Supplier?.ApiBaseUrl ?? "",
            SupplierProductCode = supplierProduct.SupplierProductCode ?? "",
            DestinationNumber = transaction.Destination,
            ReferenceId = transaction.Id,
            TimeoutSeconds = supplierProduct.Supplier?.TimeoutSeconds ?? 30
        };

        SupplierPurchaseResult result;
        try
        {
            result = await adapter.PurchaseAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling supplier adapter");
            result = new SupplierPurchaseResult
            {
                Success = false,
                ErrorCode = "SYSTEM_ERROR",
                Message = ex.Message
            };
        }

        // Update attempt record
        attempt.Status = result.Success ? AttemptStatus.Success : AttemptStatus.Failed;
        attempt.ErrorMessage = result.Message;
        attempt.SupplierTrxId = result.SupplierTransactionId;
        attempt.SupplierRefId = result.SerialNumber;
        attempt.CompletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return result;
    }

    private async Task SuccessTransactionAsync(Transaction transaction, int supplierId, SupplierPurchaseResult result)
    {
        transaction.Status = TransactionStatus.Success;
        transaction.SupplierId = supplierId;
        transaction.SerialNumber = result.SerialNumber;
        transaction.SupplierTrxId = result.SupplierTransactionId;
        transaction.CompletedAt = DateTime.UtcNow;
        transaction.UpdatedAt = DateTime.UtcNow;

        // Debit held balance
        await DebitHeldBalanceAsync(transaction.UserId, transaction.SellPrice,
            $"Success: {result.SupplierTransactionId}");

        await _context.SaveChangesAsync();

        _logger.LogInformation("Transaction {TransactionId} completed successfully", transaction.Id);
    }

    private async Task FailTransactionAsync(Transaction transaction, string errorMessage)
    {
        transaction.Status = TransactionStatus.Failed;
        transaction.ErrorMessage = errorMessage;
        transaction.CompletedAt = DateTime.UtcNow;
        transaction.UpdatedAt = DateTime.UtcNow;

        // Release held balance
        await ReleaseHeldBalanceAsync(transaction.UserId, transaction.SellPrice, errorMessage);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Transaction {TransactionId} failed: {Message}", transaction.Id, errorMessage);
    }

    public async Task<Transaction?> GetTransactionByIdAsync(Guid id)
    {
        return await _context.Transactions
            .Include(t => t.Product)
            .Include(t => t.User)
            .Include(t => t.Supplier)
            .Include(t => t.Attempts)
                .ThenInclude(a => a.Supplier)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<(List<Transaction> Transactions, int TotalFiltered, int TotalRecords)> GetTransactionsPagedAsync(
        int page,
        int pageSize,
        string? search = null,
        Guid? userId = null,
        Guid? productId = null,
        string? status = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? sortColumn = null,
        string? sortDirection = null)
    {
        var query = _context.Transactions
            .Include(t => t.Product)
            .Include(t => t.User)
            .Include(t => t.Supplier)
            .AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t =>
                EF.Functions.ILike(t.Destination, $"%{search}%") ||
                EF.Functions.ILike(t.Sn ?? "", $"%{search}%") ||
                EF.Functions.ILike(t.SupplierTrxId ?? "", $"%{search}%"));
        }

        // Apply user filter
        if (userId.HasValue)
        {
            query = query.Where(t => t.UserId == userId.Value);
        }

        // Apply product filter
        if (productId.HasValue)
        {
            query = query.Where(t => t.ProductId == productId.Value);
        }

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(status))
        {
            var transactionStatus = status.ToLower() switch
            {
                "pending" => TransactionStatus.Pending,
                "processing" => TransactionStatus.Processing,
                "success" => TransactionStatus.Success,
                "failed" => TransactionStatus.Failed,
                "cancelled" => TransactionStatus.Cancelled,
                _ => (TransactionStatus?)null
            };
            if (transactionStatus.HasValue)
            {
                query = query.Where(t => t.Status == transactionStatus.Value);
            }
        }

        // Apply date range filter
        if (startDate.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= startDate.Value);
        }
        if (endDate.HasValue)
        {
            query = query.Where(t => t.CreatedAt <= endDate.Value);
        }

        var totalRecords = await _context.Transactions.CountAsync();
        var totalFiltered = await query.CountAsync();

        // Apply sorting
        if (!string.IsNullOrWhiteSpace(sortColumn))
        {
            var isDescending = sortDirection?.ToLower() == "desc";
            var sortCol = sortColumn.ToLower();

            if (isDescending)
            {
                query = sortCol switch
                {
                    "destination" => query.OrderByDescending(t => t.Destination),
                    "amount" => query.OrderByDescending(t => t.SellPrice),
                    "status" => query.OrderByDescending(t => t.Status),
                    "createdat" => query.OrderByDescending(t => t.CreatedAt),
                    _ => query.OrderByDescending(t => t.CreatedAt)
                };
            }
            else
            {
                query = sortCol switch
                {
                    "destination" => query.OrderBy(t => t.Destination),
                    "amount" => query.OrderBy(t => t.SellPrice),
                    "status" => query.OrderBy(t => t.Status),
                    "createdat" => query.OrderBy(t => t.CreatedAt),
                    _ => query.OrderBy(t => t.CreatedAt)
                };
            }
        }
        else
        {
            query = query.OrderByDescending(t => t.CreatedAt);
        }

        var transactions = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (transactions, totalFiltered, totalRecords);
    }
}
