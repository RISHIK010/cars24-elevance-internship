using Cars24API.Models;

namespace Cars24API.Services;

public interface INotificationService
{
    Task<NotificationDevice> RegisterDeviceAsync(NotificationDevice device);
    Task<bool> UnregisterDeviceAsync(string userId, string deviceId);
    Task<List<NotificationDevice>> GetDevicesAsync(string userId);
    Task<NotificationPreferences?> GetPreferencesAsync(string userId);
    Task<NotificationPreferences?> UpdatePreferencesAsync(string userId, NotificationPreferences preferences);

    Task<int> SendAppointmentConfirmationAsync(string userId, string? vehicleId, string body);
    Task<int> SendPriceDropAsync(string userId, string? vehicleId, string body);
    Task<int> SendBidUpdateAsync(string userId, string? vehicleId, string body);
    Task<int> SendNewMessageAsync(string userId, string? vehicleId, string body);
}
