using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Application.Abstractions.Persistence;
using PedagangPulsa.Domain.Entities;

namespace PedagangPulsa.Application.Services;

public class SupplierRegexPatternService
{
    private readonly IAppDbContext _context;

    public SupplierRegexPatternService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<List<SupplierRegexPattern>> GetBySupplierAsync(int supplierId)
    {
        return await _context.SupplierRegexPatterns
            .Where(p => p.SupplierId == supplierId)
            .OrderBy(p => p.SeqNo)
            .ToListAsync();
    }

    public async Task<SupplierRegexPattern?> GetByIdAsync(int id)
    {
        return await _context.SupplierRegexPatterns
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<(List<SupplierRegexPattern> Patterns, int TotalFiltered, int TotalRecords)> GetPagedAsync(
        int page,
        int pageSize,
        int? supplierId = null,
        string? search = null,
        bool? isTrxSukses = null)
    {
        var query = _context.SupplierRegexPatterns
            .Include(p => p.Supplier)
            .AsQueryable();

        if (supplierId.HasValue)
        {
            query = query.Where(p => p.SupplierId == supplierId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(p =>
                p.Label.ToLower().Contains(searchLower) ||
                p.Regex.ToLower().Contains(searchLower));
        }

        if (isTrxSukses.HasValue)
        {
            query = query.Where(p => p.IsTrxSukses == isTrxSukses.Value);
        }

        var totalRecords = await _context.SupplierRegexPatterns.CountAsync();
        var totalFiltered = await query.CountAsync();

        var patterns = await query
            .OrderBy(p => p.SupplierId)
            .ThenBy(p => p.SeqNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (patterns, totalFiltered, totalRecords);
    }

    public async Task<SupplierRegexPattern?> CreateAsync(SupplierRegexPattern pattern)
    {
        var exists = await _context.SupplierRegexPatterns
            .AnyAsync(p => p.SupplierId == pattern.SupplierId && p.SeqNo == pattern.SeqNo);

        if (exists)
        {
            return null;
        }

        pattern.CreatedAt = DateTime.UtcNow;

        _context.SupplierRegexPatterns.Add(pattern);
        await _context.SaveChangesAsync();
        return pattern;
    }

    public async Task<SupplierRegexPattern?> UpdateAsync(SupplierRegexPattern pattern)
    {
        var existing = await _context.SupplierRegexPatterns.FindAsync(pattern.Id);
        if (existing == null) return null;

        var seqConflict = await _context.SupplierRegexPatterns
            .AnyAsync(p => p.SupplierId == pattern.SupplierId && p.SeqNo == pattern.SeqNo && p.Id != pattern.Id);

        if (seqConflict)
        {
            return null;
        }

        existing.SupplierId = pattern.SupplierId;
        existing.SeqNo = pattern.SeqNo;
        existing.IsTrxSukses = pattern.IsTrxSukses;
        existing.Label = pattern.Label;
        existing.Regex = pattern.Regex;
        existing.SampleMessage = pattern.SampleMessage;
        existing.IsActive = pattern.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var pattern = await _context.SupplierRegexPatterns.FindAsync(id);
        if (pattern == null) return false;

        _context.SupplierRegexPatterns.Remove(pattern);
        await _context.SaveChangesAsync();
        return true;
    }

    public RegexTestResult TestRegex(string pattern, string testMessage)
    {
        try
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline, TimeSpan.FromSeconds(5));
            var match = regex.Match(testMessage);

            if (!match.Success)
            {
                return new RegexTestResult
                {
                    IsMatch = false,
                    Message = "Tidak ada kecocokan (no match).",
                    Groups = []
                };
            }

            var groups = match.Groups
                .OfType<Group>()
                .Where(g => g.Success)
                .Select(g => new RegexGroupResult
                {
                    Name = string.IsNullOrEmpty(g.Name) ? $"Group[{g.Index}]" : g.Name,
                    Value = g.Value
                })
                .ToList();

            return new RegexTestResult
            {
                IsMatch = true,
                Message = "Kecocokan ditemukan (match found).",
                Groups = groups
            };
        }
        catch (ArgumentException ex)
        {
            return new RegexTestResult
            {
                IsMatch = false,
                Message = $"Regex tidak valid: {ex.Message}",
                Groups = []
            };
        }
        catch (RegexMatchTimeoutException)
        {
            return new RegexTestResult
            {
                IsMatch = false,
                Message = "Regex timeout (lebih dari 5 detik). Periksa pola regex Anda.",
                Groups = []
            };
        }
    }
}

public class RegexTestResult
{
    public bool IsMatch { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<RegexGroupResult> Groups { get; set; } = [];
}

public class RegexGroupResult
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
