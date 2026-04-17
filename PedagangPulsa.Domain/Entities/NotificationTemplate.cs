namespace PedagangPulsa.Domain.Entities;

public class NotificationTemplate
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
