using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace Cars24API.Models
{
    public class Booking
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }  // Booking ID
        public string CarId { get; set; } = null!;  // Reference to Car by Id
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string PreferredDate { get; set; }
        public string PreferredTime { get; set; }
        public string PaymentMethod { get; set; }
        public string LoanRequired { get; set; }
        public string DownPayment { get; set; }
    }
}
