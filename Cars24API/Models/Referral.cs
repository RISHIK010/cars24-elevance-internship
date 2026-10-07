using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Cars24API.Models;

public class Referral
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string ReferrerUserId { get; set; } = string.Empty;

    public string ReferredUserId { get; set; } = string.Empty;

    public string ReferralCode { get; set; } = string.Empty;

    public string Status { get; set; } = "pending";

    public bool RewardGranted { get; set; }

    public string? QualifyingEventId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RewardGrantedAt { get; set; }
}
