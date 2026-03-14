using Microsoft.Extensions.Logging;
using ClosedXML.Excel;
using PedagangPulsa.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Infrastructure.Data;

namespace PedagangPulsa.Application.Services;

public class ExportService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ExportService> _logger;

    public ExportService(AppDbContext context, ILogger<ExportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<byte[]> ExportTransactionsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? status = null,
        int? userId = null)
    {
        var query = _context.Transactions
            .Include(t => t.User)
            .Include(t => t.Product)
            .ThenInclude(p => p.Category)
            .AsQueryable();

        if (startDate.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            var end = endDate.Value.AddDays(1).AddTicks(-1);
            query = query.Where(t => t.CreatedAt <= end);
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TransactionStatus>(status, true, out var statusEnum))
        {
            query = query.Where(t => t.Status == statusEnum);
        }

        if (userId.HasValue)
        {
            query = query.Where(t => t.User.Id == Guid.Parse(userId.Value.ToString()));
        }

        var transactions = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Transactions");

        // Headers
        worksheet.Cell("B2").Value = "Transaction Export";
        worksheet.Cell("B2").Style.Font.Bold = true;
        worksheet.Cell("B2").Style.Font.FontSize = 16;

        if (startDate.HasValue || endDate.HasValue)
        {
            var dateRange = $"{startDate.GetValueOrDefault():dd MMM yyyy} - {endDate.GetValueOrDefault():dd MMM yyyy}";
            worksheet.Cell("B3").Value = $"Period: {dateRange}";
        }

        // Column Headers
        var headerRow = 5;
        worksheet.Cell(headerRow, 1).Value = "No";
        worksheet.Cell(headerRow, 2).Value = "Date";
        worksheet.Cell(headerRow, 3).Value = "Reference ID";
        worksheet.Cell(headerRow, 4).Value = "Username";
        worksheet.Cell(headerRow, 5).Value = "Category";
        worksheet.Cell(headerRow, 6).Value = "Product";
        worksheet.Cell(headerRow, 7).Value = "Destination";
        worksheet.Cell(headerRow, 8).Value = "Sell Price";
        worksheet.Cell(headerRow, 9).Value = "Cost Price";
        worksheet.Cell(headerRow, 10).Value = "Profit";
        worksheet.Cell(headerRow, 11).Value = "Status";
        worksheet.Cell(headerRow, 12).Value = "Created At";

        var headerRange = worksheet.Range(headerRow, 1, headerRow, 12);
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Font.Bold = true;

        // Data
        var row = headerRow + 1;
        var no = 1;

        foreach (var trx in transactions)
        {
            var profit = (trx.SellPrice - (trx.CostPrice ?? 0));

            worksheet.Cell(row, 1).Value = no++;
            worksheet.Cell(row, 2).Value = trx.CreatedAt.ToString("dd MMM yyyy");
            worksheet.Cell(row, 3).Value = trx.ReferenceId;
            worksheet.Cell(row, 4).Value = trx.User?.Username ?? "-";
            worksheet.Cell(row, 5).Value = trx.Product?.Category?.Name ?? "-";
            worksheet.Cell(row, 6).Value = trx.Product?.Name ?? "-";
            worksheet.Cell(row, 7).Value = trx.Destination;
            worksheet.Cell(row, 8).Value = trx.SellPrice;
            worksheet.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
            worksheet.Cell(row, 9).Value = trx.CostPrice ?? 0;
            worksheet.Cell(row, 9).Style.NumberFormat.Format = "#,##0";
            worksheet.Cell(row, 10).Value = profit;
            worksheet.Cell(row, 10).Style.NumberFormat.Format = "#,##0";
            worksheet.Cell(row, 11).Value = trx.Status.ToString();
            worksheet.Cell(row, 12).Value = trx.CreatedAt.ToString("dd MMM yyyy HH:mm:ss");

            // Color code status
            if (trx.Status == TransactionStatus.Success)
            {
                worksheet.Cell(row, 11).Style.Fill.BackgroundColor = XLColor.FromHtml("#C6EFCE");
            }
            else if (trx.Status == TransactionStatus.Failed)
            {
                worksheet.Cell(row, 11).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFC7CE");
            }

            row++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        // Add borders
        var dataRange = worksheet.Range(headerRow, 1, row - 1, 12);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportTopupRequestsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? status = null)
    {
        var query = _context.TopupRequests
            .Include(t => t.User)
            .Include(t => t.BankAccount)
            .AsQueryable();

        if (startDate.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            var end = endDate.Value.AddDays(1).AddTicks(-1);
            query = query.Where(t => t.CreatedAt <= end);
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TopupStatus>(status, true, out var statusEnum))
        {
            query = query.Where(t => t.Status == statusEnum);
        }

        var topups = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Topup Requests");

        // Headers
        worksheet.Cell("B2").Value = "Topup Request Export";
        worksheet.Cell("B2").Style.Font.Bold = true;
        worksheet.Cell("B2").Style.Font.FontSize = 16;

        if (startDate.HasValue || endDate.HasValue)
        {
            var dateRange = $"{startDate.GetValueOrDefault():dd MMM yyyy} - {endDate.GetValueOrDefault():dd MMM yyyy}";
            worksheet.Cell("B3").Value = $"Period: {dateRange}";
        }

        // Column Headers
        var headerRow = 5;
        worksheet.Cell(headerRow, 1).Value = "No";
        worksheet.Cell(headerRow, 2).Value = "Date";
        worksheet.Cell(headerRow, 3).Value = "Username";
        worksheet.Cell(headerRow, 4).Value = "Full Name";
        worksheet.Cell(headerRow, 5).Value = "Amount";
        worksheet.Cell(headerRow, 6).Value = "Final Amount";
        worksheet.Cell(headerRow, 7).Value = "Bank";
        worksheet.Cell(headerRow, 8).Value = "Account Number";
        worksheet.Cell(headerRow, 9).Value = "Account Name";
        worksheet.Cell(headerRow, 10).Value = "Status";
        worksheet.Cell(headerRow, 11).Value = "Approved By";
        worksheet.Cell(headerRow, 12).Value = "Approved At";

        var headerRange = worksheet.Range(headerRow, 1, headerRow, 12);
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Font.Bold = true;

        // Data
        var row = headerRow + 1;
        var no = 1;

        foreach (var topup in topups)
        {
            worksheet.Cell(row, 1).Value = no++;
            worksheet.Cell(row, 2).Value = topup.CreatedAt.ToString("dd MMM yyyy");
            worksheet.Cell(row, 3).Value = topup.User?.Username ?? "-";
            worksheet.Cell(row, 4).Value = topup.User?.FullName ?? "-";
            worksheet.Cell(row, 5).Value = topup.Amount;
            worksheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
            worksheet.Cell(row, 6).Value = topup.Amount; // Using same as FinalAmount since it doesn't exist
            worksheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
            worksheet.Cell(row, 7).Value = topup.BankAccount?.BankName ?? "-";
            worksheet.Cell(row, 8).Value = topup.BankAccount?.AccountNumber ?? "-";
            worksheet.Cell(row, 9).Value = topup.BankAccount?.AccountName ?? "-";
            worksheet.Cell(row, 10).Value = topup.Status.ToString();
            worksheet.Cell(row, 11).Value = topup.ApprovedBy.HasValue ? topup.ApprovedBy.Value.ToString() : "-";
            worksheet.Cell(row, 12).Value = topup.ApprovedAt?.ToString("dd MMM yyyy HH:mm") ?? "-";

            // Color code status
            if (topup.Status == TopupStatus.Approved)
            {
                worksheet.Cell(row, 10).Style.Fill.BackgroundColor = XLColor.FromHtml("#C6EFCE");
            }
            else if (topup.Status == TopupStatus.Rejected)
            {
                worksheet.Cell(row, 10).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFC7CE");
            }

            row++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        // Add borders
        var dataRange = worksheet.Range(headerRow, 1, row - 1, 12);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportBalanceLedgerAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? type = null,
        Guid? userId = null)
    {
        var query = _context.BalanceLedgers
            .Include(bl => bl.User)
            .AsQueryable();

        if (startDate.HasValue)
        {
            query = query.Where(bl => bl.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            var end = endDate.Value.AddDays(1).AddTicks(-1);
            query = query.Where(bl => bl.CreatedAt <= end);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            if (Enum.TryParse<BalanceTransactionType>(type, true, out var transactionType))
            {
                query = query.Where(bl => bl.Type == transactionType);
            }
        }

        if (userId.HasValue)
        {
            query = query.Where(bl => bl.UserId == userId.Value);
        }

        var ledgers = await query
            .OrderByDescending(bl => bl.CreatedAt)
            .Take(50000) // Limit to 50k records for Excel
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Balance Ledger");

        // Headers
        worksheet.Cell("B2").Value = "Balance Ledger Export";
        worksheet.Cell("B2").Style.Font.Bold = true;
        worksheet.Cell("B2").Style.Font.FontSize = 16;

        if (startDate.HasValue || endDate.HasValue)
        {
            var dateRange = $"{startDate.GetValueOrDefault():dd MMM yyyy} - {endDate.GetValueOrDefault():dd MMM yyyy}";
            worksheet.Cell("B3").Value = $"Period: {dateRange}";
        }

        // Column Headers
        var headerRow = 5;
        worksheet.Cell(headerRow, 1).Value = "No";
        worksheet.Cell(headerRow, 2).Value = "Date";
        worksheet.Cell(headerRow, 3).Value = "Username";
        worksheet.Cell(headerRow, 4).Value = "Type";
        worksheet.Cell(headerRow, 5).Value = "Amount";
        worksheet.Cell(headerRow, 6).Value = "Balance Before";
        worksheet.Cell(headerRow, 7).Value = "Balance After";
        worksheet.Cell(headerRow, 8).Value = "Description";
        worksheet.Cell(headerRow, 9).Value = "Performed By";

        var headerRange = worksheet.Range(headerRow, 1, headerRow, 9);
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Font.Bold = true;

        // Data
        var row = headerRow + 1;
        var no = 1;

        foreach (var ledger in ledgers)
        {
            worksheet.Cell(row, 1).Value = no++;
            worksheet.Cell(row, 2).Value = ledger.CreatedAt.ToString("dd MMM yyyy HH:mm");
            worksheet.Cell(row, 3).Value = ledger.User?.Username ?? "-";
            worksheet.Cell(row, 4).Value = ledger.Type.ToString();
            worksheet.Cell(row, 5).Value = ledger.Amount;
            worksheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
            worksheet.Cell(row, 6).Value = ledger.ActiveBefore;
            worksheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
            worksheet.Cell(row, 7).Value = ledger.ActiveAfter;
            worksheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0";
            worksheet.Cell(row, 8).Value = ledger.Notes ?? "-";
            worksheet.Cell(row, 9).Value = ledger.CreatedBy.HasValue ? ledger.CreatedBy.Value.ToString() : "-";

            // Color code credit/debit
            if (ledger.Amount > 0)
            {
                worksheet.Cell(row, 5).Style.Font.FontColor = XLColor.FromHtml("#008000");
            }
            else if (ledger.Amount < 0)
            {
                worksheet.Cell(row, 5).Style.Font.FontColor = XLColor.FromHtml("#FF0000");
            }

            row++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        // Add borders
        var dataRange = worksheet.Range(headerRow, 1, row - 1, 9);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportProfitReportAsync(
        DateTime startDate,
        DateTime endDate)
    {
        var start = startDate.Date;
        var end = endDate.Date.AddDays(1).AddTicks(-1);

        var transactions = await _context.Transactions
            .Include(t => t.Product)
            .Include(t => t.Attempts)
            .ThenInclude(a => a.Supplier)
            .Where(t => t.Status == TransactionStatus.Success && t.CreatedAt >= start && t.CreatedAt <= end)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        // Group by date
        var dailyData = transactions
            .GroupBy(t => t.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                TotalRevenue = g.Sum(t => t.SellPrice),
                TotalCost = g.Sum(t => t.CostPrice ?? 0),
                TotalProfit = g.Sum(t => t.SellPrice) - g.Sum(t => t.CostPrice ?? 0),
                TotalTransactions = g.Count()
            })
            .ToList();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Profit Report");

        // Headers
        worksheet.Cell("B2").Value = "Profit Report";
        worksheet.Cell("B2").Style.Font.Bold = true;
        worksheet.Cell("B2").Style.Font.FontSize = 16;
        worksheet.Cell("B3").Value = $"Period: {start:dd MMM yyyy} - {endDate:dd MMM yyyy}";

        // Summary
        worksheet.Cell("B5").Value = "Summary";
        worksheet.Cell("B5").Style.Font.Bold = true;
        worksheet.Cell("B5").Style.Font.FontSize = 12;

        var totalRevenue = dailyData.Sum(d => d.TotalRevenue);
        var totalCost = dailyData.Sum(d => d.TotalCost);
        var totalProfit = dailyData.Sum(d => d.TotalProfit);
        var totalTransactions = dailyData.Sum(d => d.TotalTransactions);

        worksheet.Cell("B6").Value = "Total Revenue:";
        worksheet.Cell("C6").Value = totalRevenue;
        worksheet.Cell("C6").Style.NumberFormat.Format = "#,##0";
        worksheet.Cell("C6").Style.Font.Bold = true;

        worksheet.Cell("B7").Value = "Total Cost:";
        worksheet.Cell("C7").Value = totalCost;
        worksheet.Cell("C7").Style.NumberFormat.Format = "#,##0";

        worksheet.Cell("B8").Value = "Total Profit:";
        worksheet.Cell("C8").Value = totalProfit;
        worksheet.Cell("C8").Style.NumberFormat.Format = "#,##0";
        worksheet.Cell("C8").Style.Font.Bold = true;
        worksheet.Cell("C8").Style.Font.FontColor = XLColor.FromHtml("#008000");

        worksheet.Cell("B9").Value = "Profit Margin:";
        worksheet.Cell("C9").Value = totalRevenue > 0 ? (totalProfit / totalRevenue * 100).ToString("F2") + "%" : "0%";
        worksheet.Cell("C9").Style.Font.Bold = true;

        worksheet.Cell("B10").Value = "Total Transactions:";
        worksheet.Cell("C10").Value = totalTransactions;
        worksheet.Cell("C10").Style.Font.Bold = true;

        // Daily Breakdown
        var headerRow = 13;
        worksheet.Cell(headerRow, 1).Value = "Date";
        worksheet.Cell(headerRow, 2).Value = "Transactions";
        worksheet.Cell(headerRow, 3).Value = "Revenue";
        worksheet.Cell(headerRow, 4).Value = "Cost";
        worksheet.Cell(headerRow, 5).Value = "Profit";
        worksheet.Cell(headerRow, 6).Value = "Margin %";

        var headerRange = worksheet.Range(headerRow, 1, headerRow, 6);
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Font.Bold = true;

        var row = headerRow + 1;

        foreach (var day in dailyData)
        {
            var margin = day.TotalRevenue > 0 ? (day.TotalProfit / day.TotalRevenue * 100) : 0;

            worksheet.Cell(row, 1).Value = day.Date.ToString("dd MMM yyyy");
            worksheet.Cell(row, 2).Value = day.TotalTransactions;
            worksheet.Cell(row, 3).Value = day.TotalRevenue;
            worksheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
            worksheet.Cell(row, 4).Value = day.TotalCost;
            worksheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0";
            worksheet.Cell(row, 5).Value = day.TotalProfit;
            worksheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
            worksheet.Cell(row, 6).Value = margin.ToString("F2") + "%";

            row++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        // Add borders
        var dataRange = worksheet.Range(headerRow, 1, row - 1, 6);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
