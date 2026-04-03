using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Infrastructure.Data;

namespace PedagangPulsa.Application.Services;

public class BalanceService
{
    private readonly AppDbContext _context;
    private readonly ILogger<BalanceService> _logger;

    public BalanceService(AppDbContext context, ILogger<BalanceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(List<BalanceLedger> Ledgers, int TotalFiltered, int TotalRecords)> GetBalanceLedgersPagedAsync(
        int page,
        int pageSize,
        string? search = null,
        string? type = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? orderColumn = null,
        string? orderDirection = null)
    {
        var query = _context.BalanceLedgers
            .Include(bl => bl.User)
            .AsQueryable();

        var totalRecords = await query.CountAsync();

        // Search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(bl =>
                bl.User != null &&
                (bl.User.UserName.Contains(search) ||
                 bl.User.Email != null && bl.User.Email.Contains(search) ||
                 bl.Notes != null && bl.Notes.Contains(search)));
        }

        // Type filter
        if (!string.IsNullOrWhiteSpace(type))
        {
            if (Enum.TryParse<BalanceTransactionType>(type, true, out var transactionType))
            {
                query = query.Where(bl => bl.Type == transactionType);
            }
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
        (query, orderColumn, orderDirection) = ApplyBalanceLedgerSorting(query, orderColumn, orderDirection);

        // Pagination
        var ledgers = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .OrderByDescending(bl => bl.CreatedAt)
            .ToListAsync();

        return (ledgers, totalFiltered, totalRecords);
    }

    private (IQueryable<BalanceLedger> Query, string Column, string Direction) ApplyBalanceLedgerSorting(
        IQueryable<BalanceLedger> query,
        string? orderColumn,
        string? orderDirection)
    {
        var columnMappings = new Dictionary<string, System.Linq.Expressions.Expression<Func<BalanceLedger, object>>>
        {
            { "createdAt", bl => bl.CreatedAt },
            { "username", bl => bl.User != null ? bl.User.UserName : "" },
            { "type", bl => bl.Type },
            { "amount", bl => bl.Amount },
            { "activeBefore", bl => bl.ActiveBefore },
            { "activeAfter", bl => bl.ActiveAfter }
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

    public async Task<User?> GetUserWithBalanceAsync(Guid userId)
    {
        return await _context.Users
            .Include(u => u.Balance)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<bool> AdjustUserBalanceAsync(
        Guid userId,
        decimal amount,
        string type,
        string description,
        string? adminNote = null,
        string? performedBy = null)
    {
        // Check if using in-memory database (for testing)
        bool isInMemory = _context.Database.ProviderName?.Contains("InMemory") == true;

        // In-memory database doesn't support transactions
        if (!isInMemory)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var result = await ProcessBalanceAdjustmentAsync(userId, amount, type, description, adminNote, performedBy);
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
                _logger.LogError(ex, "Error adjusting balance for user {UserId}", userId);
                return false;
            }
        }
        else
        {
            // Run without transaction for in-memory database
            try
            {
                return await ProcessBalanceAdjustmentAsync(userId, amount, type, description, adminNote, performedBy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adjusting balance for user {UserId}", userId);
                return false;
            }
        }
    }

    private async Task<bool> ProcessBalanceAdjustmentAsync(
        Guid userId,
        decimal amount,
        string type,
        string description,
        string? adminNote,
        string? performedBy)
    {
        var user = await _context.Users
            .Include(u => u.Balance)
            .Where(u => u.Id == userId)
            .FirstOrDefaultAsync();

        if (user == null || user.Balance == null)
        {
            return false;
        }

        // Lock user balance for update (skip for in-memory database)
        bool isInMemory = _context.Database.ProviderName?.Contains("InMemory") == true;
        if (!isInMemory)
        {
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT * FROM \"UserBalances\" WHERE \"UserId\" = {0} FOR UPDATE",
                user.Balance.UserId);
        }

        var balanceBefore = user.Balance.ActiveBalance;
        var heldBefore = user.Balance.HeldBalance;

        // Adjust balance
        user.Balance.ActiveBalance += amount;

        // Ensure balance doesn't go negative
        if (user.Balance.ActiveBalance < 0)
        {
            _logger.LogWarning("Insufficient balance for user {UserId}", userId);
            return false;
        }

        var balanceAfter = user.Balance.ActiveBalance;
        var heldAfter = user.Balance.HeldBalance; // Held balance doesn't change for adjustments

        // Create ledger entry
        var ledger = new BalanceLedger
        {
            UserId = userId,
            Type = Enum.Parse<BalanceTransactionType>(type),
            Amount = amount,
            ActiveBefore = balanceBefore,
            ActiveAfter = balanceAfter,
            HeldBefore = heldBefore,
            HeldAfter = heldAfter,
            Notes = description,
            CreatedBy = performedBy != null ? Guid.Parse(performedBy) : null,
            CreatedAt = DateTime.UtcNow
        };

        _context.BalanceLedgers.Add(ledger);

        // Update user
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Balance adjusted for user {UserId}: {Type} {Amount}, Before: {Before}, After: {After}, By: {PerformedBy}",
            userId, type, amount, balanceBefore, balanceAfter, performedBy);

        return true;
    }

    public async Task<List<Domain.Entities.User>> SearchUsersAsync(string search)
    {
        return await _context.Users
            .Where(u => u.UserName.Contains(search) ||
                       (u.Email != null && u.Email.Contains(search)) ||
                       (u.FullName != null && u.FullName.Contains(search)) ||
                       (u.Phone != null && u.Phone.Contains(search)))
            .Take(20)
            .ToListAsync();
    }

    public async Task<List<Domain.Entities.User>> GetTopBalanceHoldersAsync(int count = 20)
    {
        return await _context.Users
            .Include(u => u.Balance)
            .Where(u => u.Balance != null)
            .OrderByDescending(u => u.Balance!.ActiveBalance)
            .Take(count)
            .ToListAsync();
    }
}
