/*
 * OperatorService.cs
 * IT22267504
 * Methmini, K. A. T.
 * 
 * Service for managing station operators and automatic assignment to bookings
 */

using System;
using EV_Charging.Api.DTOs;
using EV_Charging.Api.Models;
using MongoDB.Driver;

namespace EV_Charging.Api.Services
{
    public interface IOperatorService
    {
        Task<List<OperatorDto>> GetOperatorsAsync();
        Task<OperatorDto?> GetOperatorByIdAsync(string id);
        Task<OperatorDto> CreateOperatorAsync(CreateOperatorDto createDto);
        Task<OperatorDto?> UpdateOperatorAsync(string id, UpdateOperatorDto updateDto);
        Task<OperationResult> DeactivateOperatorAsync(string id);
        Task<OperatorDto?> GetAvailableOperatorForStationAsync(string stationId, DateTime reservationDateTime);
        Task<List<OperatorDto>> GetOperatorsByStationAsync(string stationId);
        Task<OperationResult> CompleteSessionAsync(string bookingId, FinalizeSessionDto finalizeDto, string operatorUserId, bool isAdmin);
        Task<List<BookingSessionDto>> GetAssignedSessionsAsync(string operatorUserId, BookingStatus? statusFilter);
    }

    public class OperatorService : IOperatorService
    {
        private readonly IMongoCollection<Operator> _operators;
        private readonly IMongoCollection<Booking> _bookings;
        private readonly IMongoCollection<User> _users;
        private readonly ILogger<OperatorService> _logger;

        public OperatorService(IMongoDatabase database, ILogger<OperatorService> logger)
        {
            _operators = database.GetCollection<Operator>("Operators");
            _bookings = database.GetCollection<Booking>("Bookings");
            _users = database.GetCollection<User>("users");
            _logger = logger;
        }

        public async Task<List<OperatorDto>> GetOperatorsAsync()
        {
            var operators = await _operators.Find(o => true).ToListAsync();
            return operators.Select(MapToDto).ToList();
        }

        public async Task<OperatorDto?> GetOperatorByIdAsync(string id)
        {
            var op = await _operators.Find(o => o.Id == id).FirstOrDefaultAsync();
            return op != null ? MapToDto(op) : null;
        }

