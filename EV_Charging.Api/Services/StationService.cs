/*
 * StationService.cs
 * IT22055026
 * Lakvindu U. G. V.
 * 
 * Service layer implementing business logic for charging station management
 * including CRUD operations, availability checking, and geo-spatial filtering
 */

using EV_Charging.Api.DTOs;
using EV_Charging.Api.Models;
using MongoDB.Driver;

namespace EV_Charging.Api.Services
{
    public interface IStationService
    {
        Task<List<StationDto>> GetStationsAsync(StationFilterDto filters);
        Task<StationDetailDto?> GetStationByIdAsync(string id);
        Task<StationDetailDto> CreateStationAsync(CreateStationDto createDto);
        Task<StationDetailDto?> UpdateStationAsync(string id, UpdateStationDto updateDto);
        Task<OperationResult> DeactivateStationAsync(string id);
        Task<List<TimeSlotDto>?> GetStationAvailabilityAsync(string id, DateTime date);
        
        // New methods for additional endpoints
        Task<List<StationDto>> GetActiveStationsAsync();
        Task<List<StationDto>> GetNearbyStationsAsync(double latitude, double longitude, double radiusKm);
        Task<StationDetailDto?> UpdateStationScheduleAsync(string id, UpdateScheduleDto updateScheduleDto);
    }

    public class StationService : IStationService
    {
        private readonly IMongoCollection<Station> _stations;
        private readonly IMongoCollection<Booking> _bookings;
        private readonly ILogger<StationService> _logger;

        public StationService(IMongoDatabase database, ILogger<StationService> logger)
        {
            _stations = database.GetCollection<Station>("Stations");
            _bookings = database.GetCollection<Booking>("Bookings");
            _logger = logger;
        }

        // Retrieves stations based on various filters including geo-location
        public async Task<List<StationDto>> GetStationsAsync(StationFilterDto filters)
        {
            var filterBuilder = Builders<Station>.Filter;
            var filter = filterBuilder.Empty;

            // Apply active status filter
            if (filters.IsActive.HasValue)
            {
                filter &= filterBuilder.Eq(s => s.IsActive, filters.IsActive.Value);
            }

            // Apply type filter (AC/DC)
            if (!string.IsNullOrEmpty(filters.Type))
            {
                filter &= filterBuilder.Eq(s => s.Type, filters.Type);
            }

            // Apply search query (name or location)
            if (!string.IsNullOrEmpty(filters.SearchQuery))
            {
                var searchFilter = filterBuilder.Or(
                    filterBuilder.Regex(s => s.Name, new MongoDB.Bson.BsonRegularExpression(filters.SearchQuery, "i")),
                    filterBuilder.Regex(s => s.Location.Address, new MongoDB.Bson.BsonRegularExpression(filters.SearchQuery, "i"))
                );
                filter &= searchFilter;
            }

            var stations = await _stations.Find(filter).ToListAsync();

            // Apply geo-spatial filtering if coordinates provided
            if (filters.Latitude.HasValue && filters.Longitude.HasValue && filters.RadiusKm.HasValue)
            {
                stations = FilterByDistance(
                    stations,
                    filters.Latitude.Value,
                    filters.Longitude.Value,
                    filters.RadiusKm.Value
                );
            }

            return stations.Select(s => MapToDto(s)).ToList();
        }

        // Retrieves detailed information for a specific station
        public async Task<StationDetailDto?> GetStationByIdAsync(string id)
        {
            var station = await _stations.Find(s => s.Id == id).FirstOrDefaultAsync();
            return station != null ? MapToDetailDto(station) : null;
        }

