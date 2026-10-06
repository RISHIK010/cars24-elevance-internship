using Cars24API.Models;

namespace Cars24API.Services;

public interface IFcmSender
{
    bool IsConfigured { get; }

    /// <summary>
    /// Sends a push notification to a single registration token.
    /// Returns true when delivered and false when FCM is not configured.
    /// Throws <see cref="InvalidRegistrationException"/> for invalid/unregistered tokens.
    /// </summary>
    Task<bool> SendAsync(string registrationToken, NotificationPayload payload);
}
