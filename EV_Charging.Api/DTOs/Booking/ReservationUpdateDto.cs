/*
 * ReservationUpdateDto.cs
 * IT22180384
 * Sandanima S. H. S.
 * 
 * Data Transfer Object for updating reservation data.
 * Contains validation logic for reservation updates.
 */

using System.ComponentModel.DataAnnotations;
using EV_Charging.Api.Models;

namespace EV_Charging.Api.DTOs.Booking
{
    public class ReservationUpdateDto
    {
        public DateTime? StartTime { get; set; }
        
        public DateTime? EndTime { get; set; }
        
        public ReservationStatus? Status { get; set; }
        
        public string? Notes { get; set; }

        // Custom validation for 12-hour rule
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Validate reservation update business rules
            if (StartTime.HasValue && StartTime.Value <= DateTime.UtcNow.AddHours(12))
            {
                yield return new ValidationResult(
                    "Reservations can only be updated at least 12 hours before the start time.",
                    new[] { nameof(StartTime) });
            }

            if (StartTime.HasValue && StartTime.Value > DateTime.UtcNow.AddDays(7))
            {
                yield return new ValidationResult(
                    "Start time cannot be more than 7 days in the future.",
                    new[] { nameof(StartTime) });
            }

            if (StartTime.HasValue && EndTime.HasValue && EndTime.Value <= StartTime.Value)
            {
                yield return new ValidationResult(
                    "End time must be after start time.",
                    new[] { nameof(EndTime) });
            }
        }
    }
}