        // Creates a new charging station with validation
        public async Task<StationDetailDto> CreateStationAsync(CreateStationDto createDto)
        {
            var station = new Station
            {
                Name = createDto.Name,
                Location = new Location
                {
                    Address = createDto.Address,
                    Latitude = createDto.Latitude,
                    Longitude = createDto.Longitude
                },
                Type = createDto.Type,
                TotalSlots = createDto.TotalSlots,
                AvailableSlots = createDto.TotalSlots,
                Connectors = createDto.Connectors,
                Schedule = createDto.Schedule ?? GetDefaultSchedule(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _stations.InsertOneAsync(station);
            _logger.LogInformation("Created new station: {StationId} - {StationName}", station.Id, station.Name);

            return MapToDetailDto(station);
        }

        // Updates existing station details
        public async Task<StationDetailDto?> UpdateStationAsync(string id, UpdateStationDto updateDto)
        {
            var station = await _stations.Find(s => s.Id == id).FirstOrDefaultAsync();
            
            if (station == null)
            {
                return null;
            }

            var updateBuilder = Builders<Station>.Update;
            var updates = new List<UpdateDefinition<Station>>();

            if (!string.IsNullOrEmpty(updateDto.Name))
                updates.Add(updateBuilder.Set(s => s.Name, updateDto.Name));

            if (!string.IsNullOrEmpty(updateDto.Address))
                updates.Add(updateBuilder.Set(s => s.Location.Address, updateDto.Address));

            if (updateDto.Latitude.HasValue)
                updates.Add(updateBuilder.Set(s => s.Location.Latitude, updateDto.Latitude.Value));

            if (updateDto.Longitude.HasValue)
                updates.Add(updateBuilder.Set(s => s.Location.Longitude, updateDto.Longitude.Value));

            if (updateDto.TotalSlots.HasValue)
            {
                updates.Add(updateBuilder.Set(s => s.TotalSlots, updateDto.TotalSlots.Value));
                // Adjust available slots proportionally
                var ratio = (double)station.AvailableSlots / station.TotalSlots;
                var newAvailable = (int)(updateDto.TotalSlots.Value * ratio);
                updates.Add(updateBuilder.Set(s => s.AvailableSlots, newAvailable));
            }

            if (updateDto.Connectors != null && updateDto.Connectors.Any())
                updates.Add(updateBuilder.Set(s => s.Connectors, updateDto.Connectors));

            if (updateDto.Schedule != null && updateDto.Schedule.Any())
                updates.Add(updateBuilder.Set(s => s.Schedule, updateDto.Schedule));

            updates.Add(updateBuilder.Set(s => s.UpdatedAt, DateTime.UtcNow));

            if (updates.Any())
            {
                var combinedUpdate = updateBuilder.Combine(updates);
                await _stations.UpdateOneAsync(s => s.Id == id, combinedUpdate);
                _logger.LogInformation("Updated station: {StationId}", id);
            }

            var updatedStation = await _stations.Find(s => s.Id == id).FirstOrDefaultAsync();
            return MapToDetailDto(updatedStation);
        }

        // Deactivates a station after checking for active bookings
        public async Task<OperationResult> DeactivateStationAsync(string id)
        {
            var station = await _stations.Find(s => s.Id == id).FirstOrDefaultAsync();
            
            if (station == null)
            {
                return new OperationResult { Success = false, Message = "Station not found" };
            }

            if (!station.IsActive)
            {
                return new OperationResult { Success = false, Message = "Station is already deactivated" };
            }

            // Check for active or future bookings
            var now = DateTime.UtcNow;
            var activeBookingsCount = await _bookings.CountDocumentsAsync(b =>
                b.StationId == id &&
                b.Status != BookingStatus.Cancelled &&
                b.Status != BookingStatus.Completed &&
                b.ReservationDateTime >= now
            );

            if (activeBookingsCount > 0)
            {
                return new OperationResult
                {
                    Success = false,
                    Message = $"Cannot deactivate station. There are {activeBookingsCount} active or future booking(s)"
                };
            }

            var update = Builders<Station>.Update
                .Set(s => s.IsActive, false)
                .Set(s => s.UpdatedAt, DateTime.UtcNow);

            await _stations.UpdateOneAsync(s => s.Id == id, update);
            _logger.LogInformation("Deactivated station: {StationId}", id);

            return new OperationResult
            {
                Success = true,
                Message = "Station deactivated successfully"
            };
        }

        // Gets available time slots for a station on a specific date
        public async Task<List<TimeSlotDto>?> GetStationAvailabilityAsync(string id, DateTime date)
        {
            var station = await _stations.Find(s => s.Id == id && s.IsActive).FirstOrDefaultAsync();
            
            if (station == null)
            {
                return null;
            }

            var dayOfWeek = date.DayOfWeek.ToString();
            var schedule = station.Schedule.FirstOrDefault(s => s.DayOfWeek == dayOfWeek);

            if (schedule == null || !schedule.IsOpen)
            {
                return new List<TimeSlotDto>();
            }

            // Get bookings for this station and date
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);
            
            var bookings = await _bookings.Find(b =>
                b.StationId == id &&
                b.ReservationDateTime >= startOfDay &&
                b.ReservationDateTime < endOfDay &&
                b.Status != BookingStatus.Cancelled
            ).ToListAsync();

            return GenerateTimeSlots(schedule, date, bookings, station.TotalSlots);
        }

        // Gets all active stations
        public async Task<List<StationDto>> GetActiveStationsAsync()
        {
            var filter = Builders<Station>.Filter.Eq(s => s.IsActive, true);
            var stations = await _stations.Find(filter).ToListAsync();
            return stations.Select(s => MapToDto(s)).ToList();
        }

        // Gets nearby stations within specified radius
        public async Task<List<StationDto>> GetNearbyStationsAsync(double latitude, double longitude, double radiusKm)
        {
            var stations = await _stations.Find(Builders<Station>.Filter.Empty).ToListAsync();
            var nearbyStations = FilterByDistance(stations, latitude, longitude, radiusKm);
            return nearbyStations.Select(s => MapToDto(s)).ToList();
        }

        // Updates the schedule for a specific station
        public async Task<StationDetailDto?> UpdateStationScheduleAsync(string id, UpdateScheduleDto updateScheduleDto)
        {
            var station = await _stations.Find(s => s.Id == id).FirstOrDefaultAsync();
            
            if (station == null)
            {
                return null;
            }

            var update = Builders<Station>.Update
                .Set(s => s.Schedule, updateScheduleDto.Schedule)
                .Set(s => s.UpdatedAt, DateTime.UtcNow);

            await _stations.UpdateOneAsync(s => s.Id == id, update);
            _logger.LogInformation("Updated schedule for station: {StationId}", id);

            var updatedStation = await _stations.Find(s => s.Id == id).FirstOrDefaultAsync();
            return MapToDetailDto(updatedStation);
        }

        // Helper methods

        private List<Station> FilterByDistance(List<Station> stations, double lat, double lon, double radiusKm)
        {
            return stations.Where(s =>
            {
                var distance = CalculateDistance(lat, lon, s.Location.Latitude, s.Location.Longitude);
                return distance <= radiusKm;
            }).ToList();
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            // Haversine formula for calculating distance between two coordinates
            const double R = 6371; // Earth's radius in kilometers
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }

        private List<DaySchedule> GetDefaultSchedule()
        {
            var days = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
            return days.Select(day => new DaySchedule
            {
                DayOfWeek = day,
                IsOpen = true,
                OpenTime = "08:00",
                CloseTime = "20:00"
            }).ToList();
        }

        private List<TimeSlotDto> GenerateTimeSlots(DaySchedule schedule, DateTime date, List<Booking> bookings, int totalSlots)
        {
            var slots = new List<TimeSlotDto>();
            
            // Standardized working hours: 9 AM - 6 PM with lunch break 12-1 PM
            // Slots: 9-10, 10-11, 11-12, 1-2, 2-3, 3-4, 4-5, 5-6
            var workingHours = new List<(TimeSpan start, TimeSpan end)>
            {
                (new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0)),   // 9 AM - 12 PM
                (new TimeSpan(13, 0, 0), new TimeSpan(18, 0, 0))    // 1 PM - 6 PM
            };

            var slotDuration = TimeSpan.FromHours(1);

            foreach (var (start, end) in workingHours)
            {
                var currentTime = start;
                
                while (currentTime < end)
                {
                    var slotDateTime = date.Date.Add(currentTime);
                    var endTime = currentTime.Add(slotDuration);
                    
                    if (endTime > end) break;

                    var bookedSlots = bookings.Count(b => b.ReservationDateTime == slotDateTime);
                    var availableSlots = totalSlots - bookedSlots;

                    slots.Add(new TimeSlotDto
                    {
                        StartTime = slotDateTime,
                        EndTime = date.Date.Add(endTime),
                        AvailableSlots = availableSlots,
                        IsAvailable = availableSlots > 0 && slotDateTime > DateTime.UtcNow
                    });

                    currentTime = currentTime.Add(slotDuration);
                }
            }

            return slots;
        }

        private StationDto MapToDto(Station station)
        {
            return new StationDto
            {
                Id = station.Id,
                Name = station.Name,
                Address = station.Location.Address,
                Latitude = station.Location.Latitude,
                Longitude = station.Location.Longitude,
                Type = station.Type,
                TotalSlots = station.TotalSlots,
                AvailableSlots = station.AvailableSlots,
                IsActive = station.IsActive
            };
        }

        private StationDetailDto MapToDetailDto(Station station)
        {
            return new StationDetailDto
            {
                Id = station.Id,
                Name = station.Name,
                Location = station.Location,
                Type = station.Type,
                TotalSlots = station.TotalSlots,
                AvailableSlots = station.AvailableSlots,
                Connectors = station.Connectors,
                Schedule = station.Schedule,
                IsActive = station.IsActive,
                CreatedAt = station.CreatedAt,
                UpdatedAt = station.UpdatedAt
            };
        }
    }
}