namespace PedagangPulsa.Domain.Entities;

public class UserDevice
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FcmToken { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public string? Platform { get; set; }
    public string? AppVersion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastActiveAt { get; set; }
    public bool IsActive { get; set; } = true;

    public User User { get; set; } = null!;
}