        public async Task<OperatorDto> CreateOperatorAsync(CreateOperatorDto createDto)
        {
            if (string.IsNullOrEmpty(createDto.UserId))
            {
                throw new InvalidOperationException("UserId is required to create an operator.");
            }

            var user = await _users.Find(u => u.Id == createDto.UserId).FirstOrDefaultAsync();
            if (user == null)
            {
                throw new InvalidOperationException("User not found. Register the operator account first.");
            }

            var shouldUpdateUser = false;

            if (!string.Equals(user.Role, UserRoles.StationOperator, StringComparison.OrdinalIgnoreCase))
            {
                user.Role = UserRoles.StationOperator;
                shouldUpdateUser = true;
            }

            if (!string.Equals(user.Name, createDto.Name, StringComparison.Ordinal))
            {
                user.Name = createDto.Name;
                shouldUpdateUser = true;
            }

            if (!string.Equals(user.Email, createDto.Email, StringComparison.OrdinalIgnoreCase))
            {
                user.Email = createDto.Email;
                shouldUpdateUser = true;
            }

            if (!string.Equals(user.Phone, createDto.Phone, StringComparison.Ordinal))
            {
                user.Phone = createDto.Phone;
                shouldUpdateUser = true;
            }

            if (shouldUpdateUser)
            {
                user.UpdatedAt = DateTime.UtcNow;
                await _users.ReplaceOneAsync(u => u.Id == user.Id, user);
            }

            var existingOperator = await _operators.Find(o => o.UserId == user.Id).FirstOrDefaultAsync();
            if (existingOperator != null)
            {
                throw new InvalidOperationException("Operator profile already exists for this user.");
            }

            var op = new Operator
            {
                UserId = user.Id,
                StationId = createDto.StationId,
                Name = createDto.Name,
                Email = createDto.Email,
                Phone = createDto.Phone,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _operators.InsertOneAsync(op);
            _logger.LogInformation("Created new operator: {OperatorId} for station: {StationId}", op.Id, op.StationId);

            return MapToDto(op);
        }

        public async Task<OperatorDto?> UpdateOperatorAsync(string id, UpdateOperatorDto updateDto)
        {
            var op = await _operators.Find(o => o.Id == id).FirstOrDefaultAsync();
            if (op == null) return null;

            var updateBuilder = Builders<Operator>.Update;
            var updates = new List<UpdateDefinition<Operator>>();

            if (!string.IsNullOrEmpty(updateDto.Name))
                updates.Add(updateBuilder.Set(o => o.Name, updateDto.Name));

            if (!string.IsNullOrEmpty(updateDto.Email))
                updates.Add(updateBuilder.Set(o => o.Email, updateDto.Email));

            if (!string.IsNullOrEmpty(updateDto.Phone))
                updates.Add(updateBuilder.Set(o => o.Phone, updateDto.Phone));

            updates.Add(updateBuilder.Set(o => o.UpdatedAt, DateTime.UtcNow));

            if (updates.Any())
            {
                var combinedUpdate = updateBuilder.Combine(updates);
                await _operators.UpdateOneAsync(o => o.Id == id, combinedUpdate);
                _logger.LogInformation("Updated operator: {OperatorId}", id);
            }

            var updatedOperator = await _operators.Find(o => o.Id == id).FirstOrDefaultAsync();

            if (updatedOperator != null)
            {
                var user = await _users.Find(u => u.Id == updatedOperator.UserId).FirstOrDefaultAsync();
                if (user != null)
                {
                    var userUpdates = new List<UpdateDefinition<User>>();
                    var userUpdateBuilder = Builders<User>.Update;
                    if (!string.IsNullOrEmpty(updateDto.Name) && !string.Equals(user.Name, updateDto.Name, StringComparison.Ordinal))
                        userUpdates.Add(userUpdateBuilder.Set(u => u.Name, updateDto.Name));
                    if (!string.IsNullOrEmpty(updateDto.Email) && !string.Equals(user.Email, updateDto.Email, StringComparison.OrdinalIgnoreCase))
                        userUpdates.Add(userUpdateBuilder.Set(u => u.Email, updateDto.Email));
                    if (!string.IsNullOrEmpty(updateDto.Phone) && !string.Equals(user.Phone, updateDto.Phone, StringComparison.Ordinal))
                        userUpdates.Add(userUpdateBuilder.Set(u => u.Phone, updateDto.Phone));

                    if (userUpdates.Any())
                    {
                        userUpdates.Add(userUpdateBuilder.Set(u => u.UpdatedAt, DateTime.UtcNow));
                        var combinedUserUpdate = userUpdateBuilder.Combine(userUpdates);
                        await _users.UpdateOneAsync(u => u.Id == user.Id, combinedUserUpdate);
                    }
                }
            }

            return MapToDto(updatedOperator);
        }

        public async Task<OperationResult> DeactivateOperatorAsync(string id)
        {
            var op = await _operators.Find(o => o.Id == id).FirstOrDefaultAsync();
            if (op == null)
                return new OperationResult { Success = false, Message = "Operator not found" };

            if (!op.IsActive)
                return new OperationResult { Success = false, Message = "Operator is already deactivated" };

            // Check for active bookings assigned to this operator
            var now = DateTime.UtcNow;
            var activeBookingsCount = await _bookings.CountDocumentsAsync(b =>
                b.OperatorId == id &&
                b.Status != BookingStatus.Cancelled &&
                b.Status != BookingStatus.Completed &&
                b.ReservationDateTime >= now
            );

            if (activeBookingsCount > 0)
            {
                return new OperationResult
                {
                    Success = false,
                    Message = $"Cannot deactivate operator. There are {activeBookingsCount} active or future booking(s) assigned"
                };
            }

            var update = Builders<Operator>.Update
                .Set(o => o.IsActive, false)
                .Set(o => o.UpdatedAt, DateTime.UtcNow);

            await _operators.UpdateOneAsync(o => o.Id == id, update);
            _logger.LogInformation("Deactivated operator: {OperatorId}", id);

            return new OperationResult { Success = true, Message = "Operator deactivated successfully" };
        }

        public async Task<OperatorDto?> GetAvailableOperatorForStationAsync(string stationId, DateTime reservationDateTime)
        {
            // Convert UTC time to local time for working hours check
            var localTime = reservationDateTime.ToLocalTime();
            var timeOfDay = localTime.TimeOfDay;

            _logger.LogInformation($"Checking operator availability for station {stationId} at {reservationDateTime:yyyy-MM-dd HH:mm:ss} UTC (Local: {localTime:yyyy-MM-dd HH:mm:ss})");

            // Check if time is within working hours (9 AM - 6 PM, excluding lunch 12-1 PM)
            if (!IsTimeInWorkingHours(timeOfDay))
            {
                _logger.LogWarning($"Time {timeOfDay} is outside working hours (09:00-18:00, excluding 12:00-13:00)");
                return null;
            }

            // Find operators assigned to this station
            var stationOperators = await _operators.Find(o => 
                o.StationId == stationId && 
                o.IsActive
            ).ToListAsync();

            foreach (var op in stationOperators)
            {
                // Check if operator is already assigned to a booking at this time
                var hasConflict = await _bookings.CountDocumentsAsync(b =>
                    b.OperatorId == op.Id &&
                    b.ReservationDateTime == reservationDateTime &&
                    b.Status != BookingStatus.Cancelled &&
                    b.Status != BookingStatus.Completed
                ) > 0;

                if (!hasConflict)
                {
                    return MapToDto(op);
                }
            }

            return null; // No available operator found
        }

        public async Task<List<OperatorDto>> GetOperatorsByStationAsync(string stationId)
        {
            var operators = await _operators.Find(o => o.StationId == stationId).ToListAsync();
            return operators.Select(MapToDto).ToList();
        }

        public async Task<OperationResult> CompleteSessionAsync(string bookingId, FinalizeSessionDto finalizeDto, string operatorUserId, bool isAdmin)
        {
            var booking = await _bookings.Find(b => b.Id == bookingId).FirstOrDefaultAsync();
            
            if (booking == null)
                return new OperationResult { Success = false, Message = "Booking not found" };

            if (booking.Status != BookingStatus.InProgress)
                return new OperationResult { Success = false, Message = "Session is not in progress" };

            Operator? currentOperator = null;
            if (!isAdmin)
            {
                if (string.IsNullOrEmpty(operatorUserId))
                {
                    return new OperationResult { Success = false, Message = "Operator context is missing" };
                }

                currentOperator = await _operators.Find(o => o.UserId == operatorUserId && o.IsActive).FirstOrDefaultAsync();

                if (currentOperator == null)
                {
                    return new OperationResult { Success = false, Message = "Operator profile not found or inactive" };
                }

                if (string.IsNullOrEmpty(booking.OperatorId) || booking.OperatorId != currentOperator.Id)
                {
                    return new OperationResult { Success = false, Message = "You are not assigned to this booking" };
                }
            }

            var update = Builders<Booking>.Update
                .Set(b => b.Status, BookingStatus.Completed)
                .Set(b => b.CheckOutTime, DateTime.UtcNow)
                .Set(b => b.EnergyConsumedKWh, finalizeDto.EnergyConsumedKWh)
                .Set(b => b.SessionNotes, finalizeDto.Notes)
                .Set(b => b.UpdatedAt, DateTime.UtcNow);

            // Calculate session duration
            if (booking.CheckInTime.HasValue)
            {
                var duration = (int)(DateTime.UtcNow - booking.CheckInTime.Value).TotalMinutes;
                update = update.Set(b => b.SessionDurationMinutes, duration);
            }

            await _bookings.UpdateOneAsync(b => b.Id == bookingId, update);
            _logger.LogInformation("Completed session for booking: {BookingId}", bookingId);

            return new OperationResult { Success = true, Message = "Session completed successfully" };
        }

        public async Task<List<BookingSessionDto>> GetAssignedSessionsAsync(string operatorUserId, BookingStatus? statusFilter)
        {
            var operatorEntity = await _operators
                .Find(o => o.UserId == operatorUserId && o.IsActive)
                .FirstOrDefaultAsync();

            if (operatorEntity == null)
            {
                return new List<BookingSessionDto>();
            }

            var filterBuilder = Builders<Booking>.Filter;
            var filter = filterBuilder.Eq(b => b.OperatorId, operatorEntity.Id);

            if (statusFilter.HasValue)
            {
                filter &= filterBuilder.Eq(b => b.Status, statusFilter.Value);
            }

            var bookings = await _bookings
                .Find(filter)
                .SortByDescending(b => b.ReservationDateTime)
                .ToListAsync();

            return bookings.Select(b => new BookingSessionDto
            {
                BookingId = b.Id,
                StationId = b.StationId,
                UserId = b.UserId,
                Status = b.Status.ToString(),
                ReservationDateTime = b.ReservationDateTime,
                StartTime = b.ReservationDateTime,
                EndTime = b.ReservationDateTime.AddHours(1),
                CheckInTime = b.CheckInTime,
                CheckOutTime = b.CheckOutTime,
                EnergyConsumedKWh = b.EnergyConsumedKWh,
                SessionDurationMinutes = b.SessionDurationMinutes,
                SessionNotes = b.SessionNotes
            }).ToList();
        }

        private bool IsTimeInWorkingHours(TimeSpan timeOfDay)
        {
            // Check if time is within working hours (9 AM - 6 PM)
            if (timeOfDay < OperatorWorkingHours.StartTime || timeOfDay >= OperatorWorkingHours.EndTime)
                return false;

            // Check if time is during lunch break (12 PM - 1 PM)
            if (timeOfDay >= OperatorWorkingHours.LunchStart && timeOfDay < OperatorWorkingHours.LunchEnd)
                return false;

            return true;
        }

        private OperatorDto MapToDto(Operator op)
        {
            return new OperatorDto
            {
                Id = op.Id,
                UserId = op.UserId,
                StationId = op.StationId,
                Name = op.Name,
                Email = op.Email,
                Phone = op.Phone,
                IsActive = op.IsActive,
                CreatedAt = op.CreatedAt,
                UpdatedAt = op.UpdatedAt
            };
        }
    }
}