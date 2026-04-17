namespace PedagangPulsa.Domain.Entities;

public class NotificationLog
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid UserId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string? TemplateCode { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RefType { get; set; }
    public Guid? RefId { get; set; }
    public DateTime? SentAt { get; set; }
    public short RetryCount { get; set; }
    public string? ErrorMessage { get; set; }

    public User User { get; set; } = null!;
}
