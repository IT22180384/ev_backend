/*
 * ReservationCreateDto.cs
 * IT22180384
 * Sandanima S. H. S.
 * 
 * Data Transfer Object for creating new reservations.
 * Contains validation logic for reservation creation.
 */

using System.ComponentModel.DataAnnotations;

namespace EV_Charging.Api.DTOs.Booking
{
    public class ReservationCreateDto
    {
        // EV owner internal identifier - admin callers may omit this and use OwnerNic instead
        public string? UserId { get; set; }

        // Optional NIC identifier for backoffice/admin users to create reservations on behalf of EV owner
        public string? OwnerNic { get; set; }
        
        [Required]
        public string ChargingStationId { get; set; } = string.Empty;
        
        [Required]
        public DateTime StartTime { get; set; }
        
        [Required]
        public DateTime EndTime { get; set; }
        
        public string? Notes { get; set; }

        public bool IsWithinSevenDays()
        {
            // Check if reservation is within 7-day limit
            return StartTime <= DateTime.UtcNow.AddDays(7);
        }
    }
}
