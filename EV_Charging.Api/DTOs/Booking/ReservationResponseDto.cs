/*
 * ReservationResponseDto.cs
 * IT22180384
 * Sandanima S. H. S.
 * 
 * Data Transfer Object for reservation response data.
 * Used to return reservation information to API clients.
 */

using System.Text.Json.Serialization;
using EV_Charging.Api.Models;

namespace EV_Charging.Api.DTOs.Booking
{
    public class ReservationResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string ChargingStationId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ReservationStatus Status { get; set; }
        public string? QrCode { get; set; }
        public string? OperatorId { get; set; }
        public string? OperatorProfileId { get; set; }
        public string? BookingId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? Notes { get; set; }
    }
}