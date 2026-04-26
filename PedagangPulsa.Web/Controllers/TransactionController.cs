using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Web.Areas.Admin.ViewModels;

namespace PedagangPulsa.Web.Controllers;

[Authorize]
public class TransactionController : Controller
{
    private readonly TransactionService _transactionService;
    private readonly ILogger<TransactionController> _logger;

    public TransactionController(TransactionService transactionService, ILogger<TransactionController> logger)
    {
        _transactionService = transactionService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var transaction = await _transactionService.GetTransactionByIdAsync(id);
        if (transaction == null)
        {
            return NotFound();
        }

        var model = new TransactionDetailViewModel
        {
            Id = transaction.Id,
            ReferenceId = transaction.Id.ToString(),
            Status = transaction.Status.ToString(),
            CreatedAt = transaction.CreatedAt,
            CompletedAt = transaction.CompletedAt,
            UserId = transaction.UserId,
            Username = transaction.User?.UserName ?? "Unknown",
            ProductId = transaction.ProductId,
            ProductName = transaction.Product?.Name ?? "Unknown",
            Destination = transaction.Destination,
            SellPrice = transaction.SellPrice,
            SerialNumber = transaction.Sn,
            SupplierTrxId = transaction.SupplierTrxId,
            SupplierId = transaction.SupplierId,
            SupplierName = transaction.Supplier?.Name ?? "Unknown",
            ErrorMessage = transaction.ErrorMessage,
            Attempts = transaction.Attempts.Select(a => new TransactionDetailViewModel.AttemptItem
            {
                Id = a.Id,
                SupplierName = a.Supplier?.Name ?? "Unknown",
                Seq = a.Seq,
                Status = a.Status.ToString(),
                AttemptedAt = a.AttemptedAt,
                CompletedAt = a.CompletedAt,
                ErrorMessage = a.ErrorMessage,
                SupplierRefId = a.SupplierRefId,
                SupplierTrxId = a.SupplierTrxId
            }).OrderByDescending(a => a.AttemptedAt).ToList()
        };

        return View(model);
    }

    #region AJAX Endpoints

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessTransaction(Guid id)
    {
        try
        {
            // Check transaction status to determine which method to call
            var transaction = await _transactionService.GetTransactionByIdAsync(id);
            if (transaction == null)
            {
                return Json(new { success = false, message = "Transaction not found." });
            }

            if (transaction.Status == TransactionStatus.Processing)
            {
                // Reprocess a queued transaction
                var (success, message) = await _transactionService.ReprocessTransactionAsync(id);
                if (success)
                {
                    TempData["Success"] = message;
                    return Json(new { success = true });
                }

                TempData["Error"] = message;
                return Json(new { success = false, message });
            }

            if (transaction.Status == TransactionStatus.Pending)
            {
                // Normal process flow
                var result = await _transactionService.ProcessTransactionAsync(id);
                if (result)
                {
                    TempData["Success"] = "Transaction processed successfully.";
                    return Json(new { success = true });
                }

                var tx = await _transactionService.GetTransactionByIdAsync(id);
                var msg = tx?.ErrorMessage ?? "Failed to process transaction.";
                TempData["Error"] = msg;
                return Json(new { success = false, message = msg });
            }

            return Json(new { success = false, message = $"Transaction is {transaction.Status}, cannot process." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing transaction {TransactionId}", id);
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> GetData(
        [FromForm] int draw,
        [FromForm] int start,
        [FromForm] int length,
        [FromForm] string? search = null,
        [FromForm] Guid? userId = null,
        [FromForm] Guid? productId = null,
        [FromForm] string? status = null,
        [FromForm] string? startDate = null,
        [FromForm] string? endDate = null,
        [FromForm] string? orderColumn = null,
        [FromForm] string? orderDirection = null)
    {
        var page = (start / length) + 1;
        var pageSize = length;

        DateTime? startDt = null;
        DateTime? endDt = null;

        if (!string.IsNullOrWhiteSpace(startDate) && DateTime.TryParse(startDate, out var parsedStart))
        {
            startDt = parsedStart;
        }

        if (!string.IsNullOrWhiteSpace(endDate) && DateTime.TryParse(endDate, out var parsedEnd))
        {
            endDt = parsedEnd.AddDays(1).AddTicks(-1);
        }

        var (transactions, totalFiltered, totalRecords) = await _transactionService.GetTransactionsPagedAsync(
            page,
            pageSize,
            search,
            userId,
            productId,
            status,
            startDt,
            endDt,
            orderColumn,
            orderDirection);

        var transactionData = transactions.Select(t => new TransactionListViewModel.TransactionDataRow
        {
            Id = t.Id,
            ReferenceId = t.ReferenceId,
            CreatedAt = t.CreatedAt.ToString("dd MMM yyyy HH:mm"),
            Username = t.User?.UserName ?? "Unknown",
            ProductName = t.Product?.Name ?? "Unknown",
            Destination = t.Destination,
            SellPrice = t.SellPrice,
            Status = t.Status.ToString(),
            SerialNumber = t.Sn,
            SupplierName = t.Supplier?.Name
        }).ToList();

        return Json(new
        {
            draw = draw,
            recordsTotal = totalRecords,
            recordsFiltered = totalFiltered,
            data = transactionData
        });
    }

    #endregion
}
