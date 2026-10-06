using System.Globalization;
using System.Text.RegularExpressions;
using Cars24API.Models;

namespace Cars24API.Services
{
    // Deterministic maintenance-cost estimator for the internship feature.
    //
    // EstimatedMonthlyMaintenance = BaseModelCost x AgeMultiplier x KmMultiplier
    //
    // The brand/model cost matrix below is a deliberate, deterministic
    // demonstration for the internship and is NOT live service pricing. It
    // uses the vehicle's age (Specs.Year), kilometers driven (Specs.Km) and
    // brand/model (derived from Title) to produce a stable estimate.
    public class MaintenanceService : IMaintenanceService
    {
        // Brands are detected from the free-text Title using whole-word matching.
        // Order is not significant because word boundaries prevent overlaps.
        private static readonly string[] KnownBrands =
        {
            "Maruti", "Hyundai", "Honda", "Tata", "Mahindra", "Toyota",
            "Ford", "Renault", "Kia", "Volkswagen", "Skoda", "Nissan",
            "Datsun", "Chevrolet", "Mitsubishi", "MG", "Jeep", "Force",
        };

        // Fallback monthly base cost (INR/month) per brand, used when the model
        // is not present in the exact-model matrix below. All values are
        // internship estimates, not live service quotes.
        private static readonly IReadOnlyDictionary<string, double> BrandFallbackCosts =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["Maruti"] = 2800,
                ["Hyundai"] = 3200,
                ["Honda"] = 3300,
                ["Tata"] = 3200,
                ["Mahindra"] = 3800,
                ["Toyota"] = 4200,
                ["Ford"] = 3600,
                ["Renault"] = 3000,
                ["Kia"] = 3600,
                ["Volkswagen"] = 4000,
                ["Skoda"] = 4000,
                ["Nissan"] = 3200,
                ["Datsun"] = 2600,
                ["Chevrolet"] = 2800,
                ["Mitsubishi"] = 3800,
                ["MG"] = 3400,
                ["Jeep"] = 4200,
                ["Force"] = 3600,
            };

        // Used when neither the brand nor the model can be recognised.
        private const double GenericFallbackCost = 3000;

        // Exact model -> base monthly estimate (INR/month). Compound model names
        // (and shorter names that are substrings of longer ones) are listed
        // first so the most specific match wins.
        private sealed record ModelCost(string Brand, string Model, double BaseMonthly);

