using Cars24API.Models;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Cars24API.Services;

public class WalletService
{
    private readonly IMongoCollection<Wallet> _wallets;
    private readonly IMongoCollection<WalletTransaction> _transactions;
    private readonly ILogger<WalletService> _logger;

    public WalletService(IConfiguration config, ILogger<WalletService> logger)
    {
        _logger = logger;
        var client = new MongoClient(config["MongoDB:ConnectionString"]);
        var database = client.GetDatabase(config["MongoDB:DatabaseName"]);
        _wallets = database.GetCollection<Wallet>("Wallets");
        _transactions = database.GetCollection<WalletTransaction>("WalletTransactions");
    }

    public async Task<Wallet> GetOrCreateAsync(string userId)
    {
        var wallet = await _wallets.Find(w => w.UserId == userId).FirstOrDefaultAsync();
        if (wallet != null)
            return wallet;

        wallet = new Wallet { UserId = userId, Balance = 0 };
        try
        {
            await _wallets.InsertOneAsync(wallet);
            return wallet;
        }
        catch (MongoWriteException ex) when (IsDuplicateKey(ex))
        {
            // Lost a concurrent create race (possible when the Wallets_UserId
            // unique index exists). Return the wallet that won the race.
            return await _wallets.Find(w => w.UserId == userId).FirstOrDefaultAsync()
                   ?? throw new InvalidOperationException("Unable to load wallet after concurrent creation.");
        }
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
        // A credit is idempotent per (userId, referenceId). An existing
        // transaction record must NOT be treated as proof that the balance was
        // updated: a previous attempt may have persisted the record and then
        // failed before applying the balance. We always (re)apply the balance
        // effect below, which is itself a no-op when already applied.
        var existing = await _transactions
            .Find(t => t.UserId == userId && t.Type == "credit" && t.ReferenceId == referenceId)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            await ApplyCreditBalanceAsync(userId, existing.Amount, referenceId);
            return existing;
        }

        // Apply the balance effect first, atomically. The increment and the
        // recorded reference are written in a single document update, so
        // concurrent and retried requests can never credit the same reference
        // twice.
        await ApplyCreditBalanceAsync(userId, amount, referenceId);

        var transaction = new WalletTransaction
        {
            UserId = userId,
            Type = "credit",
            Amount = amount,
            Description = description,
            ReferenceId = referenceId
        };

        try
        {
            await _transactions.InsertOneAsync(transaction);
        }
        catch (MongoWriteException ex) when (IsDuplicateKey(ex))
        {
            // A concurrent request recorded this credit first (requires the
            // unique index). Return the persisted record.
            return await _transactions
                .Find(t => t.UserId == userId && t.Type == "credit" && t.ReferenceId == referenceId)
                .FirstOrDefaultAsync() ?? transaction;
        }

