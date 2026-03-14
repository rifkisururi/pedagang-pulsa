using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Api.DTOs;
using PedagangPulsa.Infrastructure.Data;
using System.Security.Claims;

namespace PedagangPulsa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(AppDbContext context, ILogger<NotificationController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] bool? isRead = null,
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

        var query = _context.NotificationLogs
            .Where(n => n.UserId == userGuid);

        if (isRead.HasValue)
        {
            query = query.Where(n => n.Status == (isRead.Value ? "read" : "pending"));
        }

        var totalRecords = await query.CountAsync();
        var unreadCount = await _context.NotificationLogs
            .Where(n => n.UserId == userGuid && n.Status == "pending")
            .CountAsync();

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationItem
            {
                Id = Guid.NewGuid(),
                Type = n.TemplateCode,
                Title = n.Subject ?? "Notification",
                Message = n.Body,
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

        var notification = await _context.NotificationLogs
            .Where(n => n.TemplateCode == id.ToString() && n.UserId == userGuid)
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

        await _context.NotificationLogs
            .Where(n => n.UserId == userGuid && n.Status == "pending")
            .ExecuteUpdateAsync(n => n.SetProperty(n => n.Status, "read"));

        return Ok(new
        {
            success = true,
            message = "All notifications marked as read"
        });
    }
}
