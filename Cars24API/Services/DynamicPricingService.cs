using System.Globalization;
using System.Text.RegularExpressions;
using Cars24API.Models;

namespace Cars24API.Services
{
    // Deterministic dynamic-pricing engine for the internship feature.
    //
    // RecommendedPrice = BasePrice x RegionalMultiplier x SeasonalMultiplier
    //
    // The regional demand matrix and seasonal rules below are a deliberate,
    // deterministic demonstration of the requirement and are NOT live market
    // statistics. There is no live fuel-price feed: the "fuel price spike"
    // example is represented by a deterministic monsoon-season rule for small
    // hatchbacks located in high-demand metros (documented below).
    public class DynamicPricingService : IPricingService
    {
        private readonly LocationService _locationService;

        public DynamicPricingService(LocationService locationService)
        {
            _locationService = locationService;
        }

        // Regional demand tiers for the supported cities (reusing the
        // Geo-Fencing catalogue). High-demand metros get a slight premium;
        // neutral regions are 1.00; softer regions a slight discount.
        private static readonly IReadOnlyDictionary<string, double> RegionalDemand =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["New Delhi"] = 1.04,   // high-demand metro
                ["Mumbai"] = 1.04,      // high-demand metro
                ["Bangalore"] = 1.03,   // high-demand metro
                ["Hyderabad"] = 1.00,   // neutral
                ["Chennai"] = 1.00,     // neutral
                ["Pune"] = 1.00,        // neutral
                ["Kolkata"] = 0.97,     // softer demand
                ["Ahmedabad"] = 0.97,   // softer demand
            };

        // Body-type keyword classification derived from the free-text Title,
        // because the legacy Car model has no explicit body-type field.
        private static readonly string[] OffRoadKeywords =
        {
            "suv", "muv", "off-road", "offroad", "4x4", "jeep", "thar",
            "jimny", "scorpio", "bolero", "fortuner", "endeavour", "pajero",
            "gurkha"
        };

        private static readonly string[] SmallHatchbackKeywords =
        {
            "hatchback", "hatch", "alto", "kwid", "tiago", "i10", "celerio",
            "wagonr", "wagon r", "santro", "eon", "figo", "beat", "redigo"
        };

        // India monsoon window (June-September), used as the seasonal trigger.
        private const int MonsoonStartMonth = 6;
        private const int MonsoonEndMonth = 9;

        // Modest overall clamp to prevent extreme adjustments.
        private const double MinTotalMultiplier = 0.85;
        private const double MaxTotalMultiplier = 1.15;

        // SUV/MUV/off-road vehicles become more desirable during monsoon.
        private const double MonsoonOffRoadMultiplier = 1.05;

        // Fuel-pressure demo: small hatchbacks in high-demand metros lose a
        // little value during the monsoon window (deterministic stand-in for
        // a live fuel-price spike, which the project does not have).
        private const double FuelPressureHatchbackMultiplier = 0.93;

        public double? RecommendPrice(Car car, string? selectedCity, DateTime? asOf = null)
        {
            var basePrice = ParsePrice(car.Price);
            if (basePrice == null || basePrice.Value <= 0)
            {
                return null;
            }

            var date = asOf ?? IndiaNow;
            var city = ResolveCity(car, selectedCity);

            var regional = RegionalMultiplier(city);
            var seasonal = SeasonalMultiplier(car, date, city);

            var total = Math.Clamp(regional * seasonal, MinTotalMultiplier, MaxTotalMultiplier);
            var recommended = basePrice.Value * total;

            // Round to a practical INR value (nearest 1,000).
            return Math.Round(recommended / 1000.0, MidpointRounding.AwayFromZero) * 1000.0;
        }

        // Location priority: user-selected city first (when it resolves to a
        // supported city), otherwise the car's own city/location.
        private string? ResolveCity(Car car, string? selectedCity)
        {
            var selected = _locationService.NormalizeCity(selectedCity);
            return selected ?? _locationService.ResolveCarCity(car);
        }

        private static double RegionalMultiplier(string? city)
        {
            if (city == null)
            {
                return 1.00;
            }

            return RegionalDemand.TryGetValue(city, out var multiplier) ? multiplier : 1.00;
        }

        private static bool IsHighDemandMetro(string? city) =>
            city is "New Delhi" or "Mumbai" or "Bangalore";

        private static bool IsMonsoon(DateTime date) =>
            date.Month >= MonsoonStartMonth && date.Month <= MonsoonEndMonth;

        private static double SeasonalMultiplier(Car car, DateTime date, string? city)
        {
            // Outside the monsoon window the seasonal multiplier is neutral.
            if (!IsMonsoon(date))
            {
                return 1.00;
            }

            // Monsoon + SUV/MUV/off-road -> modest increase.
            if (IsOffRoadOrSuv(car))
            {
                return MonsoonOffRoadMultiplier;
            }

            // Monsoon + small hatchback in a high-demand metro -> modest
            // decrease (deterministic representation of a fuel-price spike).
            if (IsSmallHatchback(car) && IsHighDemandMetro(city))
            {
                return FuelPressureHatchbackMultiplier;
            }

            return 1.00;
        }

        private static bool IsOffRoadOrSuv(Car car) =>
            ContainsAny(car.Title, OffRoadKeywords);

        private static bool IsSmallHatchback(Car car) =>
            ContainsAny(car.Title, SmallHatchbackKeywords);

        private static bool ContainsAny(string? text, string[] keywords) =>
            !string.IsNullOrWhiteSpace(text) &&
            keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

        // Server-side "now" in India Standard Time so monsoon-month checks
        // follow the India calendar regardless of server timezone.
        private static DateTime IndiaNow => DateTime.UtcNow.AddHours(5.5);

        // Parses the legacy formatted price string ("5,00,000", "₹7.80 lakh",
        // "11.20 lakh", "1.5 crore", "780000", ...) into whole INR, or null
        // when the value is missing or cannot be parsed safely.
        private static double? ParsePrice(string? price)
        {
            if (string.IsNullOrWhiteSpace(price))
            {
                return null;
            }

            var p = price.Trim().ToLowerInvariant();

            double? parsed = null;
            var match = Regex.Match(p, @"\d+(\.\d+)?");
            if (match.Success)
            {
                var amount = double.Parse(match.Value, CultureInfo.InvariantCulture);
                if (p.Contains("lakh"))
                {
                    parsed = amount * 100000;
                }
                else if (p.Contains("crore"))
                {
                    parsed = amount * 10000000;
                }
            }

            if (parsed == null)
            {
                var digits = new string(p.Where(char.IsDigit).ToArray());
                parsed = double.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                    ? value
                    : null;
            }

            return parsed.HasValue && parsed.Value > 0 ? parsed : null;
        }
    }
}
