using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Cars24API.Models;

public class Wallet
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public decimal Balance { get; set; }

    // References of credits whose balance effect has already been applied.
    // Credits are applied with an atomic $inc/$addToSet update, and this list
    // is what makes the balance update idempotent and race-free.
    public List<string> AppliedCreditReferenceIds { get; set; } = new List<string>();

    // References of debits whose balance effect has already been applied. Mirrors
    // AppliedCreditReferenceIds: debits are applied with an atomic conditional
    // $inc/$addToSet update, and this list makes the balance decrement
    // idempotent and race-free without replacing the document.
    public List<string> AppliedDebitReferenceIds { get; set; } = new List<string>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
