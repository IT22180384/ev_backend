/*
 * Station.cs
 * IT22055026
 * Lakvindu U. G. V.
 * 
 * Station model with location, connectors, and scheduling
 */

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EV_Charging.Api.Models
{
    public class Station
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("location")]
        public Location Location { get; set; } = new();

        [BsonElement("type")]
        public string Type { get; set; } = string.Empty; // AC or DC

        [BsonElement("totalSlots")]
        public int TotalSlots { get; set; }

        [BsonElement("availableSlots")]
        public int AvailableSlots { get; set; }

        [BsonElement("connectors")]
        public List<Connector> Connectors { get; set; } = new();

        [BsonElement("schedule")]
        public List<DaySchedule> Schedule { get; set; } = new();

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = true;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Location
    {
        [BsonElement("address")]
        public string Address { get; set; } = string.Empty;

        [BsonElement("latitude")]
        public double Latitude { get; set; }

        [BsonElement("longitude")]
        public double Longitude { get; set; }
    }

    public class Connector
    {
        [BsonElement("type")]
        public string Type { get; set; } = string.Empty;

        [BsonElement("power")]
        public double PowerKW { get; set; }

        [BsonElement("count")]
        public int Count { get; set; }
    }

    public class DaySchedule
    {
        [BsonElement("dayOfWeek")]
        public string DayOfWeek { get; set; } = string.Empty;

        [BsonElement("isOpen")]
        public bool IsOpen { get; set; }

        [BsonElement("openTime")]
        public string OpenTime { get; set; } = string.Empty;

        [BsonElement("closeTime")]
        public string CloseTime { get; set; } = string.Empty;
    }
}