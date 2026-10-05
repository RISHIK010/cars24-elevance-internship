namespace Cars24API.Models;

// Structured query shape accepted by the car summaries/search endpoint.
// All fields are optional so existing callers (plain /api/Car/summaries)
// keep working unchanged.
public record CarSearchQuery(
    string? Search,
    string? City,
    IReadOnlyList<string>? Brands,
    string? Fuel,
    string? Transmission,
    int? MinKm,
    int? MaxKm,
    int? MinYear,
    int? MaxYear,
    int? MinPrice,
    int? MaxPrice,
    string? Sort);
