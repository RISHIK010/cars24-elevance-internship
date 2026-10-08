using System.Globalization;
using System.Text.RegularExpressions;
using Cars24API.Models;

namespace Cars24API.Services
{
    // Implements the Advanced Search pipeline for car listings:
    //   1. hard filtering (fuel / transmission / brand / km / year / price / city)
    //   2. typo-tolerant keyword matching (exact > prefix > substring > fuzzy)
    //   3. deterministic relevance scoring (keyword >> filter >> popularity >> recency)
    //
    // Matching is done in-memory over the existing car collection. The dataset is
    // the small internship catalogue, so no external search engine/index is used.
    public class CarSearchService
    {
        private readonly CarService _carService;
        private readonly LocationService _locationService;

        public CarSearchService(CarService carService, LocationService locationService)
        {
            _carService = carService;
            _locationService = locationService;
        }

        // Keyword field weights. Make/model/title is the strongest signal;
        // location/city and fuel/transmission are secondary.
        private static readonly Dictionary<string, double> FieldWeights =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Title"] = 1.0,
                ["Location"] = 0.55,
                ["City"] = 0.55,
                ["Fuel"] = 0.40,
                ["Transmission"] = 0.40,
            };

        public async Task<List<Car>> SearchAsync(CarSearchQuery query)
        {
            var cars = await _carService.GetAllAsync();
            var tokens = Tokenize(query.Search);
            var hasSearch = tokens.Count > 0;

            var scored = new List<(Car Car, double Score)>();
            foreach (var car in cars)
            {
                if (!MatchesHardFilters(car, query))
                {
                    continue;
                }

                if (hasSearch)
                {
                    // A keyword search must actually match the vehicle; never
                    // surface every listing just because a search term exists.
                    var keyword = ScoreKeywords(car, tokens);
                    if (keyword <= 0)
                    {
                        continue;
                    }

                    scored.Add((car, ComputeRelevance(car, query, keyword)));
                }
                else
                {
                    // No keyword search: preserve the original listing order.
                    scored.Add((car, 0));
                }
            }

            // Guarantee one result per unique car so the UI never renders
            // duplicate cards for the same vehicle.
            return ApplySort(scored, query, hasSearch)
                .Select(x => x.Car)
                .GroupBy(c => c.Id)
                .Select(g => g.First())
                .ToList();
        }

        // Builds a small, real-data suggestion corpus for predictive search.
        public async Task<List<CarSuggestion>> GetSuggestionsAsync()
        {
            var suggestions = new List<CarSuggestion>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Suggestions are derived exclusively from the car catalogue so the
            // search box surfaces vehicles (title/brand/model) rather than
            // cities or other non-vehicle terms.
            var cars = await _carService.GetAllAsync();
            foreach (var car in cars)
            {
                AddSuggestion(suggestions, seen, car.Title, "Vehicle");

                var (brand, model) = ExtractBrandModel(car.Title);
                AddSuggestion(suggestions, seen, brand, "Brand");
                AddSuggestion(suggestions, seen, model, "Model");
            }

            return suggestions;
        }

        private static void AddSuggestion(
            List<CarSuggestion> list,
            HashSet<string> seen,
            string? value,
            string category)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var cleaned = value.Trim();
            if (cleaned.Length < 2 || cleaned.All(char.IsDigit))
            {
                return;
            }

            if (!seen.Add(cleaned.ToLowerInvariant()))
            {
                return;
            }

