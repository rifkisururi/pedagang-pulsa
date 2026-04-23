namespace PedagangPulsa.Domain.Configuration;

public class SmsGateConfig
{
    public const string SectionName = "SmsGate";
    public string BaseUrl { get; set; } = "https://api.sms-gate.app/3rdparty/v1";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int OtpLength { get; set; } = 6;
    public int OtpExpirySeconds { get; set; } = 300;
    public int MaxOtpRequests { get; set; } = 3;
    public int RateLimitWindowSeconds { get; set; } = 900;
    public int MaxVerifyAttempts { get; set; } = 5;
}
