using Cars24API.Models;
using Cars24API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cars24API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public class RegisterDeviceRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string RegistrationToken { get; set; } = string.Empty;
        public NotificationPreferences? Preferences { get; set; }
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest request)
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.UserId) ||
            string.IsNullOrWhiteSpace(request.DeviceId) ||
            string.IsNullOrWhiteSpace(request.RegistrationToken))
        {
            return BadRequest("userId, deviceId and registrationToken are required.");
        }

        var device = new NotificationDevice
        {
            UserId = request.UserId,
            DeviceId = request.DeviceId,
            RegistrationToken = request.RegistrationToken,
            IsActive = true,
            Channel = NotificationChannel.BrowserPush,
            Preferences = request.Preferences ?? new NotificationPreferences()
        };

        var saved = await _notificationService.RegisterDeviceAsync(device);
        return Ok(saved);
    }

    [HttpDelete("{userId}/devices/{deviceId}")]
    public async Task<IActionResult> Unregister(string userId, string deviceId)
    {
        var removed = await _notificationService.UnregisterDeviceAsync(userId, deviceId);
        if (!removed) return NotFound();
        return NoContent();
    }

    [HttpGet("{userId}/preferences")]
    public async Task<IActionResult> GetPreferences(string userId)
    {
        var preferences = await _notificationService.GetPreferencesAsync(userId);
        if (preferences is null) return NotFound();
        return Ok(preferences);
    }

    [HttpPut("{userId}/preferences")]
    public async Task<IActionResult> UpdatePreferences(string userId, [FromBody] NotificationPreferences preferences)
    {
        if (preferences is null) return BadRequest();
        var updated = await _notificationService.UpdatePreferencesAsync(userId, preferences);
        if (updated is null) return NotFound();
        return Ok(updated);
    }

    // ---------------------------------------------------------------------
    // Minimal supporting event endpoints. These exist only to demonstrate
    // real-time notification delivery for the internship requirement; they
    // intentionally do NOT implement a full bidding or messaging system.
    // ---------------------------------------------------------------------

    public class BidUpdateRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string? VehicleId { get; set; }
        public string BidAmount { get; set; } = string.Empty;
    }

    public class NewMessageRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string? VehicleId { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    [HttpPost("bid-update")]
    public async Task<IActionResult> BidUpdate([FromBody] BidUpdateRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.UserId))
            return BadRequest("userId is required.");

        var delivered = await _notificationService.SendBidUpdateAsync(
            request.UserId,
            request.VehicleId,
            $"You received a new bid of {request.BidAmount}.");

        return Ok(new { type = "BidUpdate", delivered });
    }

    [HttpPost("message")]
    public async Task<IActionResult> NewMessage([FromBody] NewMessageRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.UserId))
            return BadRequest("userId is required.");

        var delivered = await _notificationService.SendNewMessageAsync(
            request.UserId,
            request.VehicleId,
            $"New message: {request.Message}");

        return Ok(new { type = "NewMessage", delivered });
    }
}
