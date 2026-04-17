using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Api.DTOs;
using PedagangPulsa.Application.Abstractions.Persistence;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Enums;
using System.Security.Claims;

namespace PedagangPulsa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TopupController : ControllerBase
{
    private readonly IAppDbContext _context;
    private readonly TopupService _topupService;
    private readonly ILogger<TopupController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public TopupController(
        IAppDbContext context,
        TopupService topupService,
        ILogger<TopupController> logger,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _context = context;
        _topupService = topupService;
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
    }

    /// <summary>
    /// Step 1: Request topup - mendapatkan detail pembayaran dengan unique code
    /// </summary>
    [HttpPost("request")]
    public async Task<IActionResult> RequestTopup([FromBody] CreateTopupRequestDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var userGuid))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token",
                ErrorCode = "INVALID_TOKEN"
            });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Validation failed",
                Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
            });
        }

        // Verify bank account exists
        var bankAccount = await _context.BankAccounts
            .FirstOrDefaultAsync(b => b.Id == request.BankAccountId && b.IsActive);

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
            // Create topup request with unique code
            var topupRequest = await _topupService.CreateTopupRequestAsync(
                userGuid,
                request.BankAccountId,
                request.Amount);

            if (topupRequest == null)
            {
                return BadRequest(new ErrorResponse
                {
                    Message = "Failed to create topup request",
                    ErrorCode = "CREATE_TOPUP_FAILED"
                });
            }

            // Calculate expiry time (24 hours from now)
            var expiresAt = topupRequest.CreatedAt.AddHours(24);

            _logger.LogInformation("Topup request {TopupId} created for user {UserId} with unique code {UniqueCode}",
                topupRequest.Id, userGuid, topupRequest.UniqueCode);

            var response = new TopupRequestResponse
            {
                Success = true,
                Message = "Topup request created. Please transfer the specified amount.",
                TopupId = topupRequest.Id,
                Status = TopupStatus.Pending.ToString().ToLowerInvariant(),
                Payment = new PaymentDetail
                {
                    BankName = bankAccount.BankName,
                    AccountNumber = bankAccount.AccountNumber,
                    AccountName = bankAccount.AccountName,
                    OriginalAmount = topupRequest.Amount,
                    UniqueCode = topupRequest.UniqueCode,
                    TotalAmount = topupRequest.Amount + topupRequest.UniqueCode,
                    ExpiresAt = expiresAt
                },
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

    /// <summary>
    /// Step 2: Upload bukti transfer untuk topup request yang sudah dibuat
    /// </summary>
    [HttpPost("{id:guid}/proof")]
    public async Task<IActionResult> UploadTransferProof(Guid id, IFormFile transferProof)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var userGuid))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token",
                ErrorCode = "INVALID_TOKEN"
            });
        }

        // Validate file
        if (transferProof == null || transferProof.Length == 0)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Transfer proof file is required",
                ErrorCode = "MISSING_FILE"
            });
        }

        // Validate file type
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
        var fileExtension = Path.GetExtension(transferProof.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(fileExtension))
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Invalid file type. Only JPG, PNG, and PDF files are allowed",
                ErrorCode = "INVALID_FILE_TYPE"
            });
        }

        // Validate file size (max 5MB)
        const long maxFileSize = 5 * 1024 * 1024;
        if (transferProof.Length > maxFileSize)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "File size exceeds maximum limit of 5MB",
                ErrorCode = "FILE_TOO_LARGE"
            });
        }

        // Verify topup request exists and belongs to user
        var topupRequest = await _context.TopupRequests
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userGuid);

        if (topupRequest == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "Topup request not found",
                ErrorCode = "TOPUP_NOT_FOUND"
            });
        }

        if (topupRequest.Status != TopupStatus.Pending)
        {
            return BadRequest(new ErrorResponse
            {
                Message = $"Cannot upload proof for topup with status {topupRequest.Status}",
                ErrorCode = "INVALID_TOPUP_STATUS"
            });
        }

        // Check if already has proof
        if (!string.IsNullOrEmpty(topupRequest.TransferProofUrl))
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Transfer proof already uploaded",
                ErrorCode = "PROOF_ALREADY_UPLOADED"
            });
        }

        try
        {
            // Upload file
            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var webRootPath = string.IsNullOrWhiteSpace(_environment.WebRootPath)
                ? Path.Combine(_environment.ContentRootPath, "wwwroot")
                : _environment.WebRootPath;
            var uploadsFolder = Path.Combine(webRootPath, "uploads", "topup");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await transferProof.CopyToAsync(stream);
            }

            var fileUrl = $"/uploads/topup/{fileName}";

            // Update topup request
            await _topupService.UploadTransferProofAsync(id, fileUrl);

            _logger.LogInformation("Transfer proof uploaded for topup {TopupId} by user {UserId}", id, userGuid);

            return Ok(new TransferProofResponse
            {
                Success = true,
                Message = "Transfer proof uploaded successfully. Waiting for admin approval.",
                TopupId = id,
                Status = TopupStatus.Pending.ToString().ToLowerInvariant(),
                UploadedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading transfer proof for topup {TopupId}", id);
            return StatusCode(500, new ErrorResponse
            {
                Message = "An error occurred while uploading transfer proof",
                ErrorCode = "UPLOAD_ERROR"
            });
        }
    }

    /// <summary>
    /// Get topup request detail by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTopupDetail(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var userGuid))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token",
                ErrorCode = "INVALID_TOKEN"
            });
        }

        var topupRequest = await _topupService.GetUserTopupRequestAsync(id, userGuid);
        if (topupRequest == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "Topup request not found",
                ErrorCode = "TOPUP_NOT_FOUND"
            });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                id = topupRequest.Id,
                amount = topupRequest.Amount,
                uniqueCode = topupRequest.UniqueCode,
                totalAmount = topupRequest.Amount + topupRequest.UniqueCode,
                status = topupRequest.Status.ToString().ToLowerInvariant(),
                transferProofUrl = topupRequest.TransferProofUrl,
                bankName = topupRequest.BankAccount?.BankName,
                accountNumber = topupRequest.BankAccount?.AccountNumber,
                accountName = topupRequest.BankAccount?.AccountName,
                rejectReason = topupRequest.RejectReason,
                createdAt = topupRequest.CreatedAt,
                updatedAt = topupRequest.UpdatedAt
            }
        });
    }

    /// <summary>
    /// Get topup history with pagination
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetTopupHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var userGuid))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token",
                ErrorCode = "INVALID_TOKEN"
            });
        }

        var (topups, totalRecords) = await _topupService.GetUserTopupHistoryAsync(userGuid, page, pageSize);

        return Ok(new TopupHistoryResponse
        {
            Success = true,
            Data = topups.Select(t => new TopupHistoryItem
            {
                Id = t.Id,
                Amount = t.Amount,
                UniqueCode = t.UniqueCode,
                TotalAmount = t.Amount + t.UniqueCode,
                Status = t.Status.ToString().ToLowerInvariant(),
                TransferProofUrl = t.TransferProofUrl,
                BankName = t.BankAccount?.BankName,
                BankAccountNumber = t.BankAccount?.AccountNumber,
                RejectReason = t.RejectReason,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            }).ToList(),
            TotalRecords = totalRecords,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// Get list of active bank accounts
    /// </summary>
    [HttpGet("banks")]
    public async Task<IActionResult> GetBankAccounts()
    {
        var bankAccounts = await _context.BankAccounts
            .Where(b => b.IsActive)
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
