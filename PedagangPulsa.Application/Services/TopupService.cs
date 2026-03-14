using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Infrastructure.Data;

namespace PedagangPulsa.Application.Services;

public class TopupService
{
    private readonly AppDbContext _context;

    public TopupService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(List<TopupRequest> Topups, int TotalFiltered, int TotalRecords)> GetTopupRequestsPagedAsync(
        int page,
        int pageSize,
        string? search = null,
        string? status = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? sortColumn = null,
        string? sortDirection = null)
    {
        var query = _context.TopupRequests
            .Include(t => t.User)
            .AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t =>
                EF.Functions.ILike(t.User.Username, $"%{search}%") ||
                (t.BankAccount != null && EF.Functions.ILike(t.BankAccount.BankName, $"%{search}%")));
        }

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(status))
        {
            var topupStatus = status.ToLower() switch
            {
                "pending" => TopupStatus.Pending,
                "approved" => TopupStatus.Approved,
                "rejected" => TopupStatus.Rejected,
                _ => (TopupStatus?)null
            };
            if (topupStatus.HasValue)
            {
                query = query.Where(t => t.Status == topupStatus.Value);
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

        var totalRecords = await _context.TopupRequests.CountAsync();
        var totalFiltered = await query.CountAsync();

        // Apply sorting
        if (!string.IsNullOrWhiteSpace(sortColumn))
        {
            var sortDir = sortDirection?.ToLower() == "desc";
            var sortCol = sortColumn.ToLower();

            if (sortDir)
            {
                query = sortCol switch
                {
                    "username" => query.OrderByDescending(t => t.User.Username),
                    "amount" => query.OrderByDescending(t => t.Amount),
                    "bank" => query.OrderByDescending(t => t.BankAccount != null ? t.BankAccount.BankName : ""),
                    "status" => query.OrderByDescending(t => t.Status),
                    "createdat" => query.OrderByDescending(t => t.CreatedAt),
                    _ => query.OrderByDescending(t => t.CreatedAt)
                };
            }
            else
            {
                query = sortCol switch
                {
                    "username" => query.OrderBy(t => t.User.Username),
                    "amount" => query.OrderBy(t => t.Amount),
                    "bank" => query.OrderBy(t => t.BankAccount != null ? t.BankAccount.BankName : ""),
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

        var topups = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (topups, totalFiltered, totalRecords);
    }

    public async Task<TopupRequest?> GetTopupRequestByIdAsync(Guid id)
    {
        return await _context.TopupRequests
            .Include(t => t.User)
                .ThenInclude(u => u.Balance)
            .Include(t => t.BankAccount)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<bool> ApproveTopupAsync(Guid id, decimal finalAmount, string? notes, string? approvedBy)
    {
        // Check if using in-memory database (for testing)
        bool isInMemory = _context.Database.ProviderName?.Contains("InMemory") == true;

        // In-memory database doesn't support transactions
        if (!isInMemory)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var result = await ProcessTopupApprovalAsync(id, finalAmount, notes, approvedBy);
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
            catch
            {
                await transaction.RollbackAsync();
                return false;
            }
        }
        else
        {
            // Run without transaction for in-memory database
            return await ProcessTopupApprovalAsync(id, finalAmount, notes, approvedBy);
        }
    }

    private async Task<bool> ProcessTopupApprovalAsync(Guid id, decimal finalAmount, string? notes, string? approvedBy)
    {
        var topup = await _context.TopupRequests
            .Include(t => t.User)
                .ThenInclude(u => u.Balance)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (topup == null)
        {
            return false;
        }

        if (topup.Status != TopupStatus.Pending)
        {
            return false;
        }

        // Lock and update user balance
        var user = topup.User;
        if (user?.Balance == null)
        {
            return false;
        }

        var activeBefore = user.Balance.ActiveBalance;
        user.Balance.ActiveBalance += finalAmount;
        user.Balance.UpdatedAt = DateTime.UtcNow;

        // Create balance ledger entry
        var ledger = new BalanceLedger
        {
            Id = 0, // Database will generate this
            UserId = user.Id,
            Type = BalanceTransactionType.Topup,
            Amount = finalAmount,
            ActiveBefore = activeBefore,
            ActiveAfter = user.Balance.ActiveBalance,
            Notes = notes ?? $"Topup approved. Reference: {topup.Id}",
            RefId = topup.Id,
            RefType = "TopupRequest",
            CreatedAt = DateTime.UtcNow
        };

        _context.BalanceLedgers.Add(ledger);

        // Update topup request
        topup.Status = TopupStatus.Approved;
        topup.Notes = notes;
        topup.ApprovedBy = approvedBy != null ? Guid.Parse(approvedBy) : null;
        topup.ApprovedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> RejectTopupAsync(Guid id, string reason, string? rejectedBy)
    {
        var topup = await _context.TopupRequests.FindAsync(id);
        if (topup == null || topup.Status != TopupStatus.Pending)
        {
            return false;
        }

        topup.Status = TopupStatus.Rejected;
        topup.RejectReason = reason;
        topup.RejectedBy = rejectedBy != null ? Guid.Parse(rejectedBy) : null;
        topup.RejectedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }
}
