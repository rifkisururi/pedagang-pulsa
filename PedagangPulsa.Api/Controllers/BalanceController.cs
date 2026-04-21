using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Api.DTOs;
using PedagangPulsa.Infrastructure.Data;
using System.Security.Claims;

namespace PedagangPulsa.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class BalanceController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<BalanceController> _logger;

    public BalanceController(AppDbContext context, ILogger<BalanceController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetBalance()
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

        var user = await _context.Users
            .Include(u => u.Balance)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "User not found",
                ErrorCode = "USER_NOT_FOUND"
            });
        }

        return Ok(new BalanceResponse
        {
            Success = true,
            ActiveBalance = user.Balance?.ActiveBalance ?? 0,
            HeldBalance = user.Balance?.HeldBalance ?? 0,
            TotalBalance = (user.Balance?.ActiveBalance ?? 0) + (user.Balance?.HeldBalance ?? 0)
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
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

        var ledgers = await _context.BalanceLedgers
            .Where(bl => bl.UserId == userId)
            .OrderByDescending(bl => bl.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totalRecords = await _context.BalanceLedgers
            .Where(bl => bl.UserId == userId)
            .CountAsync();

        var ledgerItems = ledgers.Select(bl => new BalanceHistoryItem
        {
            Id = Guid.NewGuid(),
            CreatedAt = bl.CreatedAt,
            Type = bl.Type.ToString(),
            Amount = bl.Amount,
            ActiveBefore = bl.ActiveBefore,
            ActiveAfter = bl.ActiveAfter,
            HeldBefore = bl.HeldBefore,
            HeldAfter = bl.HeldAfter,
            Description = bl.Notes
        }).ToList();

        return Ok(new BalanceHistoryResponse
        {
            Success = true,
            Data = ledgerItems,
            TotalRecords = totalRecords,
            Page = page,
            PageSize = pageSize
        });
    }
}
