using Cars24API.Models;
using MongoDB.Driver;

namespace Cars24API.Services;

public class ReferralService
{
    public const decimal ReferralRewardAmount = 100m;

    private const string CodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private readonly IMongoCollection<Referral> _referrals;
    private readonly IMongoCollection<User> _users;
    private readonly WalletService _walletService;

    public ReferralService(IConfiguration config, WalletService walletService)
    {
        var client = new MongoClient(config["MongoDB:ConnectionString"]);
        var database = client.GetDatabase(config["MongoDB:DatabaseName"]);
        _referrals = database.GetCollection<Referral>("Referrals");
        _users = database.GetCollection<User>("Users");
        _walletService = walletService;
    }

    public async Task<string> GenerateUniqueReferralCodeAsync()
    {
        while (true)
        {
            var code = GenerateCode();
            var existing = await _users.Find(u => u.ReferralCode == code).FirstOrDefaultAsync();
            if (existing == null)
                return code;
        }
    }

    private static string GenerateCode()
    {
        var suffix = new char[6];
        for (var i = 0; i < suffix.Length; i++)
            suffix[i] = CodeAlphabet[Random.Shared.Next(CodeAlphabet.Length)];

        return "C24" + new string(suffix);
    }

    public async Task<User?> GetUserByReferralCodeAsync(string referralCode)
    {
        return await _users.Find(u => u.ReferralCode == referralCode).FirstOrDefaultAsync();
    }

    public async Task<Referral?> GetByReferredUserAsync(string referredUserId)
    {
        return await _referrals.Find(r => r.ReferredUserId == referredUserId).FirstOrDefaultAsync();
    }

    public async Task<List<Referral>> GetByReferrerAsync(string referrerUserId)
    {
        return await _referrals.Find(r => r.ReferrerUserId == referrerUserId).ToListAsync();
    }

    public async Task<Referral> AttachAsync(string referredUserId, string referralCode, string referrerUserId)
    {
        var referral = new Referral
        {
            ReferrerUserId = referrerUserId,
            ReferredUserId = referredUserId,
            ReferralCode = referralCode,
            Status = "pending",
            RewardGranted = false
        };
        await _referrals.InsertOneAsync(referral);
        return referral;
    }

    public async Task TryGrantReferralRewardAsync(string referredUserId, string qualifyingEventId)
    {
        var referral = await GetByReferredUserAsync(referredUserId);
        if (referral == null || referral.RewardGranted)
            return;

        referral.RewardGranted = true;
        referral.Status = "rewarded";
        referral.QualifyingEventId = qualifyingEventId;
        referral.RewardGrantedAt = DateTime.UtcNow;
        await _referrals.ReplaceOneAsync(r => r.Id == referral.Id, referral);

        await _walletService.CreditAsync(
            referral.ReferrerUserId,
            ReferralRewardAmount,
            "Referral reward",
            qualifyingEventId);
    }
}
