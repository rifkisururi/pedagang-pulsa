using System.ComponentModel.DataAnnotations;

namespace PedagangPulsa.Api.DTOs;

public class NotificationItem
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

public class NotificationListResponse
{
    public bool Success { get; set; }
    public List<NotificationItem> Data { get; set; } = new();
    public int UnreadCount { get; set; }
    public int TotalRecords { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class RegisterFcmTokenRequest
{
    [Required(ErrorMessage = "FCM token is required")]
    public string FcmToken { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? DeviceName { get; set; }

    [MaxLength(20)]
    public string? Platform { get; set; }

    [MaxLength(20)]
    public string? AppVersion { get; set; }
}

public class UnregisterFcmTokenRequest
{
    [Required(ErrorMessage = "FCM token is required")]
    public string FcmToken { get; set; } = string.Empty;
}

public class FcmTokenResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class TestNotificationRequest
{
    [MaxLength(200)]
    public string? Title { get; set; }

    [MaxLength(500)]
    public string? Body { get; set; }

    public Dictionary<string, string>? Data { get; set; }
}
