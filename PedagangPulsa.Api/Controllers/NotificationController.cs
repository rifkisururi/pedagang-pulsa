using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Api.DTOs;
using PedagangPulsa.Application.Services;
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
    private readonly FcmService _fcmService;

    public NotificationController(AppDbContext context, FcmService fcmService)
    {
        _context = context;
        _fcmService = fcmService;
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

    [HttpPost("fcm-token")]
    public async Task<IActionResult> RegisterFcmToken([FromBody] RegisterFcmTokenRequest request)
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

        try
        {
            await _fcmService.RegisterOrUpdateTokenAsync(
                userId, request.FcmToken, request.DeviceName, request.Platform, request.AppVersion);

            return Ok(new FcmTokenResponse
            {
                Success = true,
                Message = "FCM token registered successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponse
            {
                Message = "Failed to register FCM token",
                ErrorCode = "FCM_REGISTRATION_FAILED",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    [HttpDelete("fcm-token")]
    public async Task<IActionResult> UnregisterFcmToken([FromBody] UnregisterFcmTokenRequest request)
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

        var removed = await _fcmService.UnregisterTokenAsync(userId, request.FcmToken);

        if (!removed)
        {
            return NotFound(new ErrorResponse
            {
                Message = "FCM token not found or already unregistered",
                ErrorCode = "FCM_TOKEN_NOT_FOUND"
            });
        }

        return Ok(new FcmTokenResponse
        {
            Success = true,
            Message = "FCM token unregistered successfully"
        });
    }

    [HttpPost("test")]
    public async Task<IActionResult> TestNotification([FromBody] TestNotificationRequest request)
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

        var title = !string.IsNullOrWhiteSpace(request.Title) ? request.Title : "Test Notification";
        var body = !string.IsNullOrWhiteSpace(request.Body) ? request.Body : "Ini adalah notifikasi test dari PedagangPulsa.";

        var result = await _fcmService.SendToUserAsync(userId, title, body, request.Data);

        if (!result.Success)
        {
            return StatusCode(500, new ErrorResponse
            {
                Message = result.Message,
                ErrorCode = "FCM_SEND_FAILED"
            });
        }

        return Ok(new
        {
            success = true,
            message = result.Message,
            fcmMessageId = result.FcmMessageId
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
