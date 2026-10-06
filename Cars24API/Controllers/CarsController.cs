using Microsoft.AspNetCore.Mvc;
using Cars24API.Models;
using Cars24API.Services;


namespace Cars24API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CarController : ControllerBase
    {
        private readonly CarService _carservice;
        private readonly LocationService _locationService;
        private readonly CarSearchService _carSearchService;
        private readonly IPricingService _pricingService;
        public CarController(CarService carService, LocationService locationService, CarSearchService carSearchService, IPricingService pricingService)
        {
            _carservice = carService;
            _locationService = locationService;
            _carSearchService = carSearchService;
            _pricingService = pricingService;
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var car = await _carservice.GetByIdAsync(id);
            if (car == null)
            {
                return NotFound();
            }
            // Detail view has no selected city filter, so pricing falls back to
            // the vehicle's own city/location.
            car.RecommendedPrice = _pricingService.RecommendPrice(car, selectedCity: null);
            return Ok(car);
        }
        [HttpGet("summaries")]
        public async Task<IActionResult> GetCarsummaries(
            [FromQuery] string? search,
            [FromQuery] string? city,
            [FromQuery] string[]? brand,
            [FromQuery] string? fuel,
            [FromQuery] string? transmission,
            [FromQuery] int? minKm,
            [FromQuery] int? maxKm,
            [FromQuery] int? minYear,
            [FromQuery] int? maxYear,
            [FromQuery] int? minPrice,
            [FromQuery] int? maxPrice,
            [FromQuery] string? sort)
        {
            var query = new CarSearchQuery(
                search,
                city,
                brand,
                fuel,
                transmission,
                minKm,
                maxKm,
                minYear,
                maxYear,
                minPrice,
                maxPrice,
                sort);

            var cars = await _carSearchService.SearchAsync(query);
            var result = cars.Select(car => new
            {
                id = car.Id,
                title = car.Title,
                km = car.Specs.Km,
                fuel = car.Specs.Fuel,
                transmission = car.Specs.Transmission,
                owner = car.Specs.Owner,
                year = car.Specs.Year,
                emi = car.Emi,
                price = car.Price,
                location = car.Location,
                city = _locationService.ResolveCarCity(car),
                image = car.Images.FirstOrDefault() ?? string.Empty,
                recommendedPrice = _pricingService.RecommendPrice(car, query.City)
            });
            return Ok(result);
        }

        [HttpGet("suggestions")]
        public async Task<IActionResult> GetSearchSuggestions()
        {
            return Ok(await _carSearchService.GetSuggestionsAsync());
        }
        [HttpGet("cities")]
        public IActionResult GetSupportedCities()
        {
            return Ok(_locationService.GetSupportedCities());
        }
        [HttpGet("hubs")]
        public IActionResult GetHubs([FromQuery] string? city)
        {
            var hubs = string.IsNullOrWhiteSpace(city)
                ? _locationService.GetHubs()
                : _locationService.GetHubs(city);
            return Ok(hubs);
        }
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Car car)
        {
            if (car == null)
            {
                return BadRequest("Car data is required");
            }
            // Store the normalized city alongside the free-text Location
            // for new records (existing documents are derived at read time).
            car.City = _locationService.NormalizeCity(car.Location);
            // Stamp creation time so new listings participate in recency ranking.
            car.CreatedAtUtc = DateTime.UtcNow;
            await _carservice.CreateAsync(car);
            return CreatedAtAction(nameof(GetById), new { id = car.Id }, car);
        }
    }
}
