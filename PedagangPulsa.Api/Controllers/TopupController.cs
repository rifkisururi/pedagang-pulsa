using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Api.DTOs;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Infrastructure.Data;
using System.Data;
using System.Data.Common;
using System.Security.Claims;

namespace PedagangPulsa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TopupController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TopupService _topupService;
    private readonly ILogger<TopupController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public TopupController(
        AppDbContext context,
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
    public async Task<IActionResult> UploadTransferProof(Guid id, [FromForm] IFormFile transferProof)
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

        var connection = await OpenConnectionAsync();
        await using var command = CreateCommand(connection, """
            SELECT
                t."Id",
                t."Amount",
                t."UniqueCode",
                t."Status",
                t."TransferProofUrl",
                b."BankName",
                b."AccountNumber",
                b."AccountName",
                t."RejectReason",
                t."CreatedAt",
                t."UpdatedAt"
            FROM "TopupRequests" AS t
            LEFT JOIN "BankAccounts" AS b ON b."Id" = t."BankAccountId"
            WHERE t."Id" = @id AND t."UserId" = @userId
            """);
        AddParameter(command, "@id", id);
        AddParameter(command, "@userId", userGuid);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return NotFound(new ErrorResponse
            {
                Message = "Topup request not found",
                ErrorCode = "TOPUP_NOT_FOUND"
            });
        }

        var amount = reader.GetDecimal(reader.GetOrdinal("Amount"));
        var uniqueCode = reader.GetInt32(reader.GetOrdinal("UniqueCode"));

        return Ok(new
        {
            success = true,
            data = new
            {
                id = reader.GetGuid(reader.GetOrdinal("Id")),
                amount = amount,
                uniqueCode = uniqueCode,
                totalAmount = amount + uniqueCode,
                status = reader.GetString(reader.GetOrdinal("Status")).ToLowerInvariant(),
                transferProofUrl = reader["TransferProofUrl"] as string,
                bankName = reader["BankName"] as string,
                accountNumber = reader["AccountNumber"] as string,
                accountName = reader["AccountName"] as string,
                rejectReason = reader["RejectReason"] as string,
                createdAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                updatedAt = reader["UpdatedAt"] is DBNull ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
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

        var connection = await OpenConnectionAsync();
        var topupItems = new List<TopupHistoryItem>();

        await using (var historyCommand = CreateCommand(connection, """
            SELECT
                t."Id",
                t."Amount",
                t."UniqueCode",
                t."Status",
                t."TransferProofUrl",
                b."BankName",
                b."AccountNumber",
                t."RejectReason",
                t."CreatedAt",
                t."UpdatedAt"
            FROM "TopupRequests" AS t
            LEFT JOIN "BankAccounts" AS b ON b."Id" = t."BankAccountId"
            WHERE t."UserId" = @userId
            ORDER BY t."CreatedAt" DESC
            LIMIT @limit OFFSET @offset
            """))
        {
            AddParameter(historyCommand, "@userId", userGuid);
            AddParameter(historyCommand, "@limit", pageSize);
            AddParameter(historyCommand, "@offset", (page - 1) * pageSize);

            await using var historyReader = await historyCommand.ExecuteReaderAsync();
            while (await historyReader.ReadAsync())
            {
                var amount = historyReader.GetDecimal(historyReader.GetOrdinal("Amount"));
                var uniqueCode = historyReader.GetInt32(historyReader.GetOrdinal("UniqueCode"));

                topupItems.Add(new TopupHistoryItem
                {
                    Id = historyReader.GetGuid(historyReader.GetOrdinal("Id")),
                    Amount = amount,
                    UniqueCode = uniqueCode,
                    TotalAmount = amount + uniqueCode,
                    Status = historyReader.GetString(historyReader.GetOrdinal("Status")).ToLowerInvariant(),
                    TransferProofUrl = historyReader["TransferProofUrl"] as string,
                    BankName = historyReader["BankName"] as string,
                    BankAccountNumber = historyReader["AccountNumber"] as string,
                    RejectReason = historyReader["RejectReason"] as string,
                    CreatedAt = historyReader.GetDateTime(historyReader.GetOrdinal("CreatedAt")),
                    UpdatedAt = historyReader["UpdatedAt"] is DBNull ? null : historyReader.GetDateTime(historyReader.GetOrdinal("UpdatedAt"))
                });
            }
        }

        int totalRecords;
        await using (var countCommand = CreateCommand(connection, """
            SELECT COUNT(*)
            FROM "TopupRequests"
            WHERE "UserId" = @userId
            """))
        {
            AddParameter(countCommand, "@userId", userGuid);
            totalRecords = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
        }

        return Ok(new TopupHistoryResponse
        {
            Success = true,
            Data = topupItems,
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

    private async Task<DbConnection> OpenConnectionAsync()
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        return connection;
    }

    private static DbCommand CreateCommand(DbConnection connection, string commandText)
    {
        var command = connection.CreateCommand();
        command.CommandText = commandText;
        return command;
    }

    private static void AddParameter(DbCommand command, string parameterName, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = parameterName;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
