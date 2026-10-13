using Cars24API.Models;
using MongoDB.Driver;

namespace Cars24API.Services
{
    public class BookingService
    {
        private readonly IMongoCollection<Booking> _bookings;
        public BookingService(IConfiguration config)
        {
            var client = new MongoClient(config["MongoDB:ConnectionString"]);
            var database = client.GetDatabase(config["MongoDB:DatabaseName"]);
            _bookings = database.GetCollection<Booking>("Bookings");
        }
        public async Task CreateAsync(Booking booking)
        {
            await _bookings.InsertOneAsync(booking);
        }

        public async Task<Booking> GetByIdAsynch(string id)
        {
            return await _bookings.Find(a => a.Id == id).FirstOrDefaultAsync();
        }

        // Marks a purchase booking as completed. Returns true when the booking
        // is (or already was) completed, and false when it does not exist.
        // The transition is an atomic conditional update, so concurrent and
        // retried completion requests are safe and never transition backwards.
        public async Task<bool> MarkCompletedAsync(string id)
        {
            var update = Builders<Booking>.Update.Set(b => b.Status, "completed");
            var result = await _bookings.UpdateOneAsync(
                Builders<Booking>.Filter.And(
                    Builders<Booking>.Filter.Eq(b => b.Id, id),
                    Builders<Booking>.Filter.Ne(b => b.Status, "completed")),
                update);

            if (result.MatchedCount > 0)
                return true;

            // Either the booking does not exist, or it was already completed.
            var booking = await _bookings.Find(b => b.Id == id).FirstOrDefaultAsync();
            return booking != null;
        }

        public async Task<List<Booking>> GetAllAsync()
        {
            return await _bookings.Find(_ => true).ToListAsync();
        }
    }
}