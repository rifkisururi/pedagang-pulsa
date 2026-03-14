using PedagangPulsa.Domain.Enums;

namespace PedagangPulsa.Domain.Entities;

public class NotificationLog
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public NotificationChannel Channel { get; set; }
    public string? TemplateCode { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public string Status { get; set; } = "pending";
    public string? RefType { get; set; }
    public Guid? RefId { get; set; }
    public DateTime? SentAt { get; set; }
    public short RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User User { get; set; } = null!;
}
