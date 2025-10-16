/*
 * User.cs
 * IT22363916
 * Perers N. D. V. O.
 * 
 * Defines the User data model for MongoDB storage (Admin/Backoffice and Station Operators)
 * Maps C# properties to MongoDB collection fields with role-based user management
 */
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EV_Charging.Api.Models
{
    public static class UserRoles
    {
        public const string Admin = "Admin";
        public const string StationOperator = "StationOperator";
    }

    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = string.Empty;
        
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string NIC { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = UserRoles.StationOperator;
        public bool IsActive { get; set; } = true;
        public string Phone { get; set; } = string.Empty;
        public string? StationId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}