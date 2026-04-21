using Microsoft.Extensions.Logging;
using MediatR;
using PedagangPulsa.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Infrastructure.Data;

namespace PedagangPulsa.Application.Services;

public class UserService
{
    private readonly AppDbContext _context;

    public UserService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(List<User> Users, int TotalFiltered, int TotalRecords)> GetUsersPagedAsync(
        int page,
        int pageSize,
        string? search = null,
        int? levelId = null,
        string? status = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? sortColumn = null,
        string? sortDirection = null)
    {
        // ⚡ Bolt Optimization: Use AsNoTracking for read-only user queries to eliminate change tracking overhead, saving memory allocations and CPU cycles per request.
        var query = _context.Users
            .AsNoTracking()
            .Include(u => u.Level)
            .Include(u => u.Balance)
            .AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Check if using InMemory database (for testing)
            var isInMemory = _context.Database.ProviderName?.Contains("InMemory") ?? false;

            if (isInMemory)
            {
                // Use case-insensitive Contains for InMemory
                var searchLower = search.ToLower();
                query = query.Where(u =>
                    u.Username.ToLower().Contains(searchLower) ||
                    (u.FullName != null && u.FullName.ToLower().Contains(searchLower)) ||
                    (u.Email != null && u.Email.ToLower().Contains(searchLower)) ||
                    (u.Phone != null && u.Phone.ToLower().Contains(searchLower)));
            }
            else
            {
                // Use ILike for PostgreSQL
                query = query.Where(u =>
                    EF.Functions.ILike(u.Username, $"%{search}%") ||
                    EF.Functions.ILike(u.FullName ?? "", $"%{search}%") ||
                    EF.Functions.ILike(u.Email ?? "", $"%{search}%") ||
                    EF.Functions.ILike(u.Phone ?? "", $"%{search}%"));
            }
        }

        // Apply level filter
        if (levelId.HasValue)
        {
            query = query.Where(u => u.LevelId == levelId.Value);
        }

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(status))
        {
            var userStatus = status.ToLower() switch
            {
                "active" => UserStatus.Active,
                "inactive" => UserStatus.Inactive,
                "suspended" => UserStatus.Suspended,
                _ => (UserStatus?)null
            };
            if (userStatus.HasValue)
            {
                query = query.Where(u => u.Status == userStatus.Value);
            }
        }

        // Apply date range filter
        if (startDate.HasValue)
        {
            query = query.Where(u => u.CreatedAt >= startDate.Value);
        }
        if (endDate.HasValue)
        {
            query = query.Where(u => u.CreatedAt <= endDate.Value);
        }

        // Get total records before pagination
        var totalRecords = await query.CountAsync();

        // Get total filtered records
        var totalFiltered = totalRecords;

        // Apply sorting
        if (!string.IsNullOrWhiteSpace(sortColumn))
        {
            var sortDir = sortDirection?.ToLower() == "desc";
            var sortCol = sortColumn.ToLower();

            if (sortDir)
            {
                query = sortCol switch
                {
                    "username" => query.OrderByDescending(u => u.Username),
                    "fullname" => query.OrderByDescending(u => u.FullName),
                    "email" => query.OrderByDescending(u => u.Email),
                    "phone" => query.OrderByDescending(u => u.Phone),
                    "level" => query.OrderByDescending(u => u.Level.Name),
                    "status" => query.OrderByDescending(u => u.Status),
                    "balance" => query.OrderByDescending(u => u.Balance!.ActiveBalance),
                    "createdat" => query.OrderByDescending(u => u.CreatedAt),
                    _ => query.OrderByDescending(u => u.CreatedAt)
                };
            }
            else
            {
                query = sortCol switch
                {
                    "username" => query.OrderBy(u => u.Username),
                    "fullname" => query.OrderBy(u => u.FullName),
                    "email" => query.OrderBy(u => u.Email),
                    "phone" => query.OrderBy(u => u.Phone),
                    "level" => query.OrderBy(u => u.Level.Name),
                    "status" => query.OrderBy(u => u.Status),
                    "balance" => query.OrderBy(u => u.Balance!.ActiveBalance),
                    "createdat" => query.OrderBy(u => u.CreatedAt),
                    _ => query.OrderBy(u => u.CreatedAt)
                };
            }
        }
        else
        {
            query = query.OrderByDescending(u => u.CreatedAt);
        }

        // Apply pagination
        var users = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (users, totalFiltered, totalRecords);
    }

    public async Task<User?> GetUserByIdAsync(Guid id)
    {
        return await _context.Users
            .Include(u => u.Level)
            .Include(u => u.Balance)
            .Include(u => u.Referrer)
            .Include(u => u.BalanceLedgers
                .OrderByDescending(bl => bl.CreatedAt)
                .Take(10))
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<List<UserLevel>> GetAllLevelsAsync()
    {
        // ⚡ Bolt Optimization: Use AsNoTracking for read-only lookups.
        return await _context.UserLevels
            .AsNoTracking()
            .OrderBy(l => l.Id)
            .ToListAsync();
    }

    public async Task<bool> UpdateUserLevelAsync(Guid userId, int newLevelId, string? updatedByUserId, string? notes)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        var oldLevelId = user.LevelId;
        user.LevelId = newLevelId;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Log to audit (placeholder - will be implemented with proper audit service)
        return true;
    }

    public async Task<bool> SuspendUserAsync(Guid userId, string reason, string? suspendedByUserId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        user.Status = UserStatus.Suspended;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Log to audit (placeholder)
        return true;
    }

    public async Task<bool> UnsuspendUserAsync(Guid userId, string? unsuspendedByUserId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        user.Status = UserStatus.Active;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Log to audit (placeholder)
        return true;
    }

    public async Task<List<ProductCategory>> GetCategoriesAsync()
    {
        // ⚡ Bolt Optimization: Use AsNoTracking for read-only lookups.
        return await _context.ProductCategories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();
    }
}
