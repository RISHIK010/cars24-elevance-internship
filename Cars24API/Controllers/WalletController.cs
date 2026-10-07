using Microsoft.AspNetCore.Mvc;
using Cars24API.Services;

namespace Cars24API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WalletController : ControllerBase
{
    private readonly WalletService _walletService;

    public WalletController(WalletService walletService)
    {
        _walletService = walletService;
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> Get(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { message = "userId is required." });

        var wallet = await _walletService.GetOrCreateAsync(userId);
        var transactions = await _walletService.GetTransactionsAsync(userId);

        return Ok(new { balance = wallet.Balance, transactions });
    }

    [HttpPost("redeem")]
    public async Task<IActionResult> Redeem([FromBody] RedeemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            return BadRequest(new { message = "userId is required." });

        if (request.Amount <= 0)
            return BadRequest(new { message = "Redemption amount must be greater than zero." });

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var duplicate = await _walletService.FindByIdempotencyKeyAsync(request.UserId, request.IdempotencyKey);
            if (duplicate != null)
                return Conflict(new { message = "Duplicate redemption detected." });
        }

        var wallet = await _walletService.GetOrCreateAsync(request.UserId);
        if (wallet.Balance < request.Amount)
            return BadRequest(new { message = "Insufficient wallet balance." });

        var referenceId = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? Guid.NewGuid().ToString()
            : request.IdempotencyKey;

        var transaction = await _walletService.DebitAsync(
            request.UserId,
            request.Amount,
            "Wallet redemption",
            referenceId,
            request.IdempotencyKey);

        return Ok(new
        {
            message = "Redemption successful.",
            balance = wallet.Balance - request.Amount,
            transaction
        });
    }

    public class RedeemRequest
    {
        public string UserId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? IdempotencyKey { get; set; }
    }
}
