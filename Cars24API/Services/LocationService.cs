using Cars24API.Models;

namespace Cars24API.Services
{
    public record SupportedCity(string Name, double Latitude, double Longitude);

    // Provides the finite catalogue of supported cities (matching the
    // popularCities list already used by the sell-car flow) plus a small
    // seed catalogue of service centers / pickup hubs for the map.
    public class LocationService
    {
        private static readonly List<SupportedCity> _cities = new()
        {
            new SupportedCity("New Delhi", 28.6139, 77.2090),
            new SupportedCity("Mumbai", 19.0760, 72.8777),
            new SupportedCity("Bangalore", 12.9716, 77.5946),
            new SupportedCity("Hyderabad", 17.3850, 78.4867),
            new SupportedCity("Chennai", 13.0827, 80.2707),
            new SupportedCity("Kolkata", 22.5726, 88.3639),
            new SupportedCity("Pune", 18.5204, 73.8567),
            new SupportedCity("Ahmedabad", 23.0225, 72.5714),
        };

        // Alternate spellings / older names that may appear inside the
        // free-text Car.Location values of existing documents.
        private static readonly Dictionary<string, string[]> _aliases =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["New Delhi"] = new[] { "delhi", "ncr" },
                ["Mumbai"] = new[] { "bombay" },
                ["Bangalore"] = new[] { "bengaluru" },
            };

        // Small finite seed catalogue of service centers / pickup hubs.
        // Two touchpoints per supported city are enough for the map.
        private static readonly List<Hub> _hubs = new()
        {
            new Hub { Name = "CARS24 Hub Rohini", Type = "Hub", City = "New Delhi", Address = "Metro Walk, Rohini, New Delhi", Latitude = 28.7041, Longitude = 77.1125 },
            new Hub { Name = "CARS24 Service Center Okhla", Type = "ServiceCenter", City = "New Delhi", Address = "Okhla Industrial Area Phase II, New Delhi", Latitude = 28.5308, Longitude = 77.2713 },
            new Hub { Name = "CARS24 Pickup Point Andheri", Type = "PickupPoint", City = "Mumbai", Address = "Veera Desai Road, Andheri West, Mumbai", Latitude = 19.1364, Longitude = 72.8296 },
            new Hub { Name = "CARS24 Service Center Thane", Type = "ServiceCenter", City = "Mumbai", Address = "Ghodbunder Road, Thane West, Mumbai", Latitude = 19.2183, Longitude = 72.9781 },
            new Hub { Name = "CARS24 Hub Whitefield", Type = "Hub", City = "Bangalore", Address = "ITPL Main Road, Whitefield, Bangalore", Latitude = 12.9698, Longitude = 77.7500 },
            new Hub { Name = "CARS24 Pickup Point Koramangala", Type = "PickupPoint", City = "Bangalore", Address = "80 Feet Road, Koramangala, Bangalore", Latitude = 12.9352, Longitude = 77.6245 },
            new Hub { Name = "CARS24 Hub Hitech City", Type = "Hub", City = "Hyderabad", Address = "Hitech City Main Road, Hyderabad", Latitude = 17.4435, Longitude = 78.3772 },
            new Hub { Name = "CARS24 Service Center Secunderabad", Type = "ServiceCenter", City = "Hyderabad", Address = "SP Road, Secunderabad, Hyderabad", Latitude = 17.4399, Longitude = 78.4983 },
            new Hub { Name = "CARS24 Hub OMR Chennai", Type = "Hub", City = "Chennai", Address = "Old Mahabalipuram Road, Thoraipakkam, Chennai", Latitude = 12.9416, Longitude = 80.2363 },
            new Hub { Name = "CARS24 Pickup Point Anna Nagar", Type = "PickupPoint", City = "Chennai", Address = "2nd Avenue, Anna Nagar, Chennai", Latitude = 13.0850, Longitude = 80.2101 },
            new Hub { Name = "CARS24 Hub Salt Lake", Type = "Hub", City = "Kolkata", Address = "Sector V, Salt Lake, Kolkata", Latitude = 22.5726, Longitude = 88.4330 },
            new Hub { Name = "CARS24 Service Center EM Bypass", Type = "ServiceCenter", City = "Kolkata", Address = "EM Bypass, Kasba, Kolkata", Latitude = 22.5190, Longitude = 88.4020 },
            new Hub { Name = "CARS24 Hub Baner", Type = "Hub", City = "Pune", Address = "Baner Road, Baner, Pune", Latitude = 18.5590, Longitude = 73.7868 },
            new Hub { Name = "CARS24 Pickup Point Wakad", Type = "PickupPoint", City = "Pune", Address = "Mumbai-Bangalore Highway, Wakad, Pune", Latitude = 18.5990, Longitude = 73.7620 },
            new Hub { Name = "CARS24 Hub SG Highway", Type = "Hub", City = "Ahmedabad", Address = "SG Highway, Bodakdev, Ahmedabad", Latitude = 23.0396, Longitude = 72.5084 },
            new Hub { Name = "CARS24 Service Center Maninagar", Type = "ServiceCenter", City = "Ahmedabad", Address = "Maninagar East, Ahmedabad", Latitude = 22.9969, Longitude = 72.6028 },
        };

        public IReadOnlyList<SupportedCity> GetSupportedCities() => _cities;

        public IReadOnlyList<Hub> GetHubs() => _hubs;

        public IEnumerable<Hub> GetHubs(string? city)
        {
            var normalized = NormalizeCity(city);
            if (normalized == null)
            {
                return Enumerable.Empty<Hub>();
            }
            return _hubs.Where(h => string.Equals(h.City, normalized, StringComparison.OrdinalIgnoreCase));
        }

        // Derives the normalized supported city from a free-text location
        // string (or an already-normalized city value). Returns null when
        // the location does not match any supported city.
        public string? NormalizeCity(string? location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return null;
            }
            var trimmed = location.Trim();
            foreach (var city in _cities)
            {
                if (trimmed.Contains(city.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return city.Name;
                }
            }
            foreach (var city in _cities)
            {
                if (_aliases.TryGetValue(city.Name, out var aliases) &&
                    aliases.Any(a => trimmed.Contains(a, StringComparison.OrdinalIgnoreCase)))
                {
                    return city.Name;
                }
            }
            return null;
        }

        // City for a car document: prefer the stored City value (new
        // records), otherwise derive it from the free-text Location so
        // that existing documents keep working without migration.
        public string? ResolveCarCity(Car car) =>
            NormalizeCity(car.City) ?? NormalizeCity(car.Location);
    }
}
