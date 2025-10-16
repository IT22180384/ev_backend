/*
 * StationDto.cs
 * IT22055026
 * Lakvindu U. G. V.
 * 
 * Data Transfer Objects for station management, booking operations, and operator management
 * Contains DTOs for API request/response handling across different modules
 */

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using EV_Charging.Api.Models;

namespace EV_Charging.Api.DTOs
{
    // API Response Wrapper
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string>? Errors { get; set; }
    }

    public class OperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    // ========== Authentication DTOs ==========
    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    // ========== Station DTOs ==========
    public class StationDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Type { get; set; } = string.Empty; // AC or DC
        public int TotalSlots { get; set; }
        public int AvailableSlots { get; set; }
        public bool IsActive { get; set; }
    }

    public class StationDetailDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Location Location { get; set; } = new();
        public string Type { get; set; } = string.Empty;
        public int TotalSlots { get; set; }
        public int AvailableSlots { get; set; }
        public List<Connector> Connectors { get; set; } = new();
        public List<DaySchedule> Schedule { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateStationDto
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Address { get; set; } = string.Empty;

        [Required]
        [Range(-90, 90)]
        public double Latitude { get; set; }

        [Required]
        [Range(-180, 180)]
        public double Longitude { get; set; }

        [Required]
        [RegularExpression("^(AC|DC)$", ErrorMessage = "Type must be either AC or DC")]
        public string Type { get; set; } = string.Empty;

        [Required]
        [Range(1, 50)]
        public int TotalSlots { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "At least one connector is required")]
        public List<Connector> Connectors { get; set; } = new();

        public List<DaySchedule>? Schedule { get; set; }
    }

    public class UpdateStationDto
    {
        [StringLength(100, MinimumLength = 3)]
        public string? Name { get; set; }

        public string? Address { get; set; }

        [Range(-90, 90)]
        public double? Latitude { get; set; }

        [Range(-180, 180)]
        public double? Longitude { get; set; }

        [Range(1, 50)]
        public int? TotalSlots { get; set; }

        public List<Connector>? Connectors { get; set; }

        public List<DaySchedule>? Schedule { get; set; }
    }

    public class StationFilterDto
    {
        public bool? IsActive { get; set; }
        public string? Type { get; set; }
        public string? SearchQuery { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? RadiusKm { get; set; }
    }

    public class TimeSlotDto
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int AvailableSlots { get; set; }
        public bool IsAvailable { get; set; }
    }

    // ========== Operator DTOs ==========
    public class QRScanRequest
    {
        [Required]
        public string QRPayload { get; set; } = string.Empty;
    }

    public class BookingScanDto
    {
        public string BookingId { get; set; } = string.Empty;
        public string StationName { get; set; } = string.Empty;
        public string StationAddress { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerNIC { get; set; } = string.Empty;
        public DateTime ReservationDateTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string ValidationMessage { get; set; } = string.Empty;
    }

    public class FinalizeSessionDto
    {
        [Required]
        [Range(0, 1000)]
        public double EnergyConsumedKWh { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }

    public class BookingSessionDto
    {
        public string BookingId { get; set; } = string.Empty;
        public string StationId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime ReservationDateTime { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? CheckInTime { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? CheckOutTime { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? EnergyConsumedKWh { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? SessionDurationMinutes { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SessionNotes { get; set; }
    }

    public class BookingSummaryDto
    {
        public string BookingId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerNIC { get; set; } = string.Empty;
        public DateTime ReservationDateTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? CheckInTime { get; set; }
    }

    // ========== Operator Management DTOs ==========
    public class OperatorDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string StationId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateOperatorDto
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string StationId { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Phone]
        public string Phone { get; set; } = string.Empty;
    }

    public class UpdateOperatorDto
    {
        [StringLength(100, MinimumLength = 2)]
        public string? Name { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [Phone]
        public string? Phone { get; set; }
    }

    // ========== Schedule Management DTOs ==========
    public class UpdateScheduleDto
    {
        [Required]
        public List<DaySchedule> Schedule { get; set; } = new();
    }
}