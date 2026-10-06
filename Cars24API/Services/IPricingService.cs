using Cars24API.Models;

namespace Cars24API.Services
{
    // Computes a deterministic "Recommended Price" for a car listing based on
    // regional demand and seasonal (monsoon) factors. The stored base Price is
    // never modified; the recommendation is returned separately so callers can
    // keep the original Price and the RecommendedPrice distinct.
    public interface IPricingService
    {
        // Returns the recommended price in whole INR, or null when the base
        // price cannot be safely parsed (graceful fallback).
        double? RecommendPrice(Car car, string? selectedCity, DateTime? asOf = null);
    }
}