        private static readonly IReadOnlyList<ModelCost> ModelCosts = new List<ModelCost>
        {
            // Maruti Suzuki
            new("Maruti", "Grand Vitara", 3300),
            new("Maruti", "Wagon R", 2500),
            new("Maruti", "WagonR", 2500),
            new("Maruti", "S-Presso", 2300),
            new("Maruti", "S-Cross", 3300),
            new("Maruti", "A-Star", 2400),
            new("Maruti", "Swift", 2800),
            new("Maruti", "Baleno", 3000),
            new("Maruti", "Dzire", 2900),
            new("Maruti", "Alto", 2200),
            new("Maruti", "Celerio", 2400),
            new("Maruti", "Ertiga", 3300),
            new("Maruti", "Fronx", 3100),
            new("Maruti", "Brezza", 3200),
            new("Maruti", "Eeco", 2600),
            new("Maruti", "Ignis", 2700),
            new("Maruti", "Jimny", 3400),
            new("Maruti", "Ciaz", 3100),
            new("Maruti", "XL6", 3400),
            new("Maruti", "Ritz", 2500),
            new("Maruti", "Estilo", 2300),
            new("Maruti", "Zen", 2100),
            new("Maruti", "800", 2000),
            new("Maruti", "Omni", 2100),
            new("Maruti", "Gypsy", 2600),
            // Hyundai
            new("Hyundai", "i10", 2800),
            new("Hyundai", "i20", 3200),
            new("Hyundai", "Creta", 3600),
            new("Hyundai", "Venue", 3300),
            new("Hyundai", "Verna", 3400),
            new("Hyundai", "Santro", 2600),
            new("Hyundai", "Aura", 2900),
            new("Hyundai", "Exter", 3100),
            new("Hyundai", "Alcazar", 3800),
            new("Hyundai", "Tucson", 4500),
            new("Hyundai", "Kona", 3200),
            new("Hyundai", "Elantra", 4000),
            new("Hyundai", "Xcent", 2800),
            new("Hyundai", "Eon", 2400),
            new("Hyundai", "Getz", 2500),

            // Honda
            new("Honda", "City", 3500),
            new("Honda", "Amaze", 3100),
            new("Honda", "Jazz", 3200),
            new("Honda", "WR-V", 3300),
            new("Honda", "Elevate", 3400),
            new("Honda", "Mobilio", 3100),
            new("Honda", "Brio", 2800),
            new("Honda", "Civic", 4200),
            new("Honda", "Accord", 4800),
            new("Honda", "CR-V", 4500),

            // Tata
            new("Tata", "Tiago", 2700),
            new("Tata", "Altroz", 3000),
            new("Tata", "Nexon", 3400),
            new("Tata", "Punch", 3000),
            new("Tata", "Harrier", 4200),
            new("Tata", "Safari", 4400),
            new("Tata", "Tigor", 2800),
            new("Tata", "Indica", 2500),
            new("Tata", "Indigo", 2600),
            new("Tata", "Zest", 2700),
            new("Tata", "Bolt", 2800),
            new("Tata", "Hexa", 4000),
            new("Tata", "Nano", 2000),
            new("Tata", "Curvv", 3400),

            // Mahindra
            new("Mahindra", "XUV700", 4400),
            new("Mahindra", "XUV500", 3800),
            new("Mahindra", "XUV300", 3400),
            new("Mahindra", "Scorpio", 4200),
            new("Mahindra", "Thar", 4000),
            new("Mahindra", "Bolero", 3400),
            new("Mahindra", "TUV300", 3400),
            new("Mahindra", "KUV100", 3000),
            new("Mahindra", "Marazzo", 3600),

            // Toyota
            new("Toyota", "Innova Hycross", 4300),
            new("Toyota", "Innova Crysta", 4200),
            new("Toyota", "Innova", 4000),
            new("Toyota", "Fortuner", 5200),
            new("Toyota", "Urban Cruiser", 3200),
            new("Toyota", "Glanza", 3000),
            new("Toyota", "Corolla", 4200),
            new("Toyota", "Etios", 3000),
            new("Toyota", "Yaris", 3400),
            new("Toyota", "Hilux", 5000),
            // Ford
            new("Ford", "Endeavour", 4600),
            new("Ford", "EcoSport", 3600),
            new("Ford", "Aspire", 3000),
            new("Ford", "Freestyle", 3200),
            new("Ford", "Figo", 2800),
            new("Ford", "Fiesta", 3400),

            // Renault
            new("Renault", "Duster", 3400),
            new("Renault", "Triber", 2800),
            new("Renault", "Kiger", 3000),
            new("Renault", "Kwid", 2400),
            new("Renault", "Lodgy", 3200),

            // Kia
            new("Kia", "Carens", 3600),
            new("Kia", "Seltos", 3600),
            new("Kia", "Sonet", 3300),
            new("Kia", "Carnival", 5000),

            // Volkswagen
            new("Volkswagen", "Virtus", 4000),
            new("Volkswagen", "Taigun", 4000),
            new("Volkswagen", "Polo", 3400),
            new("Volkswagen", "Vento", 3600),
            new("Volkswagen", "Tiguan", 5200),

            // Skoda
            new("Skoda", "Slavia", 4200),
            new("Skoda", "Kushaq", 4200),
            new("Skoda", "Rapid", 3800),
            new("Skoda", "Octavia", 4800),

            // Nissan / Datsun
            new("Nissan", "Magnite", 3000),
            new("Nissan", "Kicks", 3400),
            new("Nissan", "Terrano", 3600),
            new("Nissan", "Sunny", 3200),
            new("Nissan", "Micra", 2800),
            new("Datsun", "redi-GO", 2400),
            new("Datsun", "GO+", 2600),
            new("Datsun", "GO", 2400),

            // Chevrolet
            new("Chevrolet", "Cruze", 4000),
            new("Chevrolet", "Beat", 2400),
            new("Chevrolet", "Sail", 2800),
            new("Chevrolet", "Spark", 2200),
            new("Chevrolet", "Enjoy", 2800),

            // Mitsubishi
            new("Mitsubishi", "Pajero", 4600),
            new("Mitsubishi", "Outlander", 4800),
            new("Mitsubishi", "Lancer", 3600),

            // MG / Jeep / Force
            new("MG", "Hector Plus", 4200),
            new("MG", "Hector", 4000),
            new("MG", "Astor", 3800),
            new("MG", "Gloster", 5200),
            new("Jeep", "Compass", 4600),
            new("Jeep", "Meridian", 5200),
            new("Jeep", "Wrangler", 6000),
            new("Force", "Gurkha", 4000),
        };

