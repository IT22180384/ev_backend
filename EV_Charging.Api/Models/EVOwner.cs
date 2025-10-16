/*
 * EVOwner.cs
 * IT22363916
 * Perers N. D. V. O.
 * 
 * Defines the EVOwner data model for MongoDB storage
 * Specific model for electric vehicle owners with their unique properties
 */
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EV_Charging.Api.Models
{
    public class EVOwner
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = string.Empty;
        
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string NIC { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string Phone { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}