using PedagangPulsa.Domain.Enums;

namespace PedagangPulsa.Domain.Entities;

public class NotificationTemplate
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
