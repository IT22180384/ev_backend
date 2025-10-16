/*
 * Operator.cs
 * IT22267504
 * Methmini, K. A. T.
 * 
 * Operator model for managing station operators and their assignments
 */

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EV_Charging.Api.Models
{
    public class Operator
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonElement("userId")]
        public string UserId { get; set; } = string.Empty; // Reference to User with StationOperator role

        [BsonElement("stationId")]
        public string StationId { get; set; } = string.Empty; // Station this operator is assigned to

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("phone")]
        public string Phone { get; set; } = string.Empty;

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = true;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    // Simple working hours: 9 AM - 6 PM daily with lunch break 12-1 PM
    public static class OperatorWorkingHours
    {
        public static readonly TimeSpan StartTime = new(9, 0, 0);  // 9:00 AM
        public static readonly TimeSpan EndTime = new(18, 0, 0);   // 6:00 PM
        public static readonly TimeSpan LunchStart = new(12, 0, 0); // 12:00 PM
        public static readonly TimeSpan LunchEnd = new(13, 0, 0);   // 1:00 PM
    }
}