        // Service / wear thresholds (deterministic, documented estimates).
        private const int MajorServiceIntervalKm = 10000;
        private const int BrakePadKmThreshold = 40000;
        private const int TireKmThreshold = 45000;
        private const int TireAgeThreshold = 5;

        public MaintenanceEstimate Estimate(Car car, DateTime? asOf = null)
        {
            var now = asOf ?? DateTime.UtcNow.AddHours(5.5); // India Standard Time
            var age = ComputeAge(car.Specs.Year, now.Year);
            var km = ParseKm(car.Specs.Km) ?? 0;

            var baseCost = ResolveBaseMonthlyCost(car.Title);
            var multiplier = AgeMultiplier(age) * KmMultiplier(km);
            var monthly = Math.Round(baseCost * multiplier / 100.0, MidpointRounding.AwayFromZero) * 100.0;

            return new MaintenanceEstimate(
                monthly,
                DetermineStatus(age, km),
                BuildInsights(age, km));
        }

        // ============================ lookups ============================

        private static double ResolveBaseMonthlyCost(string title)
        {
            var brand = DetectBrand(title);
            if (brand == null)
            {
                return GenericFallbackCost;
            }

            foreach (var entry in ModelCosts)
            {
                if (string.Equals(entry.Brand, brand, StringComparison.OrdinalIgnoreCase) &&
                    title.Contains(entry.Model, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.BaseMonthly;
                }
            }

            return BrandFallbackCosts.TryGetValue(brand, out var brandCost)
                ? brandCost
                : GenericFallbackCost;
        }

        private static string? DetectBrand(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            foreach (var brand in KnownBrands)
            {
                if (Regex.IsMatch(title, $@"\b{Regex.Escape(brand)}\b", RegexOptions.IgnoreCase))
                {
                    return brand;
                }
            }

            return null;
        }

        private static int ComputeAge(int year, int currentYear) =>
            year > 1900 && year <= currentYear ? currentYear - year : 0;

        private static int? ParseKm(string? km)
        {
            if (string.IsNullOrWhiteSpace(km))
            {
                return null;
            }

            var digits = new string(km.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
        }

        // ============================ rules ============================

        // Age-based multiplier (older vehicles cost more to keep running).
        private static double AgeMultiplier(int age) => age switch
        {
            <= 2 => 1.00,
            <= 5 => 1.15,
            <= 8 => 1.35,
            <= 11 => 1.60,
            _ => 1.90,
        };

        // Mileage-based multiplier (higher km wears consumables faster).
        private static double KmMultiplier(int km) => km switch
        {
            <= 30000 => 1.00,
            <= 60000 => 1.10,
            <= 90000 => 1.25,
            <= 120000 => 1.40,
            _ => 1.60,
        };

        // Condition-based status. The internship example flags "High Maintenance
        // Expected" for vehicles that are roughly 6+ years old AND 80,000+ km.
        private static string DetermineStatus(int age, int km)
        {
            if (age >= 6 && km >= 80000)
            {
                return "High Maintenance Expected";
            }

            if (age >= 4 || km >= 50000)
            {
                return "Moderate Maintenance";
            }

            return "Low Maintenance";
        }

        // Deterministic, wording-light insights (no mechanic diagnosis).
        private static List<string> BuildInsights(int age, int km)
        {
            var insights = new List<string>();

            var nextService = MajorServiceIntervalKm - (km % MajorServiceIntervalKm);
            if (nextService == MajorServiceIntervalKm)
            {
                nextService = 0;
            }

            insights.Add(nextService == 0
                ? "Next major service is estimated due now"
                : $"Next major service estimated due in {nextService:N0} km");

            if (km >= BrakePadKmThreshold)
            {
                insights.Add("Brake pads likely to need replacement soon");
            }

            if (km >= TireKmThreshold || age >= TireAgeThreshold)
            {
                insights.Add("Tire replacement expected soon");
            }

            return insights;
        }
    }
}

