using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;
namespace Cars24API.Models;

public class Specs
{
    public int Year { get; set; }
    public string Km { get; set; } = string.Empty;
    public string Fuel { get; set; } = string.Empty;
    public string Transmission { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Insurance { get; set; } = string.Empty;
}
public class Car
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    public List<string> Images { get; set; } = new List<string>();
    public string Title { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    // Computed "Recommended Price" produced by the dynamic-pricing engine.
    // This is populated at read time and is NOT persisted (BsonIgnore keeps it
    // out of MongoDB), so the stored base Price is never modified.
    [BsonIgnore]
    public double? RecommendedPrice { get; set; }

    // Maintenance estimate (deterministic, computed at read time; not persisted).
    // These mirror the RecommendedPrice pattern: populated when a listing is
    // served and kept out of MongoDB so stored data is never modified.
    [BsonIgnore]
    public double? EstimatedMonthlyMaintenance { get; set; }
    [BsonIgnore]
    public string? MaintenanceStatus { get; set; }
    [BsonIgnore]
    public List<string> MaintenanceInsights { get; set; } = new List<string>();

    public string Emi { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    // Normalized city derived from Location. Optional so that existing
    // MongoDB documents (which predate this field) continue to load.
    [BsonIgnoreIfNull]
    public string? City { get; set; }
    public Specs Specs { get; set; } = new Specs();
    public List<string> Features { get; set; } = new List<string>();
    public List<string> Highlights { get; set; } = new List<string>();
    // Optional popularity signal used for relevance ranking. Existing MongoDB
    // documents (which predate this field) load with null and are treated as
    // neutral by the search scorer. No analytics/tracking is performed.
    [BsonIgnoreIfNull]
    public int? Popularity { get; set; }
    // Optional creation timestamp used for recency ranking. Existing MongoDB
    // documents load with null and receive a neutral recency score.
    [BsonIgnoreIfNull]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? CreatedAtUtc { get; set; }
}
