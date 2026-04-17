using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PedagangPulsa.Application.Abstractions.Fcm;
using PedagangPulsa.Application.Abstractions.Persistence;
using PedagangPulsa.Domain.Entities;

namespace PedagangPulsa.Application.Services;

public class FcmService
{
    private readonly IAppDbContext _context;
    private readonly IFcmClient _fcmClient;
    private readonly ILogger<FcmService> _logger;

    public FcmService(IAppDbContext context, IFcmClient fcmClient, ILogger<FcmService> logger)
    {
        _context = context;
        _fcmClient = fcmClient;
        _logger = logger;
    }

    public async Task<UserDevice> RegisterOrUpdateTokenAsync(
        Guid userId, string fcmToken, string? deviceName, string? platform, string? appVersion)
    {
        var existing = await _context.UserDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.FcmToken == fcmToken);

        if (existing != null)
        {
            existing.DeviceName = deviceName;
            existing.Platform = platform;
            existing.AppVersion = appVersion;
            existing.LastActiveAt = DateTime.UtcNow;
            existing.IsActive = true;
            await _context.SaveChangesAsync();
            return existing;
        }

        // Deactivate stale registrations of this token from other users
        var staleDevices = await _context.UserDevices
            .Where(d => d.FcmToken == fcmToken && d.UserId != userId)
            .ToListAsync();

        foreach (var stale in staleDevices)
        {
            stale.IsActive = false;
        }

        var device = new UserDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FcmToken = fcmToken,
            DeviceName = deviceName,
            Platform = platform,
            AppVersion = appVersion,
            CreatedAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.UserDevices.Add(device);
        await _context.SaveChangesAsync();
        return device;
    }

    public async Task<bool> UnregisterTokenAsync(Guid userId, string fcmToken)
    {
        var device = await _context.UserDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.FcmToken == fcmToken && d.IsActive);

        if (device == null)
        {
            return false;
        }

        device.IsActive = false;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<UserDevice>> GetActiveDevicesAsync(Guid userId)
    {
        return await _context.UserDevices
            .Where(d => d.UserId == userId && d.IsActive)
            .ToListAsync();
    }

    public async Task<FcmSendResult> SendToUserAsync(
        Guid userId, string title, string body, Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        var devices = await GetActiveDevicesAsync(userId);

        if (devices.Count == 0)
        {
            _logger.LogWarning("No active devices for user {UserId}, skipping FCM send", userId);
            return new FcmSendResult(false, "No active devices");
        }

        var payload = new FcmNotificationPayload(title, body, data);

        if (devices.Count == 1)
        {
            return await _fcmClient.SendAsync(devices[0].FcmToken, payload, cancellationToken);
        }

        var tokens = devices.Select(d => d.FcmToken).ToList();
        var results = await _fcmClient.SendMulticastAsync(tokens, payload, cancellationToken);

        var successCount = results.Count(r => r.Success);
        return new FcmSendResult(
            successCount > 0,
            $"Sent to {successCount}/{results.Count} devices",
            results.FirstOrDefault(r => r.FcmMessageId != null)?.FcmMessageId);
    }
}
