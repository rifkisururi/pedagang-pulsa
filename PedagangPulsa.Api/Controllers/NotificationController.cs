using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Api.DTOs;
using PedagangPulsa.Infrastructure.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PedagangPulsa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly AppDbContext _context;

    public NotificationController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] bool? isRead = null,
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

        var query = _context.NotificationLogs
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (isRead.HasValue)
        {
            query = query.Where(n => n.Status == (isRead.Value ? "read" : "pending"));
        }

        var totalRecords = await query.CountAsync();
        var unreadCount = await _context.NotificationLogs
            .AsNoTracking()
            .Where(n => n.UserId == userId && n.Status == "pending")
            .CountAsync();

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationItem
            {
                Id = CreateNotificationId(n.Id, n.CreatedAt),
                Type = n.TemplateCode ?? "general",
                Title = n.Subject ?? "Notification",
                Message = n.Body ?? string.Empty,
                IsRead = n.Status == "read",
                CreatedAt = n.CreatedAt,
                ReadAt = null
            })
            .ToListAsync();

        return Ok(new NotificationListResponse
        {
            Success = true,
            Data = notifications,
            UnreadCount = unreadCount,
            TotalRecords = totalRecords,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
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

        var notificationKeys = await _context.NotificationLogs
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .Select(n => new { n.Id, n.CreatedAt })
            .ToListAsync();

        var match = notificationKeys.FirstOrDefault(n => CreateNotificationId(n.Id, n.CreatedAt) == id);
        if (match == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "Notification not found",
                ErrorCode = "NOTIFICATION_NOT_FOUND"
            });
        }

        var notification = await _context.NotificationLogs
            .Where(n => n.UserId == userId && n.Id == match.Id && n.CreatedAt == match.CreatedAt)
            .FirstOrDefaultAsync();

        if (notification == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "Notification not found",
                ErrorCode = "NOTIFICATION_NOT_FOUND"
            });
        }

        notification.Status = "read";

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Notification marked as read"
        });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
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

        await _context.NotificationLogs
            .Where(n => n.UserId == userId && n.Status == "pending")
            .ExecuteUpdateAsync(n => n.SetProperty(notification => notification.Status, "read"));

        return Ok(new
        {
            success = true,
            message = "All notifications marked as read"
        });
    }

    // Keep a stable public identifier without exposing the composite database key.
    private static Guid CreateNotificationId(long id, DateTime createdAt)
    {
        var source = $"{id}:{createdAt.ToUniversalTime():O}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return new Guid(hash.AsSpan(0, 16));
    }
}
