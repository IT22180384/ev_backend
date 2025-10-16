/*
 * Booking.cs
 * IT22180384
 * Sandanima S. H. S.
 * 
 * Booking model for managing charging station reservations
 */

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EV_Charging.Api.Models
{
    public class Booking
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonElement("userId")]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("stationId")]
        public string StationId { get; set; } = string.Empty;

        [BsonElement("reservationDateTime")]
        public DateTime ReservationDateTime { get; set; }

        [BsonElement("status")]
        [BsonRepresentation(BsonType.String)]
        public BookingStatus Status { get; set; } = BookingStatus.Pending;

        [BsonElement("operatorId")]
        public string? OperatorId { get; set; }

        [BsonElement("checkInTime")]
        public DateTime? CheckInTime { get; set; }

        [BsonElement("checkOutTime")]
        public DateTime? CheckOutTime { get; set; }

        [BsonElement("energyConsumedKWh")]
        public double? EnergyConsumedKWh { get; set; }

        [BsonElement("sessionDurationMinutes")]
        public int? SessionDurationMinutes { get; set; }

        [BsonElement("sessionNotes")]
        public string? SessionNotes { get; set; }

        [BsonElement("qrCode")]
        public string? QRCode { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum BookingStatus
    {
        Pending,
        Approved,
        InProgress,
        Completed,
        Cancelled,
        NoShow
    }
}