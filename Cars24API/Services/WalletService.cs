using Cars24API.Models;
using MongoDB.Driver;

namespace Cars24API.Services;

public class WalletService
{
    private readonly IMongoCollection<Wallet> _wallets;
    private readonly IMongoCollection<WalletTransaction> _transactions;

    public WalletService(IConfiguration config)
    {
        var client = new MongoClient(config["MongoDB:ConnectionString"]);
        var database = client.GetDatabase(config["MongoDB:DatabaseName"]);
        _wallets = database.GetCollection<Wallet>("Wallets");
        _transactions = database.GetCollection<WalletTransaction>("WalletTransactions");
    }

    public async Task<Wallet> GetOrCreateAsync(string userId)
    {
        var wallet = await _wallets.Find(w => w.UserId == userId).FirstOrDefaultAsync();
        if (wallet == null)
        {
            wallet = new Wallet { UserId = userId, Balance = 0 };
            await _wallets.InsertOneAsync(wallet);
        }
        return wallet;
    }

    public async Task<List<WalletTransaction>> GetTransactionsAsync(string userId)
    {
        return await _transactions.Find(t => t.UserId == userId)
            .SortByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<WalletTransaction?> FindByIdempotencyKeyAsync(string userId, string idempotencyKey)
    {
        return await _transactions
            .Find(t => t.UserId == userId && t.IdempotencyKey == idempotencyKey)
            .FirstOrDefaultAsync();
    }

    public async Task<WalletTransaction> CreditAsync(string userId, decimal amount, string description, string referenceId)
    {
        var wallet = await GetOrCreateAsync(userId);

        var transaction = new WalletTransaction
        {
            UserId = userId,
            Type = "credit",
            Amount = amount,
            Description = description,
            ReferenceId = referenceId
        };
        await _transactions.InsertOneAsync(transaction);

        wallet.Balance += amount;
        wallet.UpdatedAt = DateTime.UtcNow;
        await _wallets.ReplaceOneAsync(w => w.Id == wallet.Id, wallet);

        return transaction;
    }

    public async Task<WalletTransaction> DebitAsync(string userId, decimal amount, string description, string referenceId, string? idempotencyKey = null)
    {
        var wallet = await GetOrCreateAsync(userId);

        if (wallet.Balance < amount)
            throw new InvalidOperationException("Insufficient wallet balance.");

        var transaction = new WalletTransaction
        {
            UserId = userId,
            Type = "debit",
            Amount = amount,
            Description = description,
            ReferenceId = referenceId,
            IdempotencyKey = idempotencyKey
        };
        await _transactions.InsertOneAsync(transaction);

        wallet.Balance -= amount;
        wallet.UpdatedAt = DateTime.UtcNow;
        await _wallets.ReplaceOneAsync(w => w.Id == wallet.Id, wallet);

        return transaction;
    }
}
