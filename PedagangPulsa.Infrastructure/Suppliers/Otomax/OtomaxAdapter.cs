using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PedagangPulsa.Application.Abstractions.Suppliers;

namespace PedagangPulsa.Infrastructure.Suppliers.Otomax;

/// <summary>
/// Adapter for Otomax-based supplier API (e.g. DFlash, Khazanah).
///
/// Sign generation: SHA-1(template) → Base64 → URL-safe (no padding)
///   TRX template:   OtomaX|{memberId}|{product}|{dest}|{refID}|{pin}|{password}
///   Balance template: OtomaX|{memberId}||||{pin}|{password}
///
/// Response format: plain text
///   Sample failed:  "PLPP10 ke 14530083964 R#260426120138 PLPP10.14530083964 tdk diproses. Stok tidak cukup. Sisa stok 3.986. Hrg:11.755. #*TRANSAKSI LANCAR.."
///   Sample success: "PLPP10 ke 14530083964 R#260426120138 SN:1234567890 BERHASIL. Hrg:11.755. #*TRANSAKSI LANCAR.."
///
/// Field mapping from SupplierPurchaseRequest:
///   memberId  = SupplierApiKey  (= Supplier.MemberId)
///   pin      = Pin             (= Supplier.Pin)
///   password = SupplierApiSecret (= Supplier.Password)
/// </summary>
public class OtomaxAdapter : SupplierAdapterBase
{
    public override string SupplierName => "Otomax";
    public override string SupplierCode => "OTOMAX";

    private static readonly string[] SuccessKeywords = ["berhasil", "sukses", "ok ", "terkirim"];
    private static readonly string[] QueuedKeywords = ["akan segera di proses", "sedang di proses", "queued", "antri", "proses"];
    private static readonly string[] FailKeywords = ["tdk diproses", "gagal", "error", "stok tidak cukup", "saldo tidak cukup", "timeout", "sdh pernah", "tujuan salah", "nomor salah", "nomor tidak valid", "tidak valid"];

    public OtomaxAdapter(ILogger<OtomaxAdapter> logger, HttpClient httpClient)
        : base(logger, httpClient)
    {
    }

