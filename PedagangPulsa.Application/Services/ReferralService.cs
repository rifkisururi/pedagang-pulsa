using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Infrastructure.Data;

namespace PedagangPulsa.Application.Services;

public class ReferralService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ReferralService> _logger;

    public ReferralService(AppDbContext context, ILogger<ReferralService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(List<ReferralLog> Logs, int TotalFiltered, int TotalRecords)> GetReferralLogsPagedAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? orderColumn = null,
        string? orderDirection = null)
    {
        // ⚡ Bolt Optimization: Use AsNoTracking for read-only logs to eliminate change tracking overhead, saving CPU and memory per request.
        var query = _context.ReferralLogs
            .AsNoTracking()
            .Include(rl => rl.Referrer)
            .Include(rl => rl.Referee)
            .AsQueryable();

        var totalRecords = await query.CountAsync();

        // Search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(rl =>
                rl.Referrer != null && rl.Referrer.Username.Contains(search) ||
                rl.Referee != null && rl.Referee.Username.Contains(search));
        }

        // Status filter
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReferralBonusStatus>(status, true, out var statusEnum))
        {
            query = query.Where(rl => rl.BonusStatus == statusEnum);
        }

        // Date filter
        if (startDate.HasValue)
        {
            query = query.Where(rl => rl.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(rl => rl.CreatedAt <= endDate.Value);
        }

        var totalFiltered = await query.CountAsync();

        // Sorting
        (query, orderColumn, orderDirection) = ApplySorting(query, orderColumn, orderDirection);

        // Pagination
        var logs = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .OrderByDescending(rl => rl.CreatedAt)
            .ToListAsync();

        return (logs, totalFiltered, totalRecords);
    }

    private (IQueryable<ReferralLog> Query, string Column, string Direction) ApplySorting(
        IQueryable<ReferralLog> query,
        string? orderColumn,
        string? orderDirection)
    {
        var columnMappings = new Dictionary<string, System.Linq.Expressions.Expression<Func<ReferralLog, object>>>
        {
            { "createdAt", rl => rl.CreatedAt },
            { "referrer", rl => rl.Referrer != null ? rl.Referrer.Username : "" },
            { "referee", rl => rl.Referee != null ? rl.Referee.Username : "" },
            { "bonusAmount", rl => rl.BonusAmount ?? 0 },
            { "bonusStatus", rl => rl.BonusStatus.ToString() }
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

    public async Task<List<ReferralSummary>> GetTopReferrersAsync(int count = 20)
    {
        // ⚡ Bolt Optimization: Use AsNoTracking for read-only user summary to eliminate change tracking overhead, saving CPU and memory per request.
        return await _context.Users
            .AsNoTracking()
            .Include(u => u.ReferralLogsAsReferrer)
            .Where(u => u.ReferralLogsAsReferrer.Any(rl => rl.BonusStatus == ReferralBonusStatus.Paid))
            .Select(u => new ReferralSummary
            {
                UserId = u.Id,
                Username = u.Username,
                FullName = u.FullName,
                TotalReferrals = u.ReferralLogsAsReferrer.Count,
                TotalBonusPaid = u.ReferralLogsAsReferrer
                    .Where(rl => rl.BonusStatus == ReferralBonusStatus.Paid)
                    .Sum(rl => rl.BonusAmount ?? 0),
                PendingBonus = u.ReferralLogsAsReferrer
                    .Where(rl => rl.BonusStatus == ReferralBonusStatus.Pending)
                    .Sum(rl => rl.BonusAmount ?? 0)
            })
            .OrderByDescending(r => r.TotalBonusPaid)
            .Take(count)
            .ToListAsync();
    }

    public async Task<bool> PayPendingBonusAsync(Guid logId, string? performedBy = null)
    {
        // Check if using InMemory database (for testing)
        var isInMemory = _context.Database.ProviderName?.Contains("InMemory") ?? false;

        if (isInMemory)
        {
            return await ProcessPayBonusAsync(logId, performedBy);
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var result = await ProcessPayBonusAsync(logId, performedBy);
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
            _logger.LogError(ex, "Error paying referral bonus for LogId={LogId}", logId);
            return false;
        }
    }

    private async Task<bool> ProcessPayBonusAsync(Guid logId, string? performedBy)
    {
        var logIdGuid = logId;
        var log = await _context.ReferralLogs
            .Include(rl => rl.Referrer)
            .ThenInclude(r => r.Balance)
            .Where(rl => rl.Id == logIdGuid && rl.BonusStatus == ReferralBonusStatus.Pending)
            .FirstOrDefaultAsync();

        if (log == null || log.Referrer?.Balance == null)
        {
            return false;
        }

        // Check if using InMemory database
        var isInMemory = _context.Database.ProviderName?.Contains("InMemory") ?? false;

        // Lock user balance for update (skip for InMemory)
        if (!isInMemory)
        {
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT * FROM \"UserBalances\" WHERE \"UserId\" = {0} FOR UPDATE",
                log.Referrer.Id);
        }

        var balanceBefore = log.Referrer.Balance.ActiveBalance;

        // Credit bonus
        log.Referrer.Balance.ActiveBalance += (log.BonusAmount ?? 0);
        var balanceAfter = log.Referrer.Balance.ActiveBalance;

        // Create ledger entry
        var ledger = new BalanceLedger
        {
            UserId = log.Referrer.Id,
            Type = BalanceTransactionType.ReferralBonus,
            Amount = log.BonusAmount ?? 0,
            ActiveBefore = balanceBefore,
            ActiveAfter = balanceAfter,
            HeldBefore = log.Referrer.Balance.HeldBalance,
            HeldAfter = log.Referrer.Balance.HeldBalance,
            RefType = "ReferralLog",
            RefId = log.Id,
            Notes = $"Referral bonus from {log.Referee?.Username ?? "user"}",
            CreatedBy = performedBy != null ? Guid.Parse(performedBy) : null,
            CreatedAt = DateTime.UtcNow
        };

        _context.BalanceLedgers.Add(ledger);

        // Update referral log
        log.BonusStatus = ReferralBonusStatus.Paid;
        log.PaidAt = DateTime.UtcNow;

        // Update user
        log.Referrer.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Referral bonus paid: LogId={LogId}, Referrer={Referrer}, Amount={Amount}, PerformedBy={PerformedBy}",
            logId, log.Referrer.Username, log.BonusAmount, performedBy);

        return true;
    }

    public async Task<bool> CancelReferralBonusAsync(Guid logId, string? reason = null, string? performedBy = null)
    {
        // Check if using InMemory database (for testing)
        var isInMemory = _context.Database.ProviderName?.Contains("InMemory") ?? false;

        if (isInMemory)
        {
            return await ProcessCancelBonusAsync(logId, reason, performedBy);
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var result = await ProcessCancelBonusAsync(logId, reason, performedBy);
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
            _logger.LogError(ex, "Error cancelling referral bonus for LogId={LogId}", logId);
            return false;
        }
    }

    private async Task<bool> ProcessCancelBonusAsync(Guid logId, string? reason, string? performedBy)
    {
        var logIdGuid = logId;
        var log = await _context.ReferralLogs
            .Where(rl => rl.Id == logIdGuid && rl.BonusStatus == ReferralBonusStatus.Pending)
            .FirstOrDefaultAsync();

        if (log == null)
        {
            return false;
        }

        log.BonusStatus = ReferralBonusStatus.Cancelled;
        log.CancelledAt = DateTime.UtcNow;
        log.CancellationReason = reason;
        log.CancelledBy = performedBy != null ? Guid.Parse(performedBy) : null;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Referral bonus cancelled: LogId={LogId}, Referrer={ReferrerId}, Reason={Reason}, PerformedBy={PerformedBy}",
            logId, log.ReferrerId, reason, performedBy);

        return true;
    }
}

public class ReferralSummary
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public int TotalReferrals { get; set; }
    public decimal TotalBonusPaid { get; set; }
    public decimal PendingBonus { get; set; }
}
