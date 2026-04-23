using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PedagangPulsa.Application.Abstractions.Caching;
using PedagangPulsa.Application.Abstractions.Persistence;
using PedagangPulsa.Application.Abstractions.Sms;
using PedagangPulsa.Domain.Configuration;
using PedagangPulsa.Domain.Entities;

namespace PedagangPulsa.Application.Services;

public class PhoneVerificationService
{
    private readonly IAppDbContext _dbContext;
    private readonly IRedisService _redis;
    private readonly ISmsClient _smsClient;
    private readonly SmsGateConfig _config;
    private readonly ILogger<PhoneVerificationService> _logger;

    private const string OtpKeyPrefix = "phone_otp:";
    private const string OtpAttemptsPrefix = "phone_otp_attempts:";
    private const string OtpRateLimitPrefix = "phone_otp_limit:";

    public PhoneVerificationService(
        IAppDbContext dbContext,
        IRedisService redis,
        ISmsClient smsClient,
        IOptions<SmsGateConfig> config,
        ILogger<PhoneVerificationService> logger)
    {
        _dbContext = dbContext;
        _redis = redis;
        _smsClient = smsClient;
        _config = config.Value;
        _logger = logger;
    }

    public static string? NormalizePhoneNumber(string phone)
    {
        var cleaned = phone.Trim().Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

        if (cleaned.StartsWith("08"))
        {
            cleaned = "+62" + cleaned[1..];
        }
        else if (cleaned.StartsWith("8") && cleaned.Length >= 10)
        {
            cleaned = "+62" + cleaned;
        }
        else if (cleaned.StartsWith("+62"))
        {
            // already normalized
        }
        else if (cleaned.StartsWith("62"))
        {
            cleaned = "+" + cleaned;
        }
        else
        {
            return null;
        }

        // Validate: +62 followed by 8-13 digits
        if (cleaned.Length < 13 || cleaned.Length > 16)
            return null;

        return cleaned;
    }

    public async Task<(bool Success, string? ErrorCode, string? Message)> RequestOtpAsync(string phoneNumber)
    {
        var normalized = NormalizePhoneNumber(phoneNumber);
        if (normalized == null)
        {
            return (false, "INVALID_PHONE_FORMAT", "Format nomor telepon tidak valid");
        }

        // Rate limiting
        var rateLimitKey = $"{OtpRateLimitPrefix}{normalized}";
        var requestCount = await _redis.IncrementAsync(
            rateLimitKey, TimeSpan.FromSeconds(_config.RateLimitWindowSeconds));

        if (requestCount > _config.MaxOtpRequests)
        {
            _logger.LogWarning("OTP rate limit exceeded for {Phone}, count={Count}", normalized, requestCount);
            return (false, "PHONE_OTP_RATE_LIMITED",
                $"Terlalu banyak permintaan. Silakan coba lagi dalam {_config.RateLimitWindowSeconds / 60} menit");
        }

        // Generate OTP
        var otp = GenerateOtp(_config.OtpLength);
        var otpKey = $"{OtpKeyPrefix}{normalized}";
        var attemptsKey = $"{OtpAttemptsPrefix}{normalized}";

        // Store OTP and reset attempts
        await _redis.SetAsync(otpKey, otp, TimeSpan.FromSeconds(_config.OtpExpirySeconds));
        await _redis.SetAsync(attemptsKey, "0", TimeSpan.FromSeconds(_config.OtpExpirySeconds));

        // Send SMS
        var smsResult = await _smsClient.SendAsync(new SmsSendRequest(
            $"Kode verifikasi Anda: {otp}. Berlaku {_config.OtpExpirySeconds / 60} menit.",
            normalized));

        if (!smsResult.Success)
        {
            _logger.LogError("Failed to send OTP SMS to {Phone}: {Error}", normalized, smsResult.Error);

            // Clean up stored OTP
            await _redis.RemoveAsync(otpKey);
            await _redis.RemoveAsync(attemptsKey);

            var errorCode = smsResult.Error == "SMS_GATEWAY_UNAVAILABLE"
                ? "SMS_GATEWAY_UNAVAILABLE"
                : "SMS_SEND_FAILED";

            var message = smsResult.Error == "SMS_GATEWAY_UNAVAILABLE"
                ? "Layanan SMS sedang tidak tersedia, silakan coba lagi beberapa saat"
                : "Gagal mengirim SMS, silakan coba lagi";

            return (false, errorCode, message);
        }

        _logger.LogInformation("OTP sent to {Phone}, messageId={MessageId}", normalized, smsResult.MessageId);
        return (true, null, "OTP berhasil dikirim");
    }

    public async Task<(bool Success, string? ErrorCode, string? Message)> VerifyOtpAsync(
        string phoneNumber, string otp, Guid userId)
    {
        var normalized = NormalizePhoneNumber(phoneNumber);
        if (normalized == null)
        {
            return (false, "INVALID_PHONE_FORMAT", "Format nomor telepon tidak valid");
        }

        // Check OTP exists
        var otpKey = $"{OtpKeyPrefix}{normalized}";
        var storedOtp = await _redis.GetAsync(otpKey);

        if (storedOtp == null)
        {
            return (false, "OTP_EXPIRED", "Kode OTP sudah expired atau tidak ditemukan");
        }

        // Check attempts
        var attemptsKey = $"{OtpAttemptsPrefix}{normalized}";
        var attempts = await _redis.IncrementAsync(attemptsKey);

        if (attempts > _config.MaxVerifyAttempts)
        {
            // Invalidate OTP after max attempts
            await _redis.RemoveAsync(otpKey);
            await _redis.RemoveAsync(attemptsKey);
            return (false, "OTP_MAX_ATTEMPTS", "Terlalu banyak percobaan. Silakan request OTP baru");
        }

        // Constant-time comparison
        if (!ConstantTimeEquals(storedOtp, otp))
        {
            return (false, "INVALID_OTP", "Kode OTP salah");
        }

        // OTP valid - clean up
        await _redis.RemoveAsync(otpKey);
        await _redis.RemoveAsync(attemptsKey);

        // Update user
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return (false, "USER_NOT_FOUND", "User tidak ditemukan");
        }

        user.PhoneVerifiedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Phone verified for user {UserId}, phone={Phone}", userId, normalized);
        return (true, null, "Nomor telepon berhasil diverifikasi");
    }

    private static string GenerateOtp(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        var digits = new char[length];
        for (var i = 0; i < length; i++)
        {
            digits[i] = (char)('0' + (bytes[i] % 10));
        }
        return new string(digits);
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;

        var result = 0;
        for (var i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }
        return result == 0;
    }
}
