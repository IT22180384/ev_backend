/*
 * ReservationService.cs
 * IT22180384
 * Sandanima S. H. S.
 * 
 * Service class for managing reservation business logic.
 * Handles CRUD operations, validation, and business rules for reservations.
 */

using System;
using EV_Charging.Api.DTOs;
using EV_Charging.Api.DTOs.Booking;
using EV_Charging.Api.Models;
using EV_Charging.Api.Utils;
using EV_Charging.Api.Data;
using MongoDB.Driver;

namespace EV_Charging.Api.Services
{
    public interface IReservationService
    {
        Task<IEnumerable<ReservationResponseDto>> GetAllReservationsAsync();
        Task<ReservationResponseDto?> GetReservationByIdAsync(string id);
        Task<ReservationResponseDto> CreateReservationAsync(ReservationCreateDto reservationDto);
        Task<ReservationResponseDto?> UpdateReservationAsync(string id, ReservationUpdateDto reservationDto, bool allowAdminOverride = false);
        Task<bool> CancelReservationAsync(string id, bool allowAdminOverride = false);
        Task<bool> DeleteReservationAsync(string id);
        Task<IEnumerable<ReservationResponseDto>> GetReservationHistoryAsync(string nic);
        Task<bool> AdminCancelReservationAsync(string id);
        Task<IEnumerable<BookingSessionDto>> GetUserBookingHistoryAsync(string userId);
        Task<IEnumerable<BookingSessionDto>> GetUserPendingBookingsAsync(string userId);
    }

    public class ReservationService : IReservationService
    {
        private readonly MongoDbContext _context;
        private readonly IQrCodeGenerator _qrCodeGenerator;
        private readonly IOperatorService _operatorService;
        private readonly IMongoCollection<Booking> _bookings;

        public ReservationService(
            MongoDbContext context, 
            IQrCodeGenerator qrCodeGenerator,
            IOperatorService operatorService)
        {
            // Initialize service with database context and QR code generator
            _context = context;
            _qrCodeGenerator = qrCodeGenerator;
            _operatorService = operatorService;
            _bookings = context.GetBookingsCollection();
        }

        public async Task<IEnumerable<ReservationResponseDto>> GetAllReservationsAsync()
        {
            // Retrieve all reservations from the database
            var reservations = await _context.Reservations.Find(r => true).ToListAsync();
            return reservations.Select(MapToResponseDto);
        }

        public async Task<ReservationResponseDto?> GetReservationByIdAsync(string id)
        {
            // Retrieve a specific reservation by ID
            var reservation = await _context.Reservations.Find(r => r.Id == id).FirstOrDefaultAsync();

            if (reservation == null)
            {
                reservation = await _context.Reservations.Find(r => r.BookingId == id).FirstOrDefaultAsync();
            }

            return reservation != null ? MapToResponseDto(reservation) : null;
        }

