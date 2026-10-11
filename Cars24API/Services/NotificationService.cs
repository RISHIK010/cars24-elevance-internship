using Cars24API.Models;
using MongoDB.Driver;

namespace Cars24API.Services;

/// <summary>
/// Device-registration + dispatch service. Enforces per-device channel and
/// per-category preferences before any notification is sent, and deactivates
/// devices whose FCM token is no longer valid.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IMongoCollection<NotificationDevice> _devices;
    private readonly IFcmSender _fcmSender;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IConfiguration config, IFcmSender fcmSender, ILogger<NotificationService> logger)
    {
        var client = new MongoClient(config["MongoDB:ConnectionString"]);
        var database = client.GetDatabase(config["MongoDB:DatabaseName"]);
        _devices = database.GetCollection<NotificationDevice>("NotificationDevices");
        _fcmSender = fcmSender;
        _logger = logger;
    }

    public async Task<NotificationDevice> RegisterDeviceAsync(NotificationDevice device)
    {
        var existing = await _devices
            .Find(d => d.UserId == device.UserId && d.DeviceId == device.DeviceId)
            .FirstOrDefaultAsync();

        if (existing is null)
        {
            device.IsActive = true;
            device.CreatedAtUtc = DateTime.UtcNow;
            device.UpdatedAtUtc = DateTime.UtcNow;
            await _devices.InsertOneAsync(device);
            return device;
        }

        existing.RegistrationToken = device.RegistrationToken;
        existing.Channel = device.Channel;
        existing.IsActive = true;
        existing.Preferences = device.Preferences ?? existing.Preferences ?? new NotificationPreferences();
        existing.UpdatedAtUtc = DateTime.UtcNow;
        await _devices.ReplaceOneAsync(d => d.Id == existing.Id, existing);
        return existing;
    }

    public async Task<bool> UnregisterDeviceAsync(string userId, string deviceId)
    {
        var result = await _devices.DeleteOneAsync(d => d.UserId == userId && d.DeviceId == deviceId);
        return result.DeletedCount > 0;
    }

    public async Task<List<NotificationDevice>> GetDevicesAsync(string userId) =>
        await _devices.Find(d => d.UserId == userId && d.IsActive).ToListAsync();

    public async Task<NotificationPreferences?> GetPreferencesAsync(string userId)
    {
        var device = await _devices.Find(d => d.UserId == userId && d.IsActive)
            .SortByDescending(d => d.UpdatedAtUtc)
            .FirstOrDefaultAsync();
        return device?.Preferences;
    }

    public async Task<NotificationPreferences?> UpdatePreferencesAsync(string userId, NotificationPreferences preferences)
    {
        var devices = await _devices.Find(d => d.UserId == userId).ToListAsync();
        if (devices.Count == 0) return null;

        foreach (var device in devices)
        {
            device.Preferences = preferences;
            device.UpdatedAtUtc = DateTime.UtcNow;
            await _devices.ReplaceOneAsync(d => d.Id == device.Id, device);
        }

        return preferences;
    }

    public Task<int> SendAppointmentConfirmationAsync(string userId, string? vehicleId, string body) =>
        DispatchAsync(userId, NotificationType.AppointmentConfirmation, new NotificationPayload
        {
            Type = nameof(NotificationType.AppointmentConfirmation),
            Title = "Appointment Confirmed",
            Body = body,
            VehicleId = vehicleId,
            ActionUrl = string.IsNullOrWhiteSpace(vehicleId) ? "/appointments" : $"/buy-car/{vehicleId}"
        });

    public Task<int> SendPriceDropAsync(string userId, string? vehicleId, string body) =>
        DispatchAsync(userId, NotificationType.PriceDrop, new NotificationPayload
        {
            Type = nameof(NotificationType.PriceDrop),
            Title = "Price Drop",
            Body = body,
            VehicleId = vehicleId,
            ActionUrl = string.IsNullOrWhiteSpace(vehicleId) ? "/buy-car" : $"/buy-car/{vehicleId}"
        });

    public Task<int> SendBidUpdateAsync(string userId, string? vehicleId, string body) =>
        DispatchAsync(userId, NotificationType.BidUpdate, new NotificationPayload
        {
            Type = nameof(NotificationType.BidUpdate),
            Title = "Bid Update",
            Body = body,
            VehicleId = vehicleId,
            ActionUrl = string.IsNullOrWhiteSpace(vehicleId) ? "/buy-car" : $"/buy-car/{vehicleId}"
        });

    public Task<int> SendNewMessageAsync(string userId, string? vehicleId, string body) =>
        DispatchAsync(userId, NotificationType.NewMessage, new NotificationPayload
        {
            Type = nameof(NotificationType.NewMessage),
            Title = "New Message",
            Body = body,
            VehicleId = vehicleId,
            ActionUrl = string.IsNullOrWhiteSpace(vehicleId) ? "/buy-car" : $"/buy-car/{vehicleId}"
        });

    private async Task<int> DispatchAsync(string userId, NotificationType type, NotificationPayload payload)
    {
        _logger.LogInformation("Dispatching {Type} notification for user {UserId}.", type, userId);

        var devices = await GetDevicesAsync(userId);

        if (devices.Count == 0)
        {
            _logger.LogWarning(
                "No active push devices registered for user {UserId}; {Type} notification was not sent.",
                userId, type);
            return 0;
        }

        var sent = 0;

        foreach (var device in devices)
        {
            if (!device.Preferences.BrowserPush)
            {
                _logger.LogInformation(
                    "Skipping {Type} notification for user {UserId} device {DeviceId}: browser push is disabled.",
                    type, userId, device.DeviceId);
                continue;
            }

            if (!IsCategoryEnabled(device.Preferences, type))
            {
                _logger.LogInformation(
                    "Skipping {Type} notification for user {UserId} device {DeviceId}: category is disabled in preferences.",
                    type, userId, device.DeviceId);
                continue;
            }

            try
            {
                if (await _fcmSender.SendAsync(device.RegistrationToken, payload))
                {
                    sent++;
                }
                else
                {
                    _logger.LogWarning(
                        "FCM did not accept the {Type} notification for user {UserId} device {DeviceId} (see FcmSender log for the reason).",
                        type, userId, device.DeviceId);
                }
            }
            catch (InvalidRegistrationException)
            {
                await DeactivateAsync(device);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification dispatch failed for user {UserId}, device {DeviceId}.", userId, device.DeviceId);
            }
        }

        _logger.LogInformation(
            "Dispatched {Type} notification for user {UserId} to {Sent}/{Total} device(s).",
            type, userId, sent, devices.Count);

        return sent;
    }

    private async Task DeactivateAsync(NotificationDevice device)
    {
        device.IsActive = false;
        device.UpdatedAtUtc = DateTime.UtcNow;
        await _devices.ReplaceOneAsync(d => d.Id == device.Id, device);
        _logger.LogWarning("Deactivated invalid FCM registration for user {UserId} device {DeviceId}.", device.UserId, device.DeviceId);
    }

    private static bool IsCategoryEnabled(NotificationPreferences preferences, NotificationType type) => type switch
    {
        NotificationType.AppointmentConfirmation => preferences.AppointmentConfirmations,
        NotificationType.BidUpdate => preferences.BidUpdates,
        NotificationType.PriceDrop => preferences.PriceDrops,
        NotificationType.NewMessage => preferences.NewMessages,
        _ => false
    };
}
