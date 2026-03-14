using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Infrastructure.Data;

namespace PedagangPulsa.Application.Services;

public class SupplierBalanceService
{
    private readonly AppDbContext _context;
    private readonly ILogger<SupplierBalanceService> _logger;

    public SupplierBalanceService(AppDbContext context, ILogger<SupplierBalanceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(List<SupplierBalanceLedger> Ledgers, int TotalFiltered, int TotalRecords)> GetSupplierLedgersPagedAsync(
        int page,
        int pageSize,
        string? search = null,
        string? type = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? orderColumn = null,
        string? orderDirection = null)
    {
        var query = _context.SupplierBalanceLedgers
            .Include(bl => bl.Supplier)
            .AsQueryable();

        var totalRecords = await query.CountAsync();

        // Search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(bl =>
                bl.Supplier != null &&
                (bl.Supplier.Name.Contains(search) ||
                 bl.Supplier.Code.Contains(search) ||
                 (bl.Description != null && bl.Description.Contains(search))));
        }

        // Type filter
        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(bl => bl.Type == type);
        }

        // Date filter
        if (startDate.HasValue)
        {
            query = query.Where(bl => bl.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(bl => bl.CreatedAt <= endDate.Value);
        }

        var totalFiltered = await query.CountAsync();

        // Sorting
        (query, orderColumn, orderDirection) = ApplyLedgerSorting(query, orderColumn, orderDirection);

        // Pagination
        var ledgers = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .OrderByDescending(bl => bl.CreatedAt)
            .ToListAsync();

        return (ledgers, totalFiltered, totalRecords);
    }

    private (IQueryable<SupplierBalanceLedger> Query, string Column, string Direction) ApplyLedgerSorting(
        IQueryable<SupplierBalanceLedger> query,
        string? orderColumn,
        string? orderDirection)
    {
        var columnMappings = new Dictionary<string, System.Linq.Expressions.Expression<Func<SupplierBalanceLedger, object>>>
        {
            { "createdAt", bl => bl.CreatedAt },
            { "supplierName", bl => bl.Supplier != null ? bl.Supplier.Name : "" },
            { "type", bl => bl.Type },
            { "amount", bl => bl.Amount },
            { "balanceBefore", bl => bl.BalanceBefore },
            { "balanceAfter", bl => bl.BalanceAfter }
        };

        if (string.IsNullOrWhiteSpace(orderColumn) || !columnMappings.ContainsKey(orderColumn))
        {
            orderColumn = "createdAt";
            orderDirection = "desc";
        }

        var isAscending = string.IsNullOrWhiteSpace(orderDirection) || orderDirection.ToLower() == "asc";
        var keySelector = columnMappings[orderColumn];

        query = isAscending
            ? query.OrderBy(keySelector)
            : query.OrderByDescending(keySelector);

        return (query, orderColumn!, orderDirection ?? "desc");
    }

    public async Task<Supplier?> GetSupplierWithBalanceAsync(int supplierId)
    {
        return await _context.Suppliers
            .Include(s => s.Balance)
            .FirstOrDefaultAsync(s => s.Id == supplierId);
    }

    public async Task<List<Supplier>> GetAllSuppliersWithBalanceAsync()
    {
        return await _context.Suppliers
            .Include(s => s.Balance)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<bool> DepositToSupplierAsync(
        int supplierId,
        decimal amount,
        string description,
        string? adminNote = null,
        string? performedBy = null)
    {
        if (amount <= 0)
        {
            _logger.LogWarning("Deposit amount must be positive for supplier {SupplierId}", supplierId);
            return false;
        }

        // Check if using InMemory database (for testing)
        var isInMemory = _context.Database.ProviderName?.Contains("InMemory") ?? false;

        if (isInMemory)
        {
            return await ProcessDepositAsync(supplierId, amount, description, adminNote, performedBy);
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var result = await ProcessDepositAsync(supplierId, amount, description, adminNote, performedBy);
            if (result)
            {
                await transaction.CommitAsync();
            }
            else
            {
                await transaction.RollbackAsync();
            }
            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error depositing to supplier {SupplierId}", supplierId);
            return false;
        }
    }

    private async Task<bool> ProcessDepositAsync(
        int supplierId,
        decimal amount,
        string description,
        string? adminNote,
        string? performedBy)
    {
        var supplier = await _context.Suppliers
            .Include(s => s.Balance)
            .Where(s => s.Id == supplierId)
            .FirstOrDefaultAsync();

        if (supplier == null)
        {
            return false;
        }

        // Check if using InMemory database
        var isInMemory = _context.Database.ProviderName?.Contains("InMemory") ?? false;

        // Lock supplier balance for update (skip for InMemory)
        if (!isInMemory && supplier.Balance != null)
        {
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT * FROM \"SupplierBalances\" WHERE \"Id\" = {0} FOR UPDATE",
                supplier.Balance.Id);
        }

        // Create or get balance
        if (supplier.Balance == null)
        {
            supplier.Balance = new SupplierBalance
            {
                SupplierId = supplier.Id,
                ActiveBalance = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.SupplierBalances.Add(supplier.Balance);
            await _context.SaveChangesAsync();
        }

        var balanceBefore = supplier.Balance.ActiveBalance;

        // Add deposit
        supplier.Balance.ActiveBalance += amount;
        supplier.Balance.UpdatedAt = DateTime.UtcNow;

        var balanceAfter = supplier.Balance.ActiveBalance;

        // Create ledger entry
        var ledger = new SupplierBalanceLedger
        {
            Id = DateTime.UtcNow.Ticks,
            SupplierId = supplierId,
            Type = "Deposit",
            Amount = amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            Description = description,
            AdminNote = adminNote,
            PerformedBy = performedBy,
            CreatedAt = DateTime.UtcNow
        };

        _context.SupplierBalanceLedgers.Add(ledger);

        // Update supplier
        supplier.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Deposited {Amount} to supplier {SupplierId}: {SupplierName}, Before: {Before}, After: {After}, By: {PerformedBy}",
            amount, supplierId, supplier.Name, balanceBefore, balanceAfter, performedBy);

        return true;
    }

    public async Task<bool> AdjustSupplierBalanceAsync(
        int supplierId,
        decimal amount,
        string type,
        string description,
        string? adminNote = null,
        string? performedBy = null)
    {
        // Check if using InMemory database (for testing)
        var isInMemory = _context.Database.ProviderName?.Contains("InMemory") ?? false;

        if (isInMemory)
        {
            return await ProcessAdjustAsync(supplierId, amount, type, description, adminNote, performedBy);
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var result = await ProcessAdjustAsync(supplierId, amount, type, description, adminNote, performedBy);
            if (result)
            {
                await transaction.CommitAsync();
            }
            else
            {
                await transaction.RollbackAsync();
            }
            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error adjusting balance for supplier {SupplierId}", supplierId);
            return false;
        }
    }

    private async Task<bool> ProcessAdjustAsync(
        int supplierId,
        decimal amount,
        string type,
        string description,
        string? adminNote,
        string? performedBy)
    {
        var supplier = await _context.Suppliers
            .Include(s => s.Balance)
            .Where(s => s.Id == supplierId)
            .FirstOrDefaultAsync();

        if (supplier == null || supplier.Balance == null)
        {
            return false;
        }

        // Check if using InMemory database
        var isInMemory = _context.Database.ProviderName?.Contains("InMemory") ?? false;

        // Lock supplier balance for update (skip for InMemory)
        if (!isInMemory)
        {
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT * FROM \"SupplierBalances\" WHERE \"Id\" = {0} FOR UPDATE",
                supplier.Balance.Id);
        }

        var balanceBefore = supplier.Balance.ActiveBalance;

        // Adjust balance
        supplier.Balance.ActiveBalance += amount;

        // Ensure balance doesn't go negative
        if (supplier.Balance.ActiveBalance < 0)
        {
            _logger.LogWarning("Insufficient balance for supplier {SupplierId}", supplierId);
            return false;
        }

        var balanceAfter = supplier.Balance.ActiveBalance;
        supplier.Balance.UpdatedAt = DateTime.UtcNow;

        // Create ledger entry
        var ledger = new SupplierBalanceLedger
        {
            Id = DateTime.UtcNow.Ticks,
            SupplierId = supplierId,
            Type = type,
            Amount = amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            Description = description,
            AdminNote = adminNote,
            PerformedBy = performedBy,
            CreatedAt = DateTime.UtcNow
        };

        _context.SupplierBalanceLedgers.Add(ledger);

        // Update supplier
        supplier.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Balance adjusted for supplier {SupplierId}: {Type} {Amount}, Before: {Before}, After: {After}, By: {PerformedBy}",
            supplierId, type, amount, balanceBefore, balanceAfter, performedBy);

        return true;
    }

    public async Task<bool> DebitFromSupplierAsync(
        int supplierId,
        decimal amount,
        string description,
        long? transactionAttemptId = null)
    {
        // Check if using InMemory database (for testing)
        var isInMemory = _context.Database.ProviderName?.Contains("InMemory") ?? false;

        if (isInMemory)
        {
            return await ProcessDebitAsync(supplierId, amount, description);
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var result = await ProcessDebitAsync(supplierId, amount, description);
            if (result)
            {
                await transaction.CommitAsync();
            }
            else
            {
                await transaction.RollbackAsync();
            }
            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error debiting from supplier {SupplierId}", supplierId);
            return false;
        }
    }

    private async Task<bool> ProcessDebitAsync(
        int supplierId,
        decimal amount,
        string description)
    {
        var supplier = await _context.Suppliers
            .Include(s => s.Balance)
            .Where(s => s.Id == supplierId)
            .FirstOrDefaultAsync();

        if (supplier == null || supplier.Balance == null)
        {
            return false;
        }

        // Check if using InMemory database
        var isInMemory = _context.Database.ProviderName?.Contains("InMemory") ?? false;

        // Lock supplier balance for update (skip for InMemory)
        if (!isInMemory)
        {
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT * FROM \"SupplierBalances\" WHERE \"Id\" = {0} FOR UPDATE",
                supplier.Balance.Id);
        }

        var balanceBefore = supplier.Balance.ActiveBalance;

        // Check sufficient balance
        if (balanceBefore < amount)
        {
            _logger.LogWarning("Insufficient balance for supplier {SupplierId}. Required: {Required}, Available: {Available}",
                supplierId, amount, balanceBefore);
            return false;
        }

        // Debit balance
        supplier.Balance.ActiveBalance -= amount;
        supplier.Balance.UpdatedAt = DateTime.UtcNow;

        var balanceAfter = supplier.Balance.ActiveBalance;

        // Create ledger entry
        var ledger = new SupplierBalanceLedger
        {
            Id = DateTime.UtcNow.Ticks,
            SupplierId = supplierId,
            Type = "Transaction",
            Amount = -amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };

        _context.SupplierBalanceLedgers.Add(ledger);

        // Update supplier
        supplier.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Debited {Amount} from supplier {SupplierId}: {SupplierName}, Before: {Before}, After: {After}",
            amount, supplierId, supplier.Name, balanceBefore, balanceAfter);

        return true;
    }
}
