using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PedagangPulsa.Application.Abstractions.Suppliers;

namespace PedagangPulsa.Infrastructure.Suppliers.Digiflazz;

public class DigiflazzAdapter : SupplierAdapterBase
{
    public override string SupplierName => "Digiflazz";
    public override string SupplierCode => "DIGIFLAZZ";

    private const string DefaultUsername = "demo"; // Replace with actual config

    public DigiflazzAdapter(ILogger<DigiflazzAdapter> logger, HttpClient httpClient)
        : base(logger, httpClient)
    {
    }

    public override async Task<SupplierPurchaseResult> PurchaseAsync(SupplierPurchaseRequest request)
    {
        try
        {
            _logger.LogInformation("Initiating purchase for {Destination} with product {ProductCode}",
                request.DestinationNumber, request.SupplierProductCode);

            // Build request payload for Digiflazz API
            var payload = new
            {
                username = request.SupplierUsername,
                buyer_sku_code = request.SupplierProductCode,
                customer_no = request.DestinationNumber,
                ref_id = request.ReferenceId.ToString(),
                sign = GenerateSignature(request.SupplierUsername, request.SupplierApiKey)
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            _httpClient.Timeout = TimeSpan.FromSeconds(request.TimeoutSeconds);

            var response = await _httpClient.PostAsync($"{request.SupplierApiUrl}/transaction", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Digiflazz response: {Response}", responseBody);

            var result = JsonSerializer.Deserialize<DigiflazzResponse>(responseBody);

            if (result == null)
            {
                return new SupplierPurchaseResult
                {
                    Success = false,
                    ErrorCode = "PARSE_ERROR",
                    Message = "Failed to parse supplier response"
                };
            }

            var purchaseResult = new SupplierPurchaseResult
            {
                Success = result.Data?.Status ?? false,
                ErrorCode = result.Data?.Status == true ? "" : "TRANSACTION_FAILED",
                Message = result.Message ?? GetMessageFromStatus(result.Data?.Status),
                SupplierTransactionId = result.Data?.Sn,
                SerialNumber = result.Data?.Sn,
                SupplierMessage = result.Message
            };

            _logger.LogInformation("Purchase result: {Success}, Message: {Message}",
                purchaseResult.Success, purchaseResult.Message);

            return purchaseResult;
        }
        catch (Exception ex)
        {
            return await HandleExceptionAsync(ex, "purchase");
        }
    }

    public override async Task<SupplierBalanceResult> CheckBalanceAsync(SupplierBalanceRequest request)
    {
        try
        {
            _logger.LogInformation("Checking balance for supplier {SupplierId}", request.SupplierId);

            // Build request payload for Digiflazz balance check API
            var payload = new
            {
                cmd = "deposit",
                username = request.SupplierUsername,
                sign = GenerateSignature(request.SupplierUsername, request.SupplierApiKey)
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{request.SupplierApiUrl}/cek-saldo", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<DigiflazzBalanceResponse>(responseBody);

            if (result?.Data == null)
            {
                return new SupplierBalanceResult
                {
                    Success = false,
                    Message = "Failed to parse balance response"
                };
            }

            return new SupplierBalanceResult
            {
                Success = true,
                Balance = result.Data.Deposit,
                Message = $"Balance: Rp {result.Data.Deposit:N0}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Balance check failed");
            return new SupplierBalanceResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public override async Task<SupplierPingResult> PingAsync()
    {
        var pingUrl = "https://api.digiflazz.com/v1"; // Default endpoint
        return await PingAsync(pingUrl);
    }

    private string GenerateSignature(string username, string apiKey)
    {
        // Digiflazz signature: md5(username + apiKey + "depo")
        var input = username + apiKey + "depo";
        using var md5 = MD5.Create();
        var inputBytes = Encoding.ASCII.GetBytes(input);
        var hashBytes = md5.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes).ToLower();
    }

    private string GetMessageFromStatus(bool? status)
    {
        return status switch
        {
            true => "Transaction successful",
            false => "Transaction failed",
            _ => "Unknown status"
        };
    }

    // Digiflazz response DTOs
    private class DigiflazzResponse
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public DigiflazzData? Data { get; set; }
    }

    private class DigiflazzData
    {
        public bool Status { get; set; }
        public string? Sn { get; set; }
        public string? Message { get; set; }
    }

    private class DigiflazzBalanceResponse
    {
        public DigiflazzBalanceData? Data { get; set; }
    }

    private class DigiflazzBalanceData
    {
        public decimal Deposit { get; set; }
    }
}
