using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PedagangPulsa.Application.Abstractions.Sms;
using PedagangPulsa.Domain.Configuration;

namespace PedagangPulsa.Infrastructure.Sms;

public class SmsGateClient : ISmsClient
{
    private readonly HttpClient _httpClient;
    private readonly SmsGateConfig _config;
    private readonly ILogger<SmsGateClient> _logger;

    public SmsGateClient(
        HttpClient httpClient,
        IOptions<SmsGateConfig> config,
        ILogger<SmsGateClient> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<SmsSendResult> SendAsync(SmsSendRequest request)
    {
        var payload = new
        {
            message = request.Message,
            phoneNumbers = new[] { request.PhoneNumber }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{_config.Username}:{_config.Password}"));

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_config.BaseUrl}/message");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        httpRequest.Content = content;

        try
        {
            _logger.LogInformation("Sending SMS to {PhoneNumber}", request.PhoneNumber);

            var response = await _httpClient.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseBody);
                var messageId = doc.RootElement.GetProperty("id").GetString();
                var state = doc.RootElement.GetProperty("state").GetString();

                _logger.LogInformation(
                    "SMS sent to {PhoneNumber}, messageId={MessageId}, state={State}",
                    request.PhoneNumber, messageId, state);

                return new SmsSendResult(true, MessageId: messageId);
            }

            _logger.LogError(
                "SMS-Gate API returned {StatusCode}: {Body}",
                (int)response.StatusCode, responseBody);

            return new SmsSendResult(false, Error: $"SMS_GATEWAY_ERROR");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "SMS-Gate connection failed");
            return new SmsSendResult(false, Error: "SMS_GATEWAY_UNAVAILABLE");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "SMS-Gate request timed out");
            return new SmsSendResult(false, Error: "SMS_GATEWAY_UNAVAILABLE");
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("SMS-Gate request was cancelled");
            return new SmsSendResult(false, Error: "SMS_GATEWAY_UNAVAILABLE");
        }
    }
}
