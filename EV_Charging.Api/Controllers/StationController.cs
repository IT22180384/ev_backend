/*
 * StationController.cs
 * IT22055026
 * Lakvindu U. G. V.
 * 
 * This controller manages charging station operations including CRUD operations,
 * station discovery, and availability checking with proper authorization
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EV_Charging.Api.Services;
using EV_Charging.Api.DTOs;
using EV_Charging.Api.Models;

namespace EV_Charging.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StationsController : ControllerBase
    {
        private readonly IStationService _stationService;
        private readonly ILogger<StationsController> _logger;

        public StationsController(IStationService stationService, ILogger<StationsController> logger)
        {
            _stationService = stationService;
            _logger = logger;
        }

        // Retrieves all stations with optional filters for discovery and search
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<StationDto>>>> GetStations(
            [FromQuery] bool? active,
            [FromQuery] string? type,
            [FromQuery] string? searchQuery,
            [FromQuery] double? latitude,
            [FromQuery] double? longitude,
            [FromQuery] double? radiusKm)
        {
            try
            {
                var filters = new StationFilterDto
                {
                    IsActive = active,
                    Type = type,
                    SearchQuery = searchQuery,
                    Latitude = latitude,
                    Longitude = longitude,
                    RadiusKm = radiusKm
                };

                var stations = await _stationService.GetStationsAsync(filters);
                
                return Ok(new ApiResponse<List<StationDto>>
                {
                    Success = true,
                    Data = stations,
                    Message = $"Retrieved {stations.Count} stations successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving stations");
                return StatusCode(500, new ApiResponse<List<StationDto>>
                {
                    Success = false,
                    Message = "An error occurred while retrieving stations"
                });
            }
        }

        // Retrieves detailed information about a specific station
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<StationDetailDto>>> GetStation(string id)
        {
            try
            {
                var station = await _stationService.GetStationByIdAsync(id);
                
                if (station == null)
                {
                    return NotFound(new ApiResponse<StationDetailDto>
                    {
                        Success = false,
                        Message = "Station not found"
                    });
                }

                return Ok(new ApiResponse<StationDetailDto>
                {
                    Success = true,
                    Data = station,
                    Message = "Station retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving station {StationId}", id);
                return StatusCode(500, new ApiResponse<StationDetailDto>
                {
                    Success = false,
                    Message = "An error occurred while retrieving station details"
                });
            }
        }

        // Creates a new charging station (Admin only)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<StationDetailDto>>> CreateStation([FromBody] CreateStationDto createStationDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<StationDetailDto>
                    {
                        Success = false,
                        Message = "Invalid station data",
                        Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                    });
                }

                var station = await _stationService.CreateStationAsync(createStationDto);

                return CreatedAtAction(
                    nameof(GetStation),
                    new { id = station.Id },
                    new ApiResponse<StationDetailDto>
                    {
                        Success = true,
                        Data = station,
                        Message = "Station created successfully"
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating station");
                return StatusCode(500, new ApiResponse<StationDetailDto>
                {
                    Success = false,
                    Message = "An error occurred while creating the station"
                });
            }
        }

        // Updates an existing charging station (Admin only)
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<StationDetailDto>>> UpdateStation(string id, [FromBody] UpdateStationDto updateStationDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<StationDetailDto>
                    {
                        Success = false,
                        Message = "Invalid station data",
                        Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                    });
                }

                var station = await _stationService.UpdateStationAsync(id, updateStationDto);

                if (station == null)
                {
                    return NotFound(new ApiResponse<StationDetailDto>
                    {
                        Success = false,
                        Message = "Station not found"
                    });
                }

                return Ok(new ApiResponse<StationDetailDto>
                {
                    Success = true,
                    Data = station,
                    Message = "Station updated successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating station {StationId}", id);
                return StatusCode(500, new ApiResponse<StationDetailDto>
                {
                    Success = false,
                    Message = "An error occurred while updating the station"
                });
            }
        }

        // Deactivates a charging station (Admin only) - prevents deactivation if active or future bookings exist
        [HttpPatch("{id}/deactivate")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<object>>> DeactivateStation(string id)
        {
            try
            {
                var result = await _stationService.DeactivateStationAsync(id);

                if (!result.Success)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = result.Message
                    });
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating station {StationId}", id);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred while deactivating the station"
                });
            }
        }

        // Retrieves available time slots for a specific station on a given date
        [HttpGet("{id}/availability")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<TimeSlotDto>>>> GetStationAvailability(
            string id,
            [FromQuery] string date)
        {
            try
            {
                if (!DateTime.TryParse(date, out DateTime parsedDate))
                {
                    return BadRequest(new ApiResponse<List<TimeSlotDto>>
                    {
                        Success = false,
                        Message = "Invalid date format. Use YYYY-MM-DD"
                    });
                }

                var availability = await _stationService.GetStationAvailabilityAsync(id, parsedDate);

                if (availability == null)
                {
                    return NotFound(new ApiResponse<List<TimeSlotDto>>
                    {
                        Success = false,
                        Message = "Station not found"
                    });
                }

                return Ok(new ApiResponse<List<TimeSlotDto>>
                {
                    Success = true,
                    Data = availability,
                    Message = "Availability retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving availability for station {StationId}", id);
                return StatusCode(500, new ApiResponse<List<TimeSlotDto>>
                {
                    Success = false,
                    Message = "An error occurred while retrieving availability"
                });
            }
        }

        // Updates the schedule/time slots for a specific station (Admin only)
        [HttpPatch("{id}/schedule")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<StationDetailDto>>> UpdateStationSchedule(string id, [FromBody] UpdateScheduleDto updateScheduleDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<StationDetailDto>
                    {
                        Success = false,
                        Message = "Invalid schedule data",
                        Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                    });
                }

                var station = await _stationService.UpdateStationScheduleAsync(id, updateScheduleDto);

                if (station == null)
                {
                    return NotFound(new ApiResponse<StationDetailDto>
                    {
                        Success = false,
                        Message = "Station not found"
                    });
                }

                return Ok(new ApiResponse<StationDetailDto>
                {
                    Success = true,
                    Data = station,
                    Message = "Schedule updated successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating schedule for station {StationId}", id);
                return StatusCode(500, new ApiResponse<StationDetailDto>
                {
                    Success = false,
                    Message = "An error occurred while updating the schedule"
                });
            }
        }

        // Retrieves all active charging stations
        [HttpGet("active")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<StationDto>>>> GetActiveStations()
        {
            try
            {
                var stations = await _stationService.GetActiveStationsAsync();
                
                return Ok(new ApiResponse<List<StationDto>>
                {
                    Success = true,
                    Data = stations,
                    Message = $"Retrieved {stations.Count} active stations successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active stations");
                return StatusCode(500, new ApiResponse<List<StationDto>>
                {
                    Success = false,
                    Message = "An error occurred while retrieving active stations"
                });
            }
        }

        // Retrieves nearby charging stations within specified radius (for mobile map view)
        [HttpGet("nearby")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<StationDto>>>> GetNearbyStations(
            [FromQuery] double lat,
            [FromQuery] double lng,
            [FromQuery] double radius = 10.0)
        {
            try
            {
                if (lat < -90 || lat > 90 || lng < -180 || lng > 180)
                {
                    return BadRequest(new ApiResponse<List<StationDto>>
                    {
                        Success = false,
                        Message = "Invalid latitude or longitude values"
                    });
                }

                if (radius <= 0 || radius > 100)
                {
                    return BadRequest(new ApiResponse<List<StationDto>>
                    {
                        Success = false,
                        Message = "Radius must be between 0 and 100 km"
                    });
                }

                var stations = await _stationService.GetNearbyStationsAsync(lat, lng, radius);
                
                return Ok(new ApiResponse<List<StationDto>>
                {
                    Success = true,
                    Data = stations,
                    Message = $"Retrieved {stations.Count} nearby stations within {radius}km"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving nearby stations");
                return StatusCode(500, new ApiResponse<List<StationDto>>
                {
                    Success = false,
                    Message = "An error occurred while retrieving nearby stations"
                });
            }
        }
    }
}