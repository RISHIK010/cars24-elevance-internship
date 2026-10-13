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

        var referenceId = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? Guid.NewGuid().ToString()
            : request.IdempotencyKey;

        // DebitAsync owns idempotency (including retry recovery) and the
        // insufficient-balance check. A repeated request with the same key
        // returns the original successful redemption instead of a conflict; only
        // a key reused with a different amount is rejected.
        try
        {
            var transaction = await _walletService.DebitAsync(
                request.UserId,
                request.Amount,
                "Wallet redemption",
                referenceId,
                request.IdempotencyKey);

            // Read the persisted wallet after the operation so the returned
            // balance reflects the actual post-debit state rather than a stale
            // snapshot.
            var wallet = await _walletService.GetOrCreateAsync(request.UserId);
            return Ok(new
            {
                message = "Redemption successful.",
                balance = wallet.Balance,
                transaction
            });
        }
        catch (IdempotencyKeyConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new { message = "Insufficient wallet balance." });
        }
    }

    public class RedeemRequest
    {
        public string UserId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? IdempotencyKey { get; set; }
    }
}
