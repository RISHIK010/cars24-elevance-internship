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
        public CarController(CarService carService, LocationService locationService)
        {
            _carservice = carService;
            _locationService = locationService;
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var car = await _carservice.GetByIdAsync(id);
            if (car == null)
            {
                return NotFound();
            }
            return Ok(car);
        }
        [HttpGet("summaries")]
        public async Task<IActionResult> GetCarsummaries([FromQuery] string? city)
        {
            var cars = await _carservice.GetAllAsync();
            var normalizedCity = _locationService.NormalizeCity(city);
            if (normalizedCity != null)
            {
                // Geo-fence: keep only listings whose resolved city matches.
                cars = cars
                    .Where(car => string.Equals(
                        _locationService.ResolveCarCity(car),
                        normalizedCity,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            var result = cars.Select(car => new
            {
                car.Id,
                car.Title,
                km = car.Specs.Km,
                Fuel = car.Specs.Fuel,
                Transmission = car.Specs.Transmission,
                Owner = car.Specs.Owner,
                car.Emi,
                car.Price,
                car.Location,
                City = _locationService.ResolveCarCity(car),
                image = car.Images
            });
            return Ok(result);
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
            await _carservice.CreateAsync(car);
            return CreatedAtAction(nameof(GetById), new { id = car.Id }, car);
        }
    }
}
