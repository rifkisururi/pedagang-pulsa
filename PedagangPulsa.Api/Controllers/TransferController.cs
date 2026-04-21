using Asp.Versioning;
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
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class TransferController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<TransferController> _logger;

    public TransferController(AppDbContext context, ILogger<TransferController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Transfer([FromBody] TransferRequestDto request)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token",
                ErrorCode = "INVALID_TOKEN"
            });
        }

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token format",
                ErrorCode = "INVALID_TOKEN_FORMAT"
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

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Get from user with balance and level
            var fromUser = await _context.Users
                .Include(u => u.Balance)
                .Include(u => u.Level)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (fromUser == null || fromUser.Balance == null)
            {
                return NotFound(new ErrorResponse
                {
                    Message = "User not found",
                    ErrorCode = "USER_NOT_FOUND"
                });
            }

            // Check if user can transfer (check level config)
            var canTransferConfig = await _context.UserLevelConfigs
                .Where(lc => lc.LevelId == fromUser.LevelId && lc.ConfigKey == "can_transfer")
                .Select(lc => lc.ConfigValue)
                .FirstOrDefaultAsync();

            var canTransfer = canTransferConfig?.ToLower() == "true";
            if (!canTransfer)
            {
                return Forbid();
            }

            // Get to user
            var toUser = await _context.Users
                .Include(u => u.Balance)
                .FirstOrDefaultAsync(u => u.UserName == request.ToUsername);

            if (toUser == null)
            {
                return NotFound(new ErrorResponse
                {
                    Message = "Recipient user not found",
                    ErrorCode = "RECIPIENT_NOT_FOUND"
                });
            }

            if (toUser.Id == fromUser.Id)
            {
                return BadRequest(new ErrorResponse
                {
                    Message = "Cannot transfer to yourself",
                    ErrorCode = "INVALID_TRANSFER"
                });
            }

            if (request.Amount <= 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Message = "Amount must be greater than zero",
                    ErrorCode = "INVALID_AMOUNT"
                });
            }

            // Lock both balances
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT * FROM \"UserBalances\" WHERE \"UserId\" IN ({0}, {1}) FOR UPDATE",
                fromUser.Id, toUser.Id);

            // Check balance
            if (fromUser.Balance.ActiveBalance < request.Amount)
            {
                return BadRequest(new ErrorResponse
                {
                    Message = "Insufficient balance",
                    ErrorCode = "INSUFFICIENT_BALANCE"
                });
            }

            var fromBalanceBefore = fromUser.Balance.ActiveBalance;
            var toBalanceBefore = toUser.Balance!.ActiveBalance;

            // Perform transfer
            fromUser.Balance.ActiveBalance -= request.Amount;
            toUser.Balance.ActiveBalance += request.Amount;

            // Create ledgers
            var fromLedger = new BalanceLedger
            {
                Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                UserId = fromUser.Id,
                Type = BalanceTransactionType.TransferOut,
                Amount = -request.Amount,
                ActiveBefore = fromBalanceBefore,
                ActiveAfter = fromUser.Balance.ActiveBalance,
                HeldBefore = fromUser.Balance.HeldBalance,
                HeldAfter = fromUser.Balance.HeldBalance,
                RefType = "PeerTransfer",
                Notes = $"Transfer to {toUser.UserName}",
                CreatedAt = DateTime.UtcNow
            };

            var toLedger = new BalanceLedger
            {
                Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 1,
                UserId = toUser.Id,
                Type = BalanceTransactionType.TransferIn,
                Amount = request.Amount,
                ActiveBefore = toBalanceBefore,
                ActiveAfter = toUser.Balance.ActiveBalance,
                HeldBefore = toUser.Balance.HeldBalance,
                HeldAfter = toUser.Balance.HeldBalance,
                RefType = "PeerTransfer",
                Notes = $"Transfer from {fromUser.UserName}",
                CreatedAt = DateTime.UtcNow
            };

            _context.BalanceLedgers.Add(fromLedger);
            _context.BalanceLedgers.Add(toLedger);

            // Create transfer record
            var transfer = new PeerTransfer
            {
                FromUserId = fromUser.Id,
                ToUserId = toUser.Id,
                Amount = request.Amount,
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow
            };

            _context.PeerTransfers.Add(transfer);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var response = new
            {
                success = true,
                message = "Transfer successful",
                data = new
                {
                    transferId = transfer.Id,
                    from = fromUser.UserName,
                    to = toUser.UserName,
                    amount = request.Amount,
                    notes = request.Notes,
                    createdAt = transfer.CreatedAt
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error processing transfer for user {UserId}", userId);
            return StatusCode(500, new ErrorResponse
            {
                Message = "An error occurred while processing your transfer",
                ErrorCode = "TRANSFER_ERROR"
            });
        }
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetTransferHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token",
                ErrorCode = "INVALID_TOKEN"
            });
        }

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token format",
                ErrorCode = "INVALID_TOKEN_FORMAT"
            });
        }

        var transfers = await _context.PeerTransfers
            .Where(pt => pt.FromUserId == userId || pt.ToUserId == userId)
            .OrderByDescending(pt => pt.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totalRecords = await _context.PeerTransfers
            .Where(pt => pt.FromUserId == userId || pt.ToUserId == userId)
            .CountAsync();

        var transferData = transfers.Select(t => new
        {
            id = t.Id,
            from = _context.Users.Where(u => u.Id == t.FromUserId).Select(u => u.UserName).FirstOrDefault(),
            to = _context.Users.Where(u => u.Id == t.ToUserId).Select(u => u.UserName).FirstOrDefault(),
            amount = t.Amount,
            notes = t.Notes,
            status = "Success",
            direction = t.FromUserId == userId ? "out" : "in",
            createdAt = t.CreatedAt
        }).ToList();

        return Ok(new
        {
            success = true,
            data = transferData,
            totalRecords,
            page,
            pageSize
        });
    }
}

public class TransferRequestDto
{
    public string ToUsername { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}
