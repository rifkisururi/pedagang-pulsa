using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PedagangPulsa.Application.Abstractions.Fcm;

namespace PedagangPulsa.Infrastructure.Fcm;

public class FcmClient : IFcmClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FcmClient> _logger;
    private readonly string _projectId;
    private readonly string _serviceAccountEmail;
    private readonly string _privateKey;

    private string? _cachedAccessToken;
    private DateTime _tokenExpiresAt;

    public FcmClient(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<FcmClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _projectId = configuration["Fcm:ProjectId"]
            ?? throw new InvalidOperationException("Fcm:ProjectId is not configured");
        _serviceAccountEmail = configuration["Fcm:ServiceAccountEmail"]
            ?? throw new InvalidOperationException("Fcm:ServiceAccountEmail is not configured");
        _privateKey = configuration["Fcm:PrivateKey"]
            ?? Environment.GetEnvironmentVariable("FCM_PRIVATE_KEY")
            ?? throw new InvalidOperationException("Fcm:PrivateKey is not configured");
    }

    public async Task<FcmSendResult> SendAsync(string fcmToken, FcmNotificationPayload payload, CancellationToken cancellationToken = default)
    {
        try
        {
            var accessToken = await GetAccessTokenAsync(cancellationToken);
            var client = _httpClientFactory.CreateClient("Fcm");

            var requestBody = new
            {
                message = new
                {
                    token = fcmToken,
                    notification = new { title = payload.Title, body = payload.Body },
                    data = payload.Data
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = content;

            var response = await client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("FCM send failed. Status: {Status}, Body: {Body}", response.StatusCode, responseBody);
                return new FcmSendResult(false, $"FCM API error: {response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var messageId = doc.RootElement.GetProperty("name").GetString();

            _logger.LogInformation("FCM message sent successfully: {MessageId}", messageId);
            return new FcmSendResult(true, "Message sent", messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending FCM message to token {TokenPrefix}...",
                fcmToken[..Math.Min(20, fcmToken.Length)]);
            return new FcmSendResult(false, ex.Message);
        }
    }

    public async Task<List<FcmSendResult>> SendMulticastAsync(List<string> fcmTokens, FcmNotificationPayload payload, CancellationToken cancellationToken = default)
    {
        var results = new List<FcmSendResult>();
        foreach (var token in fcmTokens)
        {
            results.Add(await SendAsync(token, payload, cancellationToken));
        }
        return results;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedAccessToken != null && DateTime.UtcNow < _tokenExpiresAt.AddSeconds(-60))
        {
            return _cachedAccessToken;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expiry = now + 3600;

        var header = JsonSerializer.Serialize(new { alg = "RS256", typ = "JWT" });
        var headerBytes = Base64UrlEncode(Encoding.UTF8.GetBytes(header));

        var payloadObj = new
        {
            iss = _serviceAccountEmail,
            scope = "https://www.googleapis.com/auth/firebase.messaging",
            aud = "https://oauth2.googleapis.com/token",
            iat = now,
            exp = expiry
        };
        var payloadJson = JsonSerializer.Serialize(payloadObj);
        var payloadBytes = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

        var stringToSign = $"{headerBytes}.{payloadBytes}";
        var signatureBytes = SignWithRsa(stringToSign, _privateKey);
        var signature = Base64UrlEncode(signatureBytes);

        var jwt = $"{headerBytes}.{payloadBytes}.{signature}";

        var client = _httpClientFactory.CreateClient("GoogleAuth");
        var tokenRequest = new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = jwt
        };

        var response = await client.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(tokenRequest),
            cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(responseBody);
        _cachedAccessToken = doc.RootElement.GetProperty("access_token").GetString()!;
        _tokenExpiresAt = DateTime.UtcNow.AddSeconds(
            doc.RootElement.GetProperty("expires_in").GetInt32());

        return _cachedAccessToken;
    }

    private static byte[] SignWithRsa(string input, string privateKeyPem)
    {
        var pemContents = privateKeyPem
            .Replace("-----BEGIN PRIVATE KEY-----", "")
            .Replace("-----END PRIVATE KEY-----", "")
            .Replace("\n", "")
            .Replace("\r", "");

        var keyBytes = Convert.FromBase64String(pemContents);
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(keyBytes, out _);
        return rsa.SignData(Encoding.UTF8.GetBytes(input), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
