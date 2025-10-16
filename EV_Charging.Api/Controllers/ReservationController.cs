/*
 * ReservationController.cs
 * IT22180384
 * Sandanima S. H. S.
 * 
 * API controller for managing charging station reservations and bookings.
 * Provides CRUD operations and business logic for reservation management.
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EV_Charging.Api.DTOs.Booking;
using EV_Charging.Api.Services;
using EV_Charging.Api.DTOs;
using System.Security.Claims;
using System;

namespace EV_Charging.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReservationController : ControllerBase
    {
        private readonly IReservationService _reservationService;

        public ReservationController(IReservationService reservationService)
        {
            _reservationService = reservationService;
        }

        private bool IsAdmin => User.IsInRole("Admin");
        private bool IsStationOperator => User.IsInRole("StationOperator");
        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
        private string? CurrentNic => User.Claims.FirstOrDefault(c => c.Type == "NIC")?.Value;

        // Retrieves all reservations from the system (Admin only)
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<ReservationResponseDto>>> GetReservations()
        {
            var reservations = await _reservationService.GetAllReservationsAsync();
            return Ok(reservations);
        }

        // Retrieves a specific reservation by its ID with authorization checks
        [HttpGet("{id}")]
        public async Task<ActionResult<ReservationResponseDto>> GetReservation(string id)
        {
            var reservation = await _reservationService.GetReservationByIdAsync(id);
            if (reservation == null)
                return NotFound();

            if (!IsAdmin)
            {
                var isOwner = reservation.UserId == CurrentUserId;
                var operatorMatchesUserId = !string.IsNullOrEmpty(reservation.OperatorId) &&
                                            reservation.OperatorId == CurrentUserId;
                var operatorMatchesProfile = !string.IsNullOrEmpty(reservation.OperatorProfileId) &&
                                             reservation.OperatorProfileId == CurrentUserId;
                var isAssignedOperator = IsStationOperator && (operatorMatchesUserId || operatorMatchesProfile);

                if (!isOwner && !isAssignedOperator)
                    return Forbid();
            }

            return Ok(reservation);
        }

        // Creates a new reservation with business rules validation
        [HttpPost]
        public async Task<ActionResult<ReservationResponseDto>> CreateReservation(ReservationCreateDto reservationDto)
        {
            try
            {
                if (!IsAdmin)
                {
                    if (string.IsNullOrEmpty(CurrentUserId))
                        return Forbid();

                    // For EV owners we enforce that reservation targets the authenticated owner.
                    if (string.IsNullOrWhiteSpace(reservationDto.UserId))
                    {
                        reservationDto.UserId = CurrentUserId;
                    }
                    else if (!string.Equals(reservationDto.UserId, CurrentUserId, StringComparison.Ordinal))
                    {
                        return Forbid();
                    }
                }
                else
                {
                    // Admin/backoffice callers must provide either the EV owner's userId or NIC
                    if (string.IsNullOrWhiteSpace(reservationDto.UserId) &&
                        string.IsNullOrWhiteSpace(reservationDto.OwnerNic))
                    {
                        return BadRequest(new { message = "UserId or owner NIC is required when creating a reservation as admin." });
                    }
                }

                var reservation = await _reservationService.CreateReservationAsync(reservationDto);
                return CreatedAtAction(nameof(GetReservation), new { id = reservation.Id }, reservation);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Updates reservation using booking id (minimum 12 hours before start time)
        [HttpPut("booking/{bookingId}")]
        public async Task<ActionResult<ReservationResponseDto>> UpdateReservationByBooking(string bookingId, ReservationUpdateDto reservationDto)
        {
            try
            {
                var existing = await _reservationService.GetReservationByBookingIdAsync(bookingId);
                if (existing == null)
                    return NotFound();

                if (!IsAdmin && existing.UserId != CurrentUserId)
                    return Forbid();

                var reservation = await _reservationService.UpdateReservationAsync(existing.Id, reservationDto, IsAdmin);
                return Ok(reservation);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Cancels a reservation using booking id (minimum 12 hours before start time)
        [HttpPatch("booking/cancel/{bookingId}")]
        public async Task<ActionResult> CancelReservationByBooking(string bookingId)
        {
            try
            {
                var reservation = await _reservationService.GetReservationByBookingIdAsync(bookingId);
                if (reservation == null)
                    return NotFound();

                if (!IsAdmin && reservation.UserId != CurrentUserId)
                    return Forbid();

                var result = await _reservationService.CancelReservationAsync(reservation.Id, IsAdmin);
                if (!result)
                    return NotFound();

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Permanently deletes a reservation from the system
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteReservation(string id)
        {
            var reservation = await _reservationService.GetReservationByIdAsync(id);
            if (reservation == null)
                return NotFound();

            if (!IsAdmin && reservation.UserId != CurrentUserId)
                return Forbid();

            var result = await _reservationService.DeleteReservationAsync(id);
            if (!result)
                return NotFound();

            return NoContent();
        }

        // Retrieves reservation history for a specific EV owner by NIC
        [HttpGet("history/{nic}")]
        public async Task<ActionResult<IEnumerable<ReservationResponseDto>>> GetReservationHistory(string nic)
        {
            try
            {
                if (!IsAdmin)
                {
                    if (string.IsNullOrEmpty(CurrentNic) || !string.Equals(CurrentNic, nic, StringComparison.OrdinalIgnoreCase))
                    {
                        return Forbid();
                    }
                }

                var reservations = await _reservationService.GetReservationHistoryAsync(nic);
                return Ok(reservations);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet("user/{userId}/bookings/completed")]
        public async Task<ActionResult<IEnumerable<BookingSessionDto>>> GetUserCompletedBookings(string userId)
        {
            if (!IsAdmin && !string.Equals(CurrentUserId, userId, StringComparison.Ordinal))
                return Forbid();

            var bookings = await _reservationService.GetUserBookingHistoryAsync(userId);
            return Ok(bookings);
        }

        [HttpGet("user/{userId}/bookings/pending")]
        public async Task<ActionResult<IEnumerable<BookingSessionDto>>> GetUserPendingBookings(string userId)
        {
            if (!IsAdmin && !string.Equals(CurrentUserId, userId, StringComparison.Ordinal))
                return Forbid();

            var bookings = await _reservationService.GetUserPendingBookingsAsync(userId);
            return Ok(bookings);
        }

        // Admin can cancel any reservation
        [HttpPatch("{id}/admin-cancel")]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public async Task<ActionResult> AdminCancelReservation(string id)
        {
            try
            {
                var result = await _reservationService.AdminCancelReservationAsync(id);
                if (!result)
                    return NotFound();

                return Ok(new { success = true, message = "Reservation cancelled by admin" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Debug endpoint to check operator availability
        [HttpGet("debug/operator-availability")]
        public async Task<ActionResult> CheckOperatorAvailability(string stationId, DateTime reservationTime)
        {
            try
            {
                var operatorService = HttpContext.RequestServices.GetRequiredService<IOperatorService>();
                var availableOperator = await operatorService.GetAvailableOperatorForStationAsync(stationId, reservationTime);
                var stationOperators = await operatorService.GetOperatorsByStationAsync(stationId);
                
                return Ok(new {
                    stationId,
                    reservationTime,
                    localTime = reservationTime.ToLocalTime(),
                    availableOperator,
                    totalOperatorsForStation = stationOperators.Count,
                    stationOperators,
                    workingHours = new {
                        start = "09:00",
                        end = "18:00",
                        lunchBreak = "12:00-13:00"
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
