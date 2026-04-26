using Microsoft.Extensions.Logging;
using MediatR;
using PedagangPulsa.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Application.Abstractions.Persistence;
using PedagangPulsa.Application.Abstractions.Caching;
using PedagangPulsa.Domain.Entities;

namespace PedagangPulsa.Application.Services;

public class UserService
{
    private readonly IAppDbContext _context;
    private readonly IRedisService? _redisService;

    public UserService(IAppDbContext context, IRedisService? redisService = null)
    {
        _context = context;
        _redisService = redisService;
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
        var query = _context.Users
            .Include(u => u.Level)
            .Include(u => u.Balance)
            .AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(u =>
                u.UserName.ToLower().Contains(searchLower) ||
                (u.FullName != null && u.FullName.ToLower().Contains(searchLower)) ||
                (u.Email != null && u.Email.ToLower().Contains(searchLower)) ||
                (u.Phone != null && u.Phone.ToLower().Contains(searchLower)));
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
                    "username" => query.OrderByDescending(u => u.UserName),
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
                    "username" => query.OrderBy(u => u.UserName),
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
            .Include(u => u.BalanceLedgers
                .OrderByDescending(bl => bl.CreatedAt)
                .Take(10))
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<List<UserLevel>> GetAllLevelsAsync()
    {
        return await _context.UserLevels
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

    public async Task<bool> UnblockPinAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        user.PinFailedAttempts = 0;
        user.PinLockedAt = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Clear Redis lockout key
        if (_redisService != null)
        {
            try
            {
                await _redisService.RemoveAsync($"pin_lockout:{userId}");
            }
            catch
            {
                // Redis unavailable — ignore
            }
        }

        return true;
    }

    public async Task<bool> ResetPasswordAsync(Guid userId, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            return false;

        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ResetPinAsync(Guid userId, string newPin)
    {
        if (string.IsNullOrWhiteSpace(newPin) || newPin.Length != 6 || !newPin.All(char.IsDigit))
            return false;

        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        user.PinHash = BCrypt.Net.BCrypt.HashPassword(newPin);
        user.PinFailedAttempts = 0;
        user.PinLockedAt = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Clear Redis lockout key
        if (_redisService != null)
        {
            try
            {
                await _redisService.RemoveAsync($"pin_lockout:{userId}");
            }
            catch
            {
                // Redis unavailable — ignore
            }
        }

        return true;
    }

    public async Task<bool> SetUserStatusAsync(Guid userId, UserStatus status)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        user.Status = status;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<ProductCategory>> GetCategoriesAsync()
    {
        return await _context.ProductCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();
    }
}
