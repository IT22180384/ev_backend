/*
 * Reservation.cs
 * IT22180384
 * Sandanima S. H. S.
 * 
 * Model class representing a charging station reservation.
 * Contains business logic for reservation validation and status management.
 */

using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EV_Charging.Api.Models
{
    public class Reservation
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [Required]
        public string ChargingStationId { get; set; } = string.Empty;
        
        [Required]
        public DateTime StartTime { get; set; }
        
        [Required]
        public DateTime EndTime { get; set; }
        
        [BsonRepresentation(BsonType.String)]
        public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
        
        public string? QrCode { get; set; }

        [BsonElement("operatorId")]
        public string? OperatorId { get; set; }

        [BsonElement("operatorUserId")]
        public string? OperatorUserId { get; set; }

        [BsonElement("bookingId")]
        public string? BookingId { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? UpdatedAt { get; set; }
        
        public string? Notes { get; set; }

        // Business rule validations
        public bool CanBeUpdated()
        {
            // Check if reservation can be updated based on status and 12-hour rule
            return Status != ReservationStatus.Completed && 
                   Status != ReservationStatus.Cancelled &&
                   StartTime > DateTime.UtcNow.AddHours(12);
        }

        public bool CanBeCancelled()
        {
            // Check if reservation can be cancelled based on status and 12-hour rule
            return Status != ReservationStatus.Completed && 
                   Status != ReservationStatus.Cancelled &&
                   StartTime > DateTime.UtcNow.AddHours(12);
        }

        public bool IsWithinSevenDays()
        {
            // Check if reservation start time is within 7 days from now
            return StartTime <= DateTime.UtcNow.AddDays(7);
        }
    }

    public enum ReservationStatus
    {
        Pending,
        Confirmed,
        Active,
        Completed,
        Cancelled
    }
}