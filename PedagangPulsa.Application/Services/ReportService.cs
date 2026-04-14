using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Infrastructure.Data;

namespace PedagangPulsa.Application.Services;

public class ReportService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ReportService> _logger;

    public ReportService(AppDbContext context, ILogger<ReportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<DailyProfitReport> GetDailyProfitReportAsync(DateTime date)
    {
        var startOfDay = date.Date;
        var endOfDay = date.Date.AddDays(1).AddTicks(-1);

        // ⚡ Bolt Optimization: Add AsNoTracking for read-only reporting query to eliminate change-tracking overhead
        var transactions = await _context.Transactions
            .AsNoTracking()
            .Include(t => t.Product)
            .Include(t => t.Attempts)
            .ThenInclude(a => a.Supplier)
            .Where(t => t.Status == TransactionStatus.Success && t.CreatedAt >= startOfDay && t.CreatedAt <= endOfDay)
            .ToListAsync();

        var totalRevenue = transactions.Sum(t => t.SellPrice);
        var totalCost = transactions.Sum(t => t.CostPrice ?? 0);
        var totalProfit = totalRevenue - totalCost;

        var byProduct = transactions
            .GroupBy(t => new { t.ProductId, ProductName = t.Product?.Name ?? "Unknown" })
            .Select(g => new ProductProfitItem
            {
                ProductName = g.Key.ProductName,
                TotalTransactions = g.Count(),
                TotalRevenue = g.Sum(t => t.SellPrice),
                TotalCost = g.Sum(t => t.CostPrice ?? 0),
                TotalProfit = g.Sum(t => t.SellPrice) - g.Sum(t => t.CostPrice ?? 0)
            })
            .OrderByDescending(p => p.TotalProfit)
            .ToList();

        var bySupplier = transactions
            .SelectMany(t => t.Attempts.Where(a => a.Status == AttemptStatus.Success))
            .GroupBy(a => new { a.SupplierId, SupplierName = a.Supplier?.Name ?? "Unknown" })
            .Select(g => new SupplierProfitItem
            {
                SupplierName = g.Key.SupplierName,
                TotalTransactions = g.Count(),
                TotalRevenue = g.Sum(a => a.Transaction!.SellPrice),
                TotalCost = g.Sum(a => a.Transaction!.CostPrice ?? 0),
                TotalProfit = g.Sum(a => a.Transaction!.SellPrice) - g.Sum(a => a.Transaction!.CostPrice ?? 0)
            })
            .OrderByDescending(s => s.TotalProfit)
            .ToList();

        return new DailyProfitReport
        {
            Date = date,
            TotalRevenue = totalRevenue,
            TotalCost = totalCost,
            TotalProfit = totalProfit,
            TotalTransactions = transactions.Count,
            ProfitMargin = totalRevenue > 0 ? (totalProfit / totalRevenue) * 100 : 0,
            ByProduct = byProduct,
            BySupplier = bySupplier
        };
    }

    public async Task<List<DailyProfitSummary>> GetDailyProfitSummaryAsync(DateTime startDate, DateTime endDate)
    {
        // ⚡ Bolt Optimization: Replace DB queries in a loop with a single query using AsNoTracking and projection
        var startOfPeriod = startDate.Date;
        var endOfPeriod = endDate.Date.AddDays(1).AddTicks(-1);

        var transactions = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.Status == TransactionStatus.Success && t.CreatedAt >= startOfPeriod && t.CreatedAt <= endOfPeriod)
            .Select(t => new { t.CreatedAt, t.SellPrice, t.CostPrice })
            .ToListAsync();

        var summaries = new List<DailyProfitSummary>();

        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            var startOfDay = date;
            var endOfDay = date.AddDays(1).AddTicks(-1);

            var dailyTransactions = transactions
                .Where(t => t.CreatedAt >= startOfDay && t.CreatedAt <= endOfDay)
                .ToList();

            var totalRevenue = dailyTransactions.Sum(t => t.SellPrice);
            var totalCost = dailyTransactions.Sum(t => t.CostPrice ?? 0);
            var totalProfit = totalRevenue - totalCost;

            summaries.Add(new DailyProfitSummary
            {
                Date = date,
                TotalRevenue = totalRevenue,
                TotalCost = totalCost,
                TotalProfit = totalProfit,
                TotalTransactions = dailyTransactions.Count,
                ProfitMargin = totalRevenue > 0 ? (totalProfit / totalRevenue) * 100 : 0
            });
        }

        return summaries;
    }

    public async Task<ProfitBySupplierReport> GetProfitBySupplierAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        // ⚡ Bolt Optimization: Add AsNoTracking for read-only reporting query to eliminate change-tracking overhead
        var query = _context.TransactionAttempts
            .AsNoTracking()
            .Include(a => a.Supplier)
            .Include(a => a.Transaction)
            .Where(a => a.Status == AttemptStatus.Success);

        if (startDate.HasValue)
        {
            query = query.Where(a => a.AttemptedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(a => a.AttemptedAt <= endDate.Value);
        }

        var attempts = await query.ToListAsync();

        var bySupplier = attempts
            .GroupBy(a => new { a.SupplierId, SupplierName = a.Supplier?.Name ?? "Unknown" })
            .Select(g => new SupplierProfitItem
            {
                SupplierName = g.Key.SupplierName,
                TotalTransactions = g.Count(),
                TotalRevenue = g.Sum(a => a.Transaction!.SellPrice),
                TotalCost = g.Sum(a => a.Transaction!.CostPrice ?? 0),
                TotalProfit = g.Sum(a => a.Transaction!.SellPrice) - g.Sum(a => a.Transaction!.CostPrice ?? 0)
            })
            .OrderByDescending(s => s.TotalProfit)
            .ToList();

        var totalRevenue = bySupplier.Sum(s => s.TotalRevenue);
        var totalCost = bySupplier.Sum(s => s.TotalCost);
        var totalProfit = bySupplier.Sum(s => s.TotalProfit);

        return new ProfitBySupplierReport
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalRevenue = totalRevenue,
            TotalCost = totalCost,
            TotalProfit = totalProfit,
            TotalTransactions = bySupplier.Sum(s => s.TotalTransactions),
            ProfitMargin = totalRevenue > 0 ? (totalProfit / totalRevenue) * 100 : 0,
            BySupplier = bySupplier
        };
    }

    public async Task<ProfitByProductReport> GetProfitByProductAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        // ⚡ Bolt Optimization: Add AsNoTracking for read-only reporting query to eliminate change-tracking overhead
        var query = _context.Transactions
            .AsNoTracking()
            .Include(t => t.Product)
            .Where(t => t.Status == TransactionStatus.Success);

        if (startDate.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(t => t.CreatedAt <= endDate.Value);
        }

        var transactions = await query.ToListAsync();

        var byProduct = transactions
            .GroupBy(t => new { t.ProductId, ProductName = t.Product?.Name ?? "Unknown", Category = t.Product?.Category?.Name ?? "Unknown" })
            .Select(g => new ProductProfitItem
            {
                ProductName = g.Key.ProductName,
                Category = g.Key.Category,
                TotalTransactions = g.Count(),
                TotalRevenue = g.Sum(t => t.SellPrice),
                TotalCost = g.Sum(t => t.CostPrice ?? 0),
                TotalProfit = g.Sum(t => t.SellPrice) - g.Sum(t => t.CostPrice ?? 0)
            })
            .OrderByDescending(p => p.TotalProfit)
            .ToList();

        var totalRevenue = byProduct.Sum(p => p.TotalRevenue);
        var totalCost = byProduct.Sum(p => p.TotalCost);
        var totalProfit = byProduct.Sum(p => p.TotalProfit);

        return new ProfitByProductReport
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalRevenue = totalRevenue,
            TotalCost = totalCost,
            TotalProfit = totalProfit,
            TotalTransactions = byProduct.Sum(p => p.TotalTransactions),
            ProfitMargin = totalRevenue > 0 ? (totalProfit / totalRevenue) * 100 : 0,
            ByProduct = byProduct
        };
    }
}

// Report Models
public class DailyProfitReport
{
    public DateTime Date { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit { get; set; }
    public int TotalTransactions { get; set; }
    public decimal ProfitMargin { get; set; }
    public List<ProductProfitItem> ByProduct { get; set; } = new();
    public List<SupplierProfitItem> BySupplier { get; set; } = new();
}

public class DailyProfitSummary
{
    public DateTime Date { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit { get; set; }
    public int TotalTransactions { get; set; }
    public decimal ProfitMargin { get; set; }
}

public class ProfitBySupplierReport
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit { get; set; }
    public int TotalTransactions { get; set; }
    public decimal ProfitMargin { get; set; }
    public List<SupplierProfitItem> BySupplier { get; set; } = new();
}

public class ProfitByProductReport
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit { get; set; }
    public int TotalTransactions { get; set; }
    public decimal ProfitMargin { get; set; }
    public List<ProductProfitItem> ByProduct { get; set; } = new();
}

public class ProductProfitItem
{
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int TotalTransactions { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit { get; set; }
}

public class SupplierProfitItem
{
    public string SupplierName { get; set; } = string.Empty;
    public int TotalTransactions { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit { get; set; }
}
