namespace Cars24API.Models;

/// <summary>
/// Minimal notification payload forwarded to registered devices. Only the
/// fields required to display and navigate a notification are included; no
/// user data beyond the event context is sent.
/// </summary>
public class NotificationPayload
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? VehicleId { get; set; }
    public string? ActionUrl { get; set; }
}
