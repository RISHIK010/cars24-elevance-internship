using Cars24API.Models;
using MongoDB.Driver;

namespace Cars24API.Services
{
    public class CarService
    {
        private readonly IMongoCollection<Car> _cars;
        public CarService(IConfiguration config)
        {
            var client = new MongoClient(config["MongoDB:ConnectionString"]);

            var database = client.GetDatabase(config["MongoDB:DatabaseName"]);
            _cars = database.GetCollection<Car>("Cars");
        }
        public async Task<List<Car>> GetAllAsync() =>
            await _cars.Find(_ => true).ToListAsync();
        public async Task<Car?> GetByIdAsync(string id)
        {
            return await _cars.Find(u => u.Id == id).FirstOrDefaultAsync();
        }
        public async Task CreateAsync(Car car) =>
            await _cars.InsertOneAsync(car);

        public async Task<(Car? car, bool priceReduced)> UpdatePriceAsync(string id, string newPrice)
        {
            var car = await GetByIdAsync(id);
            if (car == null)
            {
                return (null, false);
            }

            var oldPrice = ParsePrice(car.Price);
            var newValue = ParsePrice(newPrice);

            car.Price = newPrice;
            await _cars.ReplaceOneAsync(c => c.Id == id, car);

            return (car, newValue < oldPrice);
        }

        // Best-effort numeric extraction for prices stored as free text
        // (e.g. "₹7.80 lakh", "₹8,50,000"). Used only for comparison; the
        // stored string is never reformatted.
        private static double ParsePrice(string price)
        {
            if (string.IsNullOrWhiteSpace(price))
            {
                return 0;
            }

            var digits = new string(price.Where(char.IsDigit).ToArray());
            return double.TryParse(digits, out var value) ? value : 0;
        }
    }

}