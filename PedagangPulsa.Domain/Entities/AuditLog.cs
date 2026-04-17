namespace PedagangPulsa.Domain.Entities;

public class AuditLog
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public Guid ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Entity { get; set; }
    public string? EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
