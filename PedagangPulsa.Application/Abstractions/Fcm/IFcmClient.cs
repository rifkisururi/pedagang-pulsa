namespace PedagangPulsa.Application.Abstractions.Fcm;

public record FcmNotificationPayload(
    string Title,
    string Body,
    Dictionary<string, string>? Data = null
);

public record FcmSendResult(
    bool Success,
    string Message,
    string? FcmMessageId = null
);

public interface IFcmClient
{
    Task<FcmSendResult> SendAsync(string fcmToken, FcmNotificationPayload payload, CancellationToken cancellationToken = default);
    Task<List<FcmSendResult>> SendMulticastAsync(List<string> fcmTokens, FcmNotificationPayload payload, CancellationToken cancellationToken = default);
}
