using Microsoft.AspNetCore.Mvc;
using Cars24API.Models;
using Cars24API.Services;

namespace Cars24API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReferralController : ControllerBase
{
    private readonly ReferralService _referralService;
    private readonly UserService _userService;

    public ReferralController(ReferralService referralService, UserService userService)
    {
        _referralService = referralService;
        _userService = userService;
    }

    [HttpGet("summary/{userId}")]
    public async Task<IActionResult> GetSummary(string userId)
    {
        var user = await _userService.GetByIdAsync(userId);
        if (user == null)
            return NotFound("User not found.");

        // Existing accounts receive a deterministic/generated code lazily.
        if (string.IsNullOrWhiteSpace(user.ReferralCode))
        {
            user.ReferralCode = await _referralService.GenerateUniqueReferralCodeAsync();
            await _userService.UpdateAsync(user.Id!, user);
        }

        var referredBy = await _referralService.GetByReferredUserAsync(userId);
        var referrals = await _referralService.GetByReferrerAsync(userId);

        object? referredByInfo = null;
        if (referredBy != null)
        {
            referredByInfo = new
            {
                referredBy.Id,
                referredBy.ReferralCode,
                referredBy.Status,
                referredBy.RewardGranted
            };
        }

        return Ok(new
        {
            referralCode = user.ReferralCode,
            referredBy = referredByInfo,
            rewardGranted = referredBy?.RewardGranted ?? false,
            referrals = referrals.Select(r => new
            {
                r.Id,
                r.ReferredUserId,
                r.Status,
                r.RewardGranted,
                r.CreatedAt
            })
        });
    }

    [HttpPost("attach")]
    public async Task<IActionResult> Attach([FromBody] AttachRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.ReferralCode))
            return BadRequest(new { message = "userId and referralCode are required." });

        var user = await _userService.GetByIdAsync(request.UserId);
        if (user == null)
            return NotFound("User not found.");

        var code = request.ReferralCode.Trim().ToUpperInvariant();

        var referrer = await _referralService.GetUserByReferralCodeAsync(code);
        if (referrer == null)
            return BadRequest(new { message = "Invalid referral code." });

        if (referrer.Id == request.UserId)
            return BadRequest(new { message = "You cannot refer yourself." });

        var existing = await _referralService.GetByReferredUserAsync(request.UserId);
        if (existing != null)
            return BadRequest(new { message = "Referral relationship already established." });

        var referral = await _referralService.AttachAsync(request.UserId, code, referrer.Id!);
        return Ok(new { message = "Referral attached.", referralId = referral.Id });
    }

    public class AttachRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string ReferralCode { get; set; } = string.Empty;
    }
}
