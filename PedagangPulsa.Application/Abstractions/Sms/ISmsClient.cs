namespace PedagangPulsa.Application.Abstractions.Sms;

public record SmsSendRequest(string Message, string PhoneNumber);
public record SmsSendResult(bool Success, string? MessageId = null, string? Error = null);

public interface ISmsClient
{
    Task<SmsSendResult> SendAsync(SmsSendRequest request);
}