        return transaction;
    }

    public async Task<WalletTransaction> DebitAsync(string userId, decimal amount, string description, string referenceId, string? idempotencyKey = null)
    {
        if (amount <= 0)
            throw new InvalidOperationException("Debit amount must be greater than zero.");

        // Idempotent replay: a previously recorded debit with this key is returned
        // as-is. We still (re)apply the balance effect below because a previous
        // attempt may have recorded the transaction and then failed before
        // applying the balance; the apply itself is a no-op when already applied.
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await FindByIdempotencyKeyAsync(userId, idempotencyKey);
            if (existing != null)
            {
                // A reused key must describe the same redemption. A different
                // amount would otherwise let one key stand for two different
                // redemptions, so it is rejected instead of silently ignored.
                if (existing.Amount != amount)
                    throw new IdempotencyKeyConflictException(
                        "This idempotency key was already used for a different redemption amount.");

                await ApplyDebitBalanceAsync(userId, existing.Amount, existing.ReferenceId);
                return existing;
            }
        }

        // Apply the balance effect first, atomically. The decrement and the
        // recorded reference are written in a single conditional document update,
        // so the balance can never go negative, concurrent credit updates (and
        // AppliedCreditReferenceIds) are preserved, and a retry/concurrent call
        // with the same reference is a no-op.
        var applied = await ApplyDebitBalanceAsync(userId, amount, referenceId);

        var transaction = new WalletTransaction
        {
            UserId = userId,
            Type = "debit",
            Amount = amount,
            Description = description,
            ReferenceId = referenceId,
            IdempotencyKey = idempotencyKey
        };

        if (!applied)
        {
            // Two reasons a debit may not have been applied: the balance is
            // insufficient, or another request already applied this exact
            // reference (idempotent duplicate). Check the wallet marker to decide.
            var wallet = await GetOrCreateAsync(userId);
            if (wallet.AppliedDebitReferenceIds.Contains(referenceId))
            {
                // Already applied (concurrent duplicate or crash recovery). Record
                // the transaction if it has not been recorded yet, then return it.
                try
                {
                    await _transactions.InsertOneAsync(transaction);
                }
                catch (MongoWriteException ex) when (IsDuplicateKey(ex))
                {
                    // A concurrent request recorded it first; fall through to
                    // return the persisted record.
                }

                if (idempotencyKey != null)
                    return await FindByIdempotencyKeyAsync(userId, idempotencyKey) ?? transaction;
                return transaction;
            }

            throw new InvalidOperationException("Insufficient wallet balance.");
        }

        try
        {
            await _transactions.InsertOneAsync(transaction);
        }
        catch (MongoWriteException ex) when (IsDuplicateKey(ex))
        {
            // A concurrent request with the same idempotency key recorded this
            // debit first (requires the unique partial index). Return the
            // persisted record.
            if (idempotencyKey != null)
                return await FindByIdempotencyKeyAsync(userId, idempotencyKey) ?? transaction;
        }

        return transaction;
    }

    // Best-effort startup migration. Creates the indexes that enforce wallet,
    // credit and debit invariants. Any failure (for example a legacy collection
    // that already contains duplicate keys) is logged and swallowed so
    // application startup is never blocked; CreditAsync/DebitAsync's atomic
    // balance updates still keep balances correct even if an index could not be
    // created.
    public async Task EnsureIndexesAsync()
    {
        try
        {
            var creditIndex = new CreateIndexModel<WalletTransaction>(
                Builders<WalletTransaction>.IndexKeys
                    .Ascending(t => t.UserId)
                    .Ascending(t => t.ReferenceId),
                new CreateIndexOptions<WalletTransaction>
                {
                    Name = "WalletTransactions_UserId_ReferenceId_Credit",
                    Unique = true,
                    PartialFilterExpression = Builders<WalletTransaction>.Filter.Eq(t => t.Type, "credit")
                });
            await _transactions.Indexes.CreateOneAsync(creditIndex);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create credit idempotency index; continuing startup.");
        }

        try
        {
            var debitIndex = new CreateIndexModel<WalletTransaction>(
                Builders<WalletTransaction>.IndexKeys
                    .Ascending(t => t.UserId)
                    .Ascending(t => t.IdempotencyKey),
                new CreateIndexOptions<WalletTransaction>
                {
                    Name = "WalletTransactions_UserId_IdempotencyKey_Debit",
                    Unique = true,
                    PartialFilterExpression = Builders<WalletTransaction>.Filter.And(
                        Builders<WalletTransaction>.Filter.Eq(t => t.Type, "debit"),
                        Builders<WalletTransaction>.Filter.Ne(t => t.IdempotencyKey, null),
                        Builders<WalletTransaction>.Filter.Ne(t => t.IdempotencyKey, ""))
                });
            await _transactions.Indexes.CreateOneAsync(debitIndex);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create debit idempotency index; continuing startup.");
        }

        try
        {
            var walletIndex = new CreateIndexModel<Wallet>(
                Builders<Wallet>.IndexKeys.Ascending(w => w.UserId),
                new CreateIndexOptions<Wallet> { Name = "Wallets_UserId", Unique = true });
            await _wallets.Indexes.CreateOneAsync(walletIndex);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create wallet user-id index; continuing startup.");
        }
    }

    // Applies a credit's balance effect exactly once per (userId, referenceId).
    // The increment and the recorded reference are written in a single
    // single-document update; the filter excludes wallets that already contain
    // the reference, so a retry/concurrent call is a no-op.
    private async Task ApplyCreditBalanceAsync(string userId, decimal amount, string referenceId)
    {
        await GetOrCreateAsync(userId);

        var filter = Builders<Wallet>.Filter.And(
            Builders<Wallet>.Filter.Eq(w => w.UserId, userId),
            Builders<Wallet>.Filter.Not(
                Builders<Wallet>.Filter.AnyEq(w => w.AppliedCreditReferenceIds, referenceId)));

        var update = Builders<Wallet>.Update
            .Inc(w => w.Balance, amount)
            .AddToSet(w => w.AppliedCreditReferenceIds, referenceId)
            .Set(w => w.UpdatedAt, DateTime.UtcNow);

        await _wallets.UpdateOneAsync(filter, update);
    }

    // Applies a debit's balance effect exactly once per (userId, referenceId).
    // The decrement and the recorded reference are written in a single
    // single-document update; the filter requires enough balance and excludes
    // wallets that already contain the reference, so the balance can never go
    // negative and a retry/concurrent call is a no-op.
    private async Task<bool> ApplyDebitBalanceAsync(string userId, decimal amount, string referenceId)
    {
        await GetOrCreateAsync(userId);

        var filter = Builders<Wallet>.Filter.And(
            Builders<Wallet>.Filter.Eq(w => w.UserId, userId),
            Builders<Wallet>.Filter.Gte(w => w.Balance, amount),
            Builders<Wallet>.Filter.Not(
                Builders<Wallet>.Filter.AnyEq(w => w.AppliedDebitReferenceIds, referenceId)));

        var update = Builders<Wallet>.Update
            .Inc(w => w.Balance, -amount)
            .AddToSet(w => w.AppliedDebitReferenceIds, referenceId)
            .Set(w => w.UpdatedAt, DateTime.UtcNow);

        var result = await _wallets.UpdateOneAsync(filter, update);
        return result.MatchedCount > 0;
    }

    private static bool IsDuplicateKey(MongoWriteException ex) =>
        ex.WriteError.Category == ServerErrorCategory.DuplicateKey;
}

// Thrown when a debit idempotency key is reused with a different amount. The
// original redemption is preserved; this prevents a single key from creating a
// second, different redemption.
public class IdempotencyKeyConflictException : Exception
{
    public IdempotencyKeyConflictException(string message) : base(message) { }
}
