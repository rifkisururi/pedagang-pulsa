using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Api.DTOs;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Infrastructure.Data;
using System.Security.Claims;

namespace PedagangPulsa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TopupController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<TopupController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public TopupController(
        AppDbContext context,
        ILogger<TopupController> logger,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
    }

    [HttpPost]
    public async Task<IActionResult> CreateTopupRequest([FromForm] CreateTopupRequestDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token",
                ErrorCode = "INVALID_TOKEN"
            });
        }

        var userGuid = Guid.Parse(userId);

        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Validation failed",
                Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
            });
        }

        if (request.TransferProof == null || request.TransferProof.Length == 0)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Transfer proof file is required",
                ErrorCode = "MISSING_FILE"
            });
        }

        // Validate file type
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
        var fileExtension = Path.GetExtension(request.TransferProof.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(fileExtension))
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Invalid file type. Only JPG, PNG, and PDF files are allowed",
                ErrorCode = "INVALID_FILE_TYPE"
            });
        }

        // Validate file size (max 5MB)
        const long maxFileSize = 5 * 1024 * 1024; // 5MB
        if (request.TransferProof.Length > maxFileSize)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "File size exceeds maximum limit of 5MB",
                ErrorCode = "FILE_TOO_LARGE"
            });
        }

        // Verify bank account exists
        var bankAccount = await _context.BankAccounts
            .FirstOrDefaultAsync(b => b.Id == request.BankAccountId);

        if (bankAccount == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "Bank account not found",
                ErrorCode = "BANK_ACCOUNT_NOT_FOUND"
            });
        }

        try
        {
            // Upload file
            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "topup");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.TransferProof.CopyToAsync(stream);
            }

            var fileUrl = $"/uploads/topup/{fileName}";

            // Create topup request
            var topupRequest = new TopupRequest
            {
                Id = Guid.NewGuid(),
                UserId = userGuid,
                BankAccountId = request.BankAccountId,
                Amount = request.Amount,
                TransferProofUrl = fileUrl,
                Status = TopupStatus.Pending,
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.TopupRequests.Add(topupRequest);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Topup request {TopupId} created for user {UserId}", topupRequest.Id, userGuid);

            var response = new TopupResponse
            {
                Success = true,
                Message = "Topup request submitted successfully. Please wait for admin approval.",
                TopupId = topupRequest.Id,
                Status = topupRequest.Status.ToString().ToLower(),
                Amount = topupRequest.Amount,
                CreatedAt = topupRequest.CreatedAt
            };

            return StatusCode(201, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating topup request for user {UserId}", userGuid);
            return StatusCode(500, new ErrorResponse
            {
                Message = "An error occurred while processing your request",
                ErrorCode = "TOPUP_ERROR"
            });
        }
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetTopupHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token",
                ErrorCode = "INVALID_TOKEN"
            });
        }

        var userGuid = Guid.Parse(userId);

        var topups = await _context.TopupRequests
            .Include(t => t.BankAccount)
            .Where(t => t.UserId == userGuid)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totalRecords = await _context.TopupRequests
            .Where(t => t.UserId == userGuid)
            .CountAsync();

        var topupItems = topups.Select(t => new TopupHistoryItem
        {
            Id = t.Id,
            Amount = t.Amount,
            Status = t.Status.ToString().ToLower(),
            TransferProofUrl = t.TransferProofUrl,
            BankName = t.BankAccount?.BankName,
            BankAccountNumber = t.BankAccount?.AccountNumber,
            RejectReason = t.RejectReason,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        }).ToList();

        return Ok(new TopupHistoryResponse
        {
            Success = true,
            Data = topupItems,
            TotalRecords = totalRecords,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpGet("banks")]
    public async Task<IActionResult> GetBankAccounts()
    {
        var bankAccounts = await _context.BankAccounts
            .OrderBy(b => b.BankName)
            .Select(b => new
            {
                id = b.Id,
                bankName = b.BankName,
                accountNumber = b.AccountNumber,
                accountName = b.AccountName,
                isActive = true
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            data = bankAccounts
        });
    }
}

public class CreateTopupRequestDto
{
    public int BankAccountId { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public IFormFile TransferProof { get; set; } = null!;
}