        public async Task<ReservationResponseDto> CreateReservationAsync(ReservationCreateDto reservationDto)
        {
            // Create a new reservation with business rule validation
            // Resolve EV owner information (supports admin-created reservations by NIC)
            EVOwner? evOwner = null;

            if (!string.IsNullOrWhiteSpace(reservationDto.UserId))
            {
                evOwner = await _context.EVOwners.Find(o => o.Id == reservationDto.UserId).FirstOrDefaultAsync();

                if (evOwner == null)
                {
                    throw new InvalidOperationException("EV owner was not found for the provided userId.");
                }
            }

            if (evOwner == null)
            {
                if (string.IsNullOrWhiteSpace(reservationDto.OwnerNic))
                {
                    throw new InvalidOperationException("Either UserId or OwnerNic must be provided to create a reservation.");
                }

                evOwner = await _context.EVOwners.Find(o => o.NIC == reservationDto.OwnerNic).FirstOrDefaultAsync();

                if (evOwner == null)
                {
                    throw new InvalidOperationException($"EV owner with NIC '{reservationDto.OwnerNic}' was not found.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(reservationDto.OwnerNic) &&
                     !string.Equals(evOwner.NIC, reservationDto.OwnerNic, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Provided UserId and OwnerNic do not refer to the same EV owner.");
            }

            if (!evOwner.IsActive)
            {
                throw new InvalidOperationException("Cannot create reservation because the EV owner account is deactivated.");
            }

            reservationDto.UserId = evOwner.Id;

            // Validate 7-day limit
            if (!reservationDto.IsWithinSevenDays())
            {
                throw new InvalidOperationException("Reservations can only be made within 7 days from now.");
            }

            // Check for time conflicts (optional business rule)
            var hasConflict = await _context.Reservations.Find(r => 
                r.ChargingStationId == reservationDto.ChargingStationId &&
                r.Status != ReservationStatus.Cancelled &&
                r.Status != ReservationStatus.Completed &&
                ((reservationDto.StartTime >= r.StartTime && reservationDto.StartTime < r.EndTime) ||
                 (reservationDto.EndTime > r.StartTime && reservationDto.EndTime <= r.EndTime) ||
                 (reservationDto.StartTime <= r.StartTime && reservationDto.EndTime >= r.EndTime))).AnyAsync();

            if (hasConflict)
            {
                throw new InvalidOperationException("The charging station is already reserved for the selected time period.");
            }
            
            var availableOperator = await _operatorService
                .GetAvailableOperatorForStationAsync(reservationDto.ChargingStationId, reservationDto.StartTime);

            if (availableOperator == null)
            {
                // Log details for debugging
                var timeOfDay = reservationDto.StartTime.TimeOfDay;
                var timeInfo = $"Reservation time: {reservationDto.StartTime:yyyy-MM-dd HH:mm:ss} UTC (TimeOfDay: {timeOfDay})";
                var workingHours = "Working hours: 09:00-18:00 (excluding 12:00-13:00 lunch break)";
                
                throw new InvalidOperationException($"No available operator for the selected time slot. {timeInfo}. {workingHours}");
            }
            
            var reservation = new Reservation
            {
                UserId = reservationDto.UserId,
                ChargingStationId = reservationDto.ChargingStationId,
                StartTime = reservationDto.StartTime,
                EndTime = reservationDto.EndTime,
                Status = ReservationStatus.Confirmed, // Auto-confirm reservations
                QrCode = null, // QR code will be generated when requested
                CreatedAt = DateTime.UtcNow,
                Notes = reservationDto.Notes,
                OperatorId = availableOperator.Id,
                OperatorUserId = availableOperator.UserId
            };

            var booking = new Booking
            {
                UserId = reservationDto.UserId,
                StationId = reservationDto.ChargingStationId,
                ReservationDateTime = reservationDto.StartTime,
                Status = BookingStatus.Approved,
                OperatorId = availableOperator.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _bookings.InsertOneAsync(booking);

            // Set the BookingId on the reservation object
            reservation.BookingId = booking.Id;

            // Debug logging to verify values before saving
            Console.WriteLine($"DEBUG: Before saving reservation - OperatorId: '{reservation.OperatorId}', BookingId: '{reservation.BookingId}'");

            // Validate that reservation has required operator and booking information
            if (string.IsNullOrEmpty(reservation.OperatorId) || string.IsNullOrEmpty(reservation.BookingId))
            {
                throw new InvalidOperationException($"Cannot create reservation without valid operator and booking assignments. OperatorId: '{reservation.OperatorId}', BookingId: '{reservation.BookingId}'");
            }

            await _context.Reservations.InsertOneAsync(reservation);
            
            // Generate QR code and update all fields at once to ensure they're persisted
            var qrCode = _qrCodeGenerator.GenerateQrCode($"RESERVATION{reservation.Id}");
            var update = Builders<Reservation>.Update
                .Set(r => r.QrCode, qrCode)
                .Set(r => r.OperatorId, reservation.OperatorId)
                .Set(r => r.OperatorUserId, reservation.OperatorUserId)
                .Set(r => r.BookingId, reservation.BookingId);
            
            await _context.Reservations.UpdateOneAsync(r => r.Id == reservation.Id, update);
            
            // Debug logging to verify what was actually saved after update
            var savedReservation = await _context.Reservations.Find(r => r.Id == reservation.Id).FirstOrDefaultAsync();
            Console.WriteLine($"DEBUG: After updating reservation - OperatorId: '{savedReservation?.OperatorId}', BookingId: '{savedReservation?.BookingId}'");
            
            // Fetch the complete reservation from database to ensure all fields are properly set
            var completeReservation = await _context.Reservations.Find(r => r.Id == reservation.Id).FirstOrDefaultAsync();
            
            if (completeReservation == null)
            {
                throw new InvalidOperationException("Failed to retrieve created reservation from database.");
            }

            // Final validation that all required fields are present
            Console.WriteLine($"DEBUG: Final reservation - ID: {completeReservation.Id}, OperatorId: '{completeReservation.OperatorId}', BookingId: '{completeReservation.BookingId}'");
            
            if (string.IsNullOrEmpty(completeReservation.OperatorId) || string.IsNullOrEmpty(completeReservation.BookingId))
            {
                throw new InvalidOperationException($"Reservation created but missing required fields. OperatorId: '{completeReservation.OperatorId}', BookingId: '{completeReservation.BookingId}'");
            }
            
            return MapToResponseDto(completeReservation);
        }

        public async Task<ReservationResponseDto?> UpdateReservationAsync(string id, ReservationUpdateDto reservationDto, bool allowAdminOverride = false)
        {
            // Update an existing reservation with validation
            var reservation = await _context.Reservations.Find(r => r.Id == id).FirstOrDefaultAsync();
            if (reservation == null)
                return null;

            // Validate 12-hour rule
            if (!allowAdminOverride && !reservation.CanBeUpdated())
            {
                throw new InvalidOperationException("Reservations can only be updated at least 12 hours before the start time.");
            }

            // Check for conflicts if time is being changed
            if (reservationDto.StartTime.HasValue || reservationDto.EndTime.HasValue)
            {
                var newStartTime = reservationDto.StartTime ?? reservation.StartTime;
                var newEndTime = reservationDto.EndTime ?? reservation.EndTime;

                var hasConflict = await _context.Reservations.Find(r => 
                    r.Id != id &&
                    r.ChargingStationId == reservation.ChargingStationId &&
                    r.Status != ReservationStatus.Cancelled &&
                    r.Status != ReservationStatus.Completed &&
                    ((newStartTime >= r.StartTime && newStartTime < r.EndTime) ||
                     (newEndTime > r.StartTime && newEndTime <= r.EndTime) ||
                     (newStartTime <= r.StartTime && newEndTime >= r.EndTime))).AnyAsync();

                if (hasConflict)
                {
                    throw new InvalidOperationException("The charging station is already reserved for the selected time period.");
                }
            }

            var updateDefinition = Builders<Reservation>.Update.Set(r => r.UpdatedAt, DateTime.UtcNow);

            if (reservationDto.StartTime.HasValue)
                updateDefinition = updateDefinition.Set(r => r.StartTime, reservationDto.StartTime.Value);
            
            if (reservationDto.EndTime.HasValue)
                updateDefinition = updateDefinition.Set(r => r.EndTime, reservationDto.EndTime.Value);
            
            if (reservationDto.Status.HasValue)
            {
                updateDefinition = updateDefinition.Set(r => r.Status, reservationDto.Status.Value);
                
                // Generate QR code when status changes to Confirmed
                if (reservationDto.Status.Value == ReservationStatus.Confirmed && string.IsNullOrEmpty(reservation.QrCode))
                {
                    var qrCode = _qrCodeGenerator.GenerateQrCode($"RESERVATION{reservation.Id}");
                    updateDefinition = updateDefinition.Set(r => r.QrCode, qrCode);
                }
            }
            
            if (reservationDto.Notes != null)
                updateDefinition = updateDefinition.Set(r => r.Notes, reservationDto.Notes);

            await _context.Reservations.UpdateOneAsync(r => r.Id == id, updateDefinition);
            
            // Sync booking record if exists
            if (!string.IsNullOrEmpty(reservation.BookingId))
            {
                var bookingUpdate = Builders<Booking>.Update.Set(b => b.UpdatedAt, DateTime.UtcNow);

                if (reservationDto.StartTime.HasValue)
                    bookingUpdate = bookingUpdate.Set(b => b.ReservationDateTime, reservationDto.StartTime.Value);

                if (reservationDto.Status.HasValue)
                {
                    var bookingStatus = MapReservationStatusToBookingStatus(reservationDto.Status.Value);
                    bookingUpdate = bookingUpdate.Set(b => b.Status, bookingStatus);
                }

                await _bookings.UpdateOneAsync(b => b.Id == reservation.BookingId, bookingUpdate);
            }

            // Return updated reservation
            var updatedReservation = await _context.Reservations.Find(r => r.Id == id).FirstOrDefaultAsync();
            return MapToResponseDto(updatedReservation!);
        }

        public async Task<bool> CancelReservationAsync(string id, bool allowAdminOverride = false)
        {
            // Cancel a reservation with business rule validation
            var reservation = await _context.Reservations.Find(r => r.Id == id).FirstOrDefaultAsync();
            if (reservation == null)
                return false;

            // Validate 12-hour rule
            if (!allowAdminOverride && !reservation.CanBeCancelled())
            {
                throw new InvalidOperationException("Reservations can only be cancelled at least 12 hours before the start time.");
            }

            var updateDefinition = Builders<Reservation>.Update
                .Set(r => r.Status, ReservationStatus.Cancelled)
                .Set(r => r.UpdatedAt, DateTime.UtcNow);

            var result = await _context.Reservations.UpdateOneAsync(r => r.Id == id, updateDefinition);

            if (!string.IsNullOrEmpty(reservation.BookingId))
            {
                var bookingUpdate = Builders<Booking>.Update
                    .Set(b => b.Status, BookingStatus.Cancelled)
                    .Set(b => b.UpdatedAt, DateTime.UtcNow);

                await _bookings.UpdateOneAsync(b => b.Id == reservation.BookingId, bookingUpdate);
            }

            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteReservationAsync(string id)
        {
            // Permanently delete a reservation from the database
            var reservation = await _context.Reservations.Find(r => r.Id == id).FirstOrDefaultAsync();
            if (reservation == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(reservation.BookingId))
            {
                await _bookings.DeleteOneAsync(b => b.Id == reservation.BookingId);
            }

            var result = await _context.Reservations.DeleteOneAsync(r => r.Id == id);
            return result.DeletedCount > 0;
        }

        public async Task<IEnumerable<ReservationResponseDto>> GetReservationHistoryAsync(string nic)
        {
            // Get EV Owner by NIC first
            var evOwner = await _context.EVOwners.Find(e => e.NIC == nic).FirstOrDefaultAsync();
            
            if (evOwner == null)
            {
                return new List<ReservationResponseDto>();
            }

            // Get all reservations for this EV owner, ordered by creation date (newest first)
            var reservations = await _context.Reservations
                .Find(r => r.UserId == evOwner.Id)
                .SortByDescending(r => r.CreatedAt)
                .ToListAsync();

            return reservations.Select(MapToResponseDto);
        }

        public async Task<IEnumerable<BookingSessionDto>> GetUserBookingHistoryAsync(string userId)
        {
            var filterBuilder = Builders<Booking>.Filter;
            var filter = filterBuilder.Eq(b => b.UserId, userId) &
                         filterBuilder.Eq(b => b.Status, BookingStatus.Completed);

            var bookings = await _bookings
                .Find(filter)
                .SortByDescending(b => b.ReservationDateTime)
                .ToListAsync();

            return bookings.Select(MapBookingToSessionDto);
        }

        public async Task<IEnumerable<BookingSessionDto>> GetUserPendingBookingsAsync(string userId)
        {
            var filterBuilder = Builders<Booking>.Filter;
            var filter = filterBuilder.Eq(b => b.UserId, userId) &
                         filterBuilder.Eq(b => b.Status, BookingStatus.Approved);

            var bookings = await _bookings
                .Find(filter)
                .SortByDescending(b => b.ReservationDateTime)
                .ToListAsync();

            return bookings.Select(MapBookingToSessionDto);
        }

        public async Task<bool> AdminCancelReservationAsync(string id)
        {
            // Admin can cancel any reservation regardless of time constraints
            var reservation = await _context.Reservations.Find(r => r.Id == id).FirstOrDefaultAsync();
            
            if (reservation == null)
            {
                return false;
            }

            if (reservation.Status == ReservationStatus.Cancelled || reservation.Status == ReservationStatus.Completed)
            {
                throw new InvalidOperationException("Reservation is already cancelled or completed.");
            }

            var update = Builders<Reservation>.Update
                .Set(r => r.Status, ReservationStatus.Cancelled)
                .Set(r => r.UpdatedAt, DateTime.UtcNow);

            var result = await _context.Reservations.UpdateOneAsync(r => r.Id == id, update);

            if (!string.IsNullOrEmpty(reservation.BookingId))
            {
                var bookingUpdate = Builders<Booking>.Update
                    .Set(b => b.Status, BookingStatus.Cancelled)
                    .Set(b => b.UpdatedAt, DateTime.UtcNow);

                await _bookings.UpdateOneAsync(b => b.Id == reservation.BookingId, bookingUpdate);
            }

            return result.ModifiedCount > 0;
        }

        private static ReservationResponseDto MapToResponseDto(Reservation reservation)
        {
            // Map reservation entity to response DTO
            return new ReservationResponseDto
            {
                Id = reservation.Id,
                UserId = reservation.UserId,
                ChargingStationId = reservation.ChargingStationId,
                StartTime = reservation.StartTime,
                EndTime = reservation.EndTime,
                Status = reservation.Status,
                QrCode = reservation.QrCode,
                OperatorId = reservation.OperatorUserId ?? reservation.OperatorId,
                OperatorProfileId = reservation.OperatorId,
                BookingId = reservation.BookingId,
                CreatedAt = reservation.CreatedAt,
                UpdatedAt = reservation.UpdatedAt,
                Notes = reservation.Notes
            };
        }

        private static BookingSessionDto MapBookingToSessionDto(Booking booking)
        {
            return new BookingSessionDto
            {
                BookingId = booking.Id,
                StationId = booking.StationId,
                UserId = booking.UserId,
                Status = booking.Status.ToString(),
                ReservationDateTime = booking.ReservationDateTime,
                StartTime = booking.ReservationDateTime,
                EndTime = booking.ReservationDateTime.AddHours(1),
                CheckInTime = booking.CheckInTime,
                CheckOutTime = booking.CheckOutTime,
                EnergyConsumedKWh = booking.EnergyConsumedKWh,
                SessionDurationMinutes = booking.SessionDurationMinutes,
                SessionNotes = booking.SessionNotes
            };
        }

        private BookingStatus MapReservationStatusToBookingStatus(ReservationStatus status)
        {
            return status switch
            {
                ReservationStatus.Confirmed => BookingStatus.Approved,
                ReservationStatus.Active => BookingStatus.InProgress,
                ReservationStatus.Completed => BookingStatus.Completed,
                ReservationStatus.Cancelled => BookingStatus.Cancelled,
                _ => BookingStatus.Pending
            };
        }
    }
}
