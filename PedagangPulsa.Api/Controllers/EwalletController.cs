using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedagangPulsa.Api.DTOs;

namespace PedagangPulsa.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ewallet")]
[Authorize]
public class EwalletController : ControllerBase
{
    private static readonly HashSet<string> SupportedProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "dana", "ovo", "gopay", "shopeepay", "linkaja", "doku"
    };

    private static readonly Dictionary<string, Dictionary<string, string>> MockDatabase = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dana"] = new()
        {
            ["081234567890"] = "Budi Santoso",
            ["085612345678"] = "Siti Aminah",
            ["087898765432"] = "Rina Wati",
        },
        ["ovo"] = new()
        {
            ["081234567891"] = "Ahmad Rifki",
            ["085612345679"] = "Dewi Lestari",
            ["087898765433"] = "Joko Widodo",
        },
        ["gopay"] = new()
        {
            ["081234567892"] = "Rudi Hartono",
            ["085612345680"] = "Mega Puspita",
            ["087898765434"] = "Bambang Suryanto",
        },
        ["shopeepay"] = new()
        {
            ["081234567893"] = "Ani Sulistyowati",
            ["085612345681"] = "Doni Firmansyah",
            ["087898765435"] = "Lina Marlina",
        },
        ["linkaja"] = new()
        {
            ["081234567894"] = "Hendra Gunawan",
            ["085612345682"] = "Yuni Astuti",
            ["087898765436"] = "Wahyu Nugroho",
        },
        ["doku"] = new()
        {
            ["081234567895"] = "Fitri Handayani",
            ["085612345683"] = "Agus Prasetyo",
            ["087898765437"] = "Nina Kurnia",
        }
    };

    [HttpGet("account-inquiry")]
    public async Task<IActionResult> AccountInquiry(
        [FromQuery] string type,
        [FromQuery] string provider,
        [FromQuery] string accountNumber)
    {
        if (string.IsNullOrWhiteSpace(provider) || !SupportedProviders.Contains(provider))
        {
            return BadRequest(new ErrorResponse
            {
                Message = $"Provider tidak valid. Provider yang didukung: {string.Join(", ", SupportedProviders)}",
                ErrorCode = "INVALID_PROVIDER"
            });
        }

        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            return BadRequest(new ErrorResponse
            {
                Message = "AccountNumber wajib diisi",
                ErrorCode = "MISSING_ACCOUNT_NUMBER"
            });
        }

        // Simulate async delay for external API call
        await Task.Delay(500);

        if (!MockDatabase.TryGetValue(provider, out var accounts) ||
            !accounts.TryGetValue(accountNumber, out var accountName))
        {
            return NotFound(new ErrorResponse
            {
                Message = "Akun tidak ditemukan",
                ErrorCode = "ACCOUNT_NOT_FOUND"
            });
        }

        return Ok(new EwalletAccountInquiryResponse
        {
            Data = new AccountInquiryData
            {
                Type = type ?? "ewallet",
                Provider = provider.ToLowerInvariant(),
                AccountNumber = accountNumber,
                AccountName = accountName,
                Status = "active"
            }
        });
    }
}