    public override async Task<SupplierPurchaseResult> PurchaseAsync(SupplierPurchaseRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.SupplierApiKey))
                return Error("INVALID_CONFIG", "MemberId (SupplierApiKey) is required");

            if (string.IsNullOrEmpty(request.Pin))
                return Error("INVALID_CONFIG", "PIN is required");

            if (string.IsNullOrEmpty(request.SupplierApiSecret))
                return Error("INVALID_CONFIG", "Password (SupplierApiSecret) is required");

            var memberId = request.SupplierApiKey;
            var pin = request.Pin;
            var password = request.SupplierApiSecret;
            var product = request.SupplierProductCode;
            var dest = request.DestinationNumber;
            var refId = request.ReferenceId;

            // Template: OtomaX|{memberId}|{product}|{dest}|{refID}|{pin}|{password}
            var template = $"OtomaX|{memberId}|{product}|{dest}|{refId}|{pin}|{password}";
            var sign = GenerateSign(template);

            var baseUrl = request.SupplierApiUrl.TrimEnd('/') + "/";
            var url = $"{baseUrl}trx?memberID={memberId}&product={product}&dest={dest}&refID={refId}&sign={sign}";

            _logger.LogInformation("Otomax TRX URL: {Url}", url);

            _httpClient.Timeout = TimeSpan.FromSeconds(request.TimeoutSeconds);
            var response = await _httpClient.GetAsync(url);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Otomax TRX Response: {Response}", responseBody);

            return ParseTrxResponse(responseBody);
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
            var memberId = request.SupplierApiKey;    // Supplier.MemberId
            var pin = request.Pin;                    // Supplier.Pin
            var password = request.SupplierApiSecret; // Supplier.Password

            if (string.IsNullOrEmpty(memberId) || string.IsNullOrEmpty(password))
                return new SupplierBalanceResult
                {
                    Success = false,
                    Message = "MemberId and Password are required for Otomax balance check"
                };

            // Template: OtomaX|{memberId}||||{pin}|{password}
            var template = $"OtomaX|{memberId}||||{pin}|{password}";
            var sign = GenerateSign(template);

            var baseUrl = request.SupplierApiUrl.TrimEnd('/') + "/";
            var url = $"{baseUrl}balance?memberID={memberId}&sign={sign}";

            _logger.LogInformation("Otomax Balance URL: {Url}", url);

            var response = await _httpClient.GetAsync(url);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Otomax Balance Response: {Response}", responseBody);

            return ParseBalanceResponse(responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Otomax balance check failed");
            return new SupplierBalanceResult
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    public override async Task<SupplierPingResult> PingAsync()
    {
        return await PingAsync("http://api.dflash.co.id/");
    }

    #region Sign Generation

    /// <summary>
    /// Generate Otomax sign identically to the JavaScript reference implementation:
    ///   SHA-1(template bytes) → Base64 → URL-safe (remove trailing '=', '+' → '-', '/' → '_')
    /// </summary>
    private string GenerateSign(string template)
    {
        var templateBytes = Encoding.UTF8.GetBytes(template);

        using var sha1 = SHA1.Create();
        var hashBytes = sha1.ComputeHash(templateBytes);

        // Base64 encode the raw SHA-1 bytes
        var base64 = Convert.ToBase64String(hashBytes);

        // Convert to URL-safe Base64
        var sign = base64
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return sign;
    }

    #endregion

    #region Response Parsing (Plain Text)

    /// <summary>
    /// Parse Otomax TRX response. Can be in two formats:
    ///
    /// 1) Plain text (multi-line possible):
    ///   "PLPPP\nPLPPP20 ke 86072219131 BERHASIL @20/04 05:46 SN: 2759-7985-4678-4322-6953/BURHAN/R1/450VA/43.9. Stok: ... R#99290257 #*TRANSAKSI LANCARRR...."
    ///
    /// 2) URL-encoded query string:
    ///   "status=52&message=R#REFW90DD5DK0 CPLN.081312111781 sdh pernah pada 17:16, Status Tujuan Salah. SN: . Stok: ... #*TRANSAKSI LANCAR.."
    /// </summary>
    private SupplierPurchaseResult ParseTrxResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return Error("EMPTY_RESPONSE", "Empty response from Otomax");

        // Detect query string format (starts with "status=")
        if (response.TrimStart().StartsWith("status=", StringComparison.OrdinalIgnoreCase))
            return ParseQuerystringResponse(response);

        // Otherwise parse as plain text
        return ParsePlainTextResponse(response);
    }

    /// <summary>
    /// Parse query string format: "status=52&message=R#... sdh pernah ... SN: . Stok: ..."
    /// </summary>
    private SupplierPurchaseResult ParseQuerystringResponse(string response)
    {
        var queryParams = System.Web.HttpUtility.ParseQueryString(response);
        var status = queryParams["status"] ?? "";
        var message = queryParams["message"] ?? response;
        var messageLower = message.ToLowerInvariant();

        // SN: empty or with just "." means no serial number
        var snMatch = Regex.Match(message, @"SN[:.]?\s*(.+?)(?:\s+Stok:|\s+R#|\s+#\*|$)", RegexOptions.IgnoreCase);
        var sn = snMatch.Success
            ? snMatch.Groups[1].Value.Trim().TrimEnd('.', ',', ' ')
            : null;

        // Treat empty/dot-only SN as null
        if (string.IsNullOrWhiteSpace(sn) || sn == ".")
            sn = null;

        // Determine success based on status code and message content
        bool isSuccess;
        bool isQueued;
        string errorCode;

        if (!string.IsNullOrEmpty(sn))
        {
            isSuccess = true;
            isQueued = false;
            errorCode = "";
        }
        else if (FailKeywords.Any(k => messageLower.Contains(k)))
        {
            isSuccess = false;
            isQueued = false;
            errorCode = status;
        }
        else if (QueuedKeywords.Any(k => messageLower.Contains(k)))
        {
            isSuccess = true;
            isQueued = true;
            errorCode = "";
        }
        else if (SuccessKeywords.Any(k => messageLower.Contains(k)))
        {
            isSuccess = true;
            isQueued = false;
            errorCode = "";
        }
        else
        {
            // Non-zero status = failed
            isSuccess = status == "00" || status == "0";
            isQueued = false;
            errorCode = isSuccess ? "" : status;
        }

        return new SupplierPurchaseResult
        {
            Success = isSuccess,
            IsQueued = isQueued,
            ErrorCode = errorCode,
            Message = message,
            SerialNumber = sn,
            SupplierMessage = message,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Parse plain text TRX response.
    /// </summary>
    private SupplierPurchaseResult ParsePlainTextResponse(string response)
    {
        var responseLower = response.ToLowerInvariant();

        // Extract SN if present.
        // SN format varies:
        //   "SN: 2759-7985-4678-4322-6953/BURHAN/R1/450VA/43.9."  (PLN, complex)
        //   "SN:1234567890"                                         (pulsa, digits only)
        // SN ends before "Stok:" or "R#" or "#*" footer
        var snMatch = Regex.Match(response, @"SN[:.]?\s*(.+?)(?:\s+Stok:|\s+R#|\s+#\*)", RegexOptions.IgnoreCase);
        var sn = snMatch.Success
            ? snMatch.Groups[1].Value.Trim().TrimEnd('.', ',')
            : null;

        // If regex didn't match but SN: exists, try simpler extraction
        if (string.IsNullOrEmpty(sn))
        {
            var snSimpleMatch = Regex.Match(response, @"SN[:.]?\s*(.+?)(?:\s+\.?\s*R#|\s+#\*|$)", RegexOptions.IgnoreCase);
            sn = snSimpleMatch.Success
                ? snSimpleMatch.Groups[1].Value.Trim().TrimEnd('.', ',', ' ')
                : null;
        }

        // Treat empty/dot-only SN as null
        if (string.IsNullOrWhiteSpace(sn) || sn == ".")
            sn = null;

        // Determine success: if SN exists → success. Otherwise check keywords.
        bool isSuccess;
        bool isQueued;

        if (!string.IsNullOrEmpty(sn))
        {
            isSuccess = true;
            isQueued = false;
        }
        else if (FailKeywords.Any(k => responseLower.Contains(k)))
        {
            isSuccess = false;
            isQueued = false;
        }
        else if (QueuedKeywords.Any(k => responseLower.Contains(k)))
        {
            isSuccess = true;
            isQueued = true;
        }
        else if (SuccessKeywords.Any(k => responseLower.Contains(k)))
        {
            isSuccess = true;
            isQueued = false;
        }
        else
        {
            isSuccess = false;
            isQueued = false;
        }

        return new SupplierPurchaseResult
        {
            Success = isSuccess,
            IsQueued = isQueued,
            ErrorCode = isSuccess ? "" : "TRANSACTION_FAILED",
            Message = response,
            SerialNumber = sn,
            SupplierMessage = response,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Parse Otomax plain text balance response.
    ///
    /// Sample: "SALDO: 1.234.567,00"
    /// </summary>
    private SupplierBalanceResult ParseBalanceResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return new SupplierBalanceResult
            {
                Success = false,
                Message = "Empty response from Otomax",
                Timestamp = DateTime.UtcNow
            };

        var responseLower = response.ToLowerInvariant();

        // Extract balance amount (format: SALDO: 1.234.567 or similar)
        var balanceMatch = Regex.Match(response, @"(?:saldo|balance)[:\s]+([\d.,]+)", RegexOptions.IgnoreCase);
        decimal balance = 0;

        if (balanceMatch.Success)
        {
            // Remove dots (thousand separator in Indonesian format) and parse
            var balanceStr = balanceMatch.Groups[1].Value.Replace(".", "").Replace(",", ".");
            decimal.TryParse(balanceStr, out balance);
        }

        bool isSuccess = balanceMatch.Success;

        return new SupplierBalanceResult
        {
            Success = isSuccess,
            Balance = balance,
            Message = isSuccess ? $"Balance: Rp {balance:N0}" : response,
            Timestamp = DateTime.UtcNow
        };
    }

    #endregion

    #region Helpers

    private static SupplierPurchaseResult Error(string code, string message)
    {
        return new SupplierPurchaseResult
        {
            Success = false,
            ErrorCode = code,
            Message = message,
            Timestamp = DateTime.UtcNow
        };
    }

    #endregion
}