            list.Add(new CarSuggestion(cleaned, category));
        }

        // From a free-text title derive a likely brand (make) and model word.
        // Titles follow the sell-car convention "make model variant year fuel".
        private static (string? Brand, string? Model) ExtractBrandModel(string title)
        {
            var words = Normalize(title)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 2 && !w.All(char.IsDigit))
                .ToList();

            var brand = words.Count > 0 ? TitleCase(words[0]) : null;
            var model = words.Count > 1 ? TitleCase(words[1]) : null;
            return (brand, model);
        }
        private List<(Car Car, double Score)> ApplySort(
            List<(Car Car, double Score)> items,
            CarSearchQuery query,
            bool hasSearch)
        {
            var sort = query.Sort?.Trim().ToLowerInvariant();
            switch (sort)
            {
                case "price_asc":
                    return items
                        .OrderBy(x => ParsePrice(x.Car.Price) ?? double.MaxValue)
                        .ThenByDescending(x => x.Score)
                        .ToList();
                case "price_desc":
                    return items
                        .OrderByDescending(x => ParsePrice(x.Car.Price) ?? double.MinValue)
                        .ThenByDescending(x => x.Score)
                        .ToList();
                case "km_asc":
                    return items
                        .OrderBy(x => ParseKm(x.Car.Specs.Km) ?? int.MaxValue)
                        .ThenByDescending(x => x.Score)
                        .ToList();
                case "km_desc":
                    return items
                        .OrderByDescending(x => ParseKm(x.Car.Specs.Km) ?? int.MinValue)
                        .ThenByDescending(x => x.Score)
                        .ToList();
                case "year_desc":
                    return items
                        .OrderByDescending(x => x.Car.Specs.Year)
                        .ThenByDescending(x => x.Score)
                        .ToList();
                case "year_asc":
                    return items
                        .OrderBy(x => x.Car.Specs.Year)
                        .ThenByDescending(x => x.Score)
                        .ToList();
                default:
                    // "relevance" (or no sort): rank by relevance only when a
                    // keyword search is active; otherwise preserve listing order.
                    return hasSearch
                        ? items.OrderByDescending(x => x.Score).ToList()
                        : items;
            }
        }

        private bool MatchesHardFilters(Car car, CarSearchQuery query)
        {
            // Geo-fence city (reuses LocationService, no behavior change).
            if (!string.IsNullOrWhiteSpace(query.City))
            {
                var normalizedCity = _locationService.NormalizeCity(query.City);
                if (normalizedCity != null &&
                    !string.Equals(
                        _locationService.ResolveCarCity(car),
                        normalizedCity,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(query.Fuel) &&
                !EqualsNorm(car.Specs.Fuel, query.Fuel))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(query.Transmission) &&
                !EqualsNorm(car.Specs.Transmission, query.Transmission))
            {
                return false;
            }

            if (query.Brands is { Count: > 0 })
            {
                var title = Normalize(car.Title);
                var matched = query.Brands.Any(b =>
                    !string.IsNullOrWhiteSpace(b) &&
                    title.Contains(Normalize(b), StringComparison.Ordinal));
                if (!matched)
                {
                    return false;
                }
            }

            var year = car.Specs.Year;
            if (query.MinYear.HasValue && year < query.MinYear.Value) return false;
            if (query.MaxYear.HasValue && year > query.MaxYear.Value) return false;

            var km = ParseKm(car.Specs.Km);
            if (km.HasValue)
            {
                if (query.MinKm.HasValue && km.Value < query.MinKm.Value) return false;
                if (query.MaxKm.HasValue && km.Value > query.MaxKm.Value) return false;
            }

            var price = ParsePrice(car.Price);
            if (price.HasValue)
            {
                if (query.MinPrice.HasValue && price.Value < query.MinPrice.Value) return false;
                if (query.MaxPrice.HasValue && price.Value > query.MaxPrice.Value) return false;
            }

            return true;
        }
        // ============================= scoring =============================

        private double ComputeRelevance(Car car, CarSearchQuery query, double keywordScore)
        {
            // Conceptual priority: keyword >> filter alignment >> popularity >> recency.
            var filter = ScoreFilterAlignment(car, query);
            var popularity = ScorePopularity(car);
            var recency = ScoreRecency(car);
            return keywordScore + filter + popularity + recency;
        }

        private double ScoreKeywords(Car car, List<string> tokens)
        {
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Title"] = Normalize(car.Title),
                ["Location"] = Normalize(car.Location),
                ["City"] = Normalize(_locationService.ResolveCarCity(car) ?? string.Empty),
                ["Fuel"] = Normalize(car.Specs.Fuel),
                ["Transmission"] = Normalize(car.Specs.Transmission),
            };

            var total = 0d;
            var matchedTokens = 0;
            foreach (var token in tokens)
            {
                var best = 0d;
                foreach (var field in fields)
                {
                    var tier = GetMatchTier(token, field.Value);
                    if (tier == MatchTier.None)
                    {
                        continue;
                    }

                    var candidate = TierScore(tier) * FieldWeights[field.Key];
                    if (candidate > best)
                    {
                        best = candidate;
                    }
                }

                total += best;
                if (best > 0)
                {
                    matchedTokens++;
                }
            }

            // Reward a result that satisfies every term of a multi-term query.
            if (tokens.Count >= 2 && matchedTokens == tokens.Count)
            {
                total += 15;
            }

            return total;
        }

        private double ScoreFilterAlignment(Car car, CarSearchQuery query)
        {
            var score = 0d;

            // Discrete filters are hard-applied above; award a modest bonus for
            // each active discrete filter so filtered results align upward.
            if (!string.IsNullOrWhiteSpace(query.Fuel)) score += 8;
            if (!string.IsNullOrWhiteSpace(query.Transmission)) score += 8;
            if (query.Brands is { Count: > 0 }) score += 8;

            // Range filters reward closeness to the user's preferred bound:
            // newer year and lower km are preferred.
            var year = car.Specs.Year;
            if (query.MinYear.HasValue && query.MaxYear.HasValue &&
                query.MaxYear.Value > query.MinYear.Value)
            {
                var span = (double)(query.MaxYear.Value - query.MinYear.Value);
                var pos = (year - query.MinYear.Value) / span;
                score += 6 * Math.Clamp(pos, 0, 1);
            }

            var km = ParseKm(car.Specs.Km);
            if (km.HasValue && query.MinKm.HasValue && query.MaxKm.HasValue &&
                query.MaxKm.Value > query.MinKm.Value)
            {
                var span = (double)(query.MaxKm.Value - query.MinKm.Value);
                var pos = (km.Value - query.MinKm.Value) / span;
                score += 6 * (1 - Math.Clamp(pos, 0, 1));
            }

            return score;
        }

        private static double ScorePopularity(Car car)
        {
            // No analytics/tracking system exists. For legacy documents without a
            // stored value we fall back to a deterministic content-richness proxy
            // (features + highlights count) so ranking stays stable and bounded.
            var popularity = car.Popularity ?? Math.Max(0, car.Features.Count + car.Highlights.Count);
            return Math.Min(popularity, 10) * 1.2; // 0..12 points
        }

        private static double ScoreRecency(Car car)
        {
            if (!car.CreatedAtUtc.HasValue)
            {
                return 0;
            }

            var ageDays = Math.Max(0, (DateTime.UtcNow - car.CreatedAtUtc.Value).TotalDays);
            return Math.Max(0, 8 - ageDays / 45); // 0..8 points, decays over ~1 year
        }
        // ============================ matching ============================

        private enum MatchTier
        {
            None,
            Fuzzy,
            Substring,
            Prefix,
            Exact,
        }

        private static MatchTier GetMatchTier(string token, string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return MatchTier.None;
            }

            if (string.Equals(field, token, StringComparison.Ordinal))
            {
                return MatchTier.Exact;
            }

            // Short tokens (1 char) are only allowed to match exactly.
            if (token.Length >= 2 && field.StartsWith(token, StringComparison.Ordinal))
            {
                return MatchTier.Prefix;
            }

            if (token.Length >= 2 && field.Contains(token, StringComparison.Ordinal))
            {
                return MatchTier.Substring;
            }

            // Only compute fuzzy distance when no stronger match exists. Very short
            // tokens never fuzzy-match (avoids matching unrelated short words).
            if (token.Length >= 4)
            {
                var maxDistance = token.Length <= 5 ? 1 : 2;
                if (LevenshteinDistance(token, field) <= maxDistance)
                {
                    return MatchTier.Fuzzy;
                }
            }

            return MatchTier.None;
        }

        private static double TierScore(MatchTier tier) => tier switch
        {
            MatchTier.Exact => 100,
            MatchTier.Prefix => 75,
            MatchTier.Substring => 55,
            MatchTier.Fuzzy => 30,
            _ => 0,
        };

        private static int LevenshteinDistance(string a, string b)
        {
            var prev = new int[b.Length + 1];
            var curr = new int[b.Length + 1];
            for (var j = 0; j <= b.Length; j++)
            {
                prev[j] = j;
            }

            for (var i = 1; i <= a.Length; i++)
            {
                curr[0] = i;
                for (var j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(
                        Math.Min(curr[j - 1] + 1, prev[j] + 1),
                        prev[j - 1] + cost);
                }

                (prev, curr) = (curr, prev);
            }

            return prev[b.Length];
        }

        // ============================ helpers =============================

        private static List<string> Tokenize(string? search)
        {
            var normalized = Normalize(search);
            if (normalized.Length == 0)
            {
                return new List<string>();
            }

            return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ");
        }

        private static bool EqualsNorm(string? a, string? b) =>
            string.Equals(Normalize(a), Normalize(b), StringComparison.Ordinal);

        private static string? TitleCase(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var v = value.Trim().ToLowerInvariant();
            return char.ToUpperInvariant(v[0]) + v.Substring(1);
        }

        private static int? ParseKm(string? km)
        {
            if (string.IsNullOrWhiteSpace(km))
            {
                return null;
            }

            var digits = new string(km.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var v)
                ? v
                : null;
        }

        // Handles "7.80 lakh", "11.20 lakh", "5,00,000", "780000", etc.
        private static double? ParsePrice(string? price)
        {
            if (string.IsNullOrWhiteSpace(price))
            {
                return null;
            }

            var p = price.Trim().ToLowerInvariant();
            var match = Regex.Match(p, @"\d+(\.\d+)?");
            if (match.Success)
            {
                var amount = double.Parse(match.Value, CultureInfo.InvariantCulture);
                if (p.Contains("lakh"))
                {
                    return amount * 100000;
                }

                if (p.Contains("crore"))
                {
                    return amount * 10000000;
                }
            }

            var digits = new string(p.Where(char.IsDigit).ToArray());
            return double.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var v)
                ? v
                : null;
        }
    }
}



