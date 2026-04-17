using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PedagangPulsa.Application.Abstractions.Suppliers;

namespace PedagangPulsa.Infrastructure.Suppliers.VIPReseller;

public class VIPResellerAdapter : SupplierAdapterBase
{
    public override string SupplierName => "VIP Reseller";
    public override string SupplierCode => "VIPRESELLER";

    public VIPResellerAdapter(ILogger<VIPResellerAdapter> logger, HttpClient httpClient)
        : base(logger, httpClient)
    {
    }

    public override async Task<SupplierPurchaseResult> PurchaseAsync(SupplierPurchaseRequest request)
    {
        try
        {
            _logger.LogInformation("Initiating VIPReseller purchase for {Destination} with product {ProductCode}",
                request.DestinationNumber, request.SupplierProductCode);

            // VIPReseller API implementation
            var payload = new
            {
                key = request.SupplierApiKey,
                sign = GenerateSignature(request.SupplierUsername, request.SupplierApiKey),
                type = "order",
                service = request.SupplierProductCode,
                destination = request.DestinationNumber,
                ref_id = request.ReferenceId.ToString()
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            _httpClient.Timeout = TimeSpan.FromSeconds(request.TimeoutSeconds);

            var response = await _httpClient.PostAsync($"{request.SupplierApiUrl}/transaction", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("VIPReseller response: {Response}", responseBody);

            var result = JsonSerializer.Deserialize<VIPResellerResponse>(responseBody);

            if (result == null)
            {
                return new SupplierPurchaseResult
                {
                    Success = false,
                    ErrorCode = "PARSE_ERROR",
                    Message = "Failed to parse supplier response"
                };
            }

            return new SupplierPurchaseResult
            {
                Success = result.Status,
                ErrorCode = result.Status ? "" : result.Code ?? "TRANSACTION_FAILED",
                Message = result.Message ?? "Transaction completed",
                SupplierTransactionId = result.TrxId,
                SerialNumber = result.Sn,
                SupplierMessage = result.Message
            };
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
            _logger.LogInformation("Checking VIPReseller balance");

            var payload = new
            {
                key = request.SupplierApiKey,
                sign = GenerateSignature(request.SupplierUsername, request.SupplierApiKey),
                type = "balance"
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{request.SupplierApiUrl}/check-balance", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<VIPResellerBalanceResponse>(responseBody);

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
                Balance = result.Data.Balance,
                Message = $"Balance: Rp {result.Data.Balance:N0}"
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
        var pingUrl = "https://vip-reseller.co.id/api"; // Default endpoint
        return await PingAsync(pingUrl);
    }

    private string GenerateSignature(string username, string apiKey)
    {
        // VIPReseller signature: md5(apiKey + "vip")
        using var md5 = System.Security.Cryptography.MD5.Create();
        var inputBytes = Encoding.ASCII.GetBytes(apiKey + "vip");
        var hashBytes = md5.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes).ToLower();
    }

    // VIPReseller response DTOs
    private class VIPResellerResponse
    {
        public bool Status { get; set; }
        public string? Message { get; set; }
        public string? Code { get; set; }
        public string? TrxId { get; set; }
        public string? Sn { get; set; }
    }

    private class VIPResellerBalanceResponse
    {
        public VIPResellerBalanceData? Data { get; set; }
    }

    private class VIPResellerBalanceData
    {
        public decimal Balance { get; set; }
    }
}
