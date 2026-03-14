using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Infrastructure.Data;

namespace PedagangPulsa.Application.Services;

public class UserLevelService
{
    private readonly AppDbContext _context;

    public UserLevelService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<UserLevel>> GetAllLevelsAsync()
    {
        return await _context.UserLevels
            .OrderBy(l => l.Id)
            .ToListAsync();
    }

    public async Task<UserLevel?> GetLevelByIdAsync(int id)
    {
        return await _context.UserLevels
            .FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task<UserLevel?> CreateLevelAsync(UserLevel level)
    {
        // Check if level with same name exists
        var exists = await _context.UserLevels
            .AnyAsync(l => l.Name == level.Name);

        if (exists)
        {
            return null;
        }

        _context.UserLevels.Add(level);
        await _context.SaveChangesAsync();
        return level;
    }

    public async Task<UserLevel?> UpdateLevelAsync(UserLevel level)
    {
        var existing = await _context.UserLevels.FindAsync(level.Id);
        if (existing == null) return null;

        // Check if another level with same name exists
        var nameExists = await _context.UserLevels
            .AnyAsync(l => l.Name == level.Name && l.Id != level.Id);

        if (nameExists)
        {
            return null;
        }

        existing.Name = level.Name;
        existing.MarkupType = level.MarkupType;
        existing.MarkupValue = level.MarkupValue;
        existing.Description = level.Description;
        existing.IsActive = level.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteLevelAsync(int id)
    {
        var level = await _context.UserLevels.FindAsync(id);
        if (level == null) return false;

        // Check if there are users using this level
        var hasUsers = await _context.Users
            .AnyAsync(u => u.LevelId == id);

        if (hasUsers)
        {
            return false;
        }

        _context.UserLevels.Remove(level);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<(List<UserLevel> Levels, int TotalFiltered, int TotalRecords)> GetLevelsPagedAsync(
        int page,
        int pageSize,
        string? search = null,
        bool? isActive = null)
    {
        var query = _context.UserLevels.AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Check if using InMemory database (for testing)
            var isInMemory = _context.Database.ProviderName?.Contains("InMemory") ?? false;

            if (isInMemory)
            {
                // Use case-insensitive Contains for InMemory
                var searchLower = search.ToLower();
                query = query.Where(l =>
                    l.Name.ToLower().Contains(searchLower) ||
                    (l.Description != null && l.Description.ToLower().Contains(searchLower)));
            }
            else
            {
                // Use ILike for PostgreSQL
                query = query.Where(l =>
                    EF.Functions.ILike(l.Name, $"%{search}%") ||
                    EF.Functions.ILike(l.Description ?? "", $"%{search}%"));
            }
        }

        // Apply active filter
        if (isActive.HasValue)
        {
            query = query.Where(l => l.IsActive == isActive.Value);
        }

        var totalRecords = await _context.UserLevels.CountAsync();
        var totalFiltered = await query.CountAsync();

        var levels = await query
            .OrderBy(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (levels, totalFiltered, totalRecords);
    }
}
