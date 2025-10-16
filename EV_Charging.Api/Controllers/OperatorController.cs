/*
 * OperatorController.cs
 * IT22267504
 * Methmini, K. A. T.
 * 
 * This controller manages station operators for automatic assignment to bookings
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EV_Charging.Api.Services;
using EV_Charging.Api.DTOs;
using System.Security.Claims;
using EV_Charging.Api.Models;

namespace EV_Charging.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OperatorsController : ControllerBase
    {
        private readonly IOperatorService _operatorService;
        private readonly ILogger<OperatorsController> _logger;

        public OperatorsController(IOperatorService operatorService, ILogger<OperatorsController> logger)
        {
            _operatorService = operatorService;
            _logger = logger;
        }

        // Get all operators in the system (Admin only)
        [HttpGet]
        [Authorize(Roles = "Admin,Backoffice")]
        public async Task<ActionResult<ApiResponse<List<OperatorDto>>>> GetOperators()
        {
            try
            {
                var operators = await _operatorService.GetOperatorsAsync();
                
                return Ok(new ApiResponse<List<OperatorDto>>
                {
                    Success = true,
                    Data = operators,
                    Message = $"Retrieved {operators.Count} operators successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving operators");
                return StatusCode(500, new ApiResponse<List<OperatorDto>>
                {
                    Success = false,
                    Message = "An error occurred while retrieving operators"
                });
            }
        }

        // Get a specific operator by their ID (Admin only)
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,Backoffice")]
        public async Task<ActionResult<ApiResponse<OperatorDto>>> GetOperator(string id)
        {
            try
            {
                var op = await _operatorService.GetOperatorByIdAsync(id);
                
                if (op == null)
                {
                    return NotFound(new ApiResponse<OperatorDto>
                    {
                        Success = false,
                        Message = "Operator not found"
                    });
                }

                return Ok(new ApiResponse<OperatorDto>
                {
                    Success = true,
                    Data = op,
                    Message = "Operator retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving operator {OperatorId}", id);
                return StatusCode(500, new ApiResponse<OperatorDto>
                {
                    Success = false,
                    Message = "An error occurred while retrieving operator details"
                });
            }
        }

        // Create a new station operator (Admin only)
        [HttpPost]
        [Authorize(Roles = "Admin,Backoffice")]
        public async Task<ActionResult<ApiResponse<OperatorDto>>> CreateOperator([FromBody] CreateOperatorDto createOperatorDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<OperatorDto>
                    {
                        Success = false,
                        Message = "Invalid operator data",
                        Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                    });
                }

                var op = await _operatorService.CreateOperatorAsync(createOperatorDto);

                return CreatedAtAction(
                    nameof(GetOperator),
                    new { id = op.Id },
                    new ApiResponse<OperatorDto>
                    {
                        Success = true,
                        Data = op,
                        Message = "Operator created successfully"
                    });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Validation failed when creating operator");
                return BadRequest(new ApiResponse<OperatorDto>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating operator");
                return StatusCode(500, new ApiResponse<OperatorDto>
                {
                    Success = false,
                    Message = "An error occurred while creating the operator"
                });
            }
        }

        // Update an existing station operator (Admin only)
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Backoffice")]
        public async Task<ActionResult<ApiResponse<OperatorDto>>> UpdateOperator(string id, [FromBody] UpdateOperatorDto updateOperatorDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<OperatorDto>
                    {
                        Success = false,
                        Message = "Invalid operator data",
                        Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                    });
                }

                var op = await _operatorService.UpdateOperatorAsync(id, updateOperatorDto);

                if (op == null)
                {
                    return NotFound(new ApiResponse<OperatorDto>
                    {
                        Success = false,
                        Message = "Operator not found"
                    });
                }

                return Ok(new ApiResponse<OperatorDto>
                {
                    Success = true,
                    Data = op,
                    Message = "Operator updated successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating operator {OperatorId}", id);
                return StatusCode(500, new ApiResponse<OperatorDto>
                {
                    Success = false,
                    Message = "An error occurred while updating the operator"
                });
            }
        }

        // Deactivate a station operator (Admin only)
        [HttpPatch("{id}/deactivate")]
        [Authorize(Roles = "Admin,Backoffice")]
        public async Task<ActionResult<ApiResponse<object>>> DeactivateOperator(string id)
        {
            try
            {
                var result = await _operatorService.DeactivateOperatorAsync(id);

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
                _logger.LogError(ex, "Error deactivating operator {OperatorId}", id);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred while deactivating the operator"
                });
            }
        }

        // Get all operators assigned to a specific charging station (Admin only)
        [HttpGet("station/{stationId}")]
        [Authorize(Roles = "Admin,Backoffice")]
        public async Task<ActionResult<ApiResponse<List<OperatorDto>>>> GetOperatorsByStation(string stationId)
        {
            try
            {
                var operators = await _operatorService.GetOperatorsByStationAsync(stationId);
                
                return Ok(new ApiResponse<List<OperatorDto>>
                {
                    Success = true,
                    Data = operators,
                    Message = $"Retrieved {operators.Count} operators for station successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving operators for station {StationId}", stationId);
                return StatusCode(500, new ApiResponse<List<OperatorDto>>
                {
                    Success = false,
                    Message = "An error occurred while retrieving station operators"
                });
            }
        }

        // Find an available operator for a station at specific time (Internal use)
        [HttpGet("available")]
        [Authorize(Roles = "Admin,StationOperator")]
        public async Task<ActionResult<ApiResponse<OperatorDto>>> GetAvailableOperator(
            [FromQuery] string stationId,
            [FromQuery] string reservationDateTime)
        {
            try
            {
                if (!DateTime.TryParse(reservationDateTime, out DateTime parsedDate))
                {
                    return BadRequest(new ApiResponse<OperatorDto>
                    {
                        Success = false,
                        Message = "Invalid date format"
                    });
                }

                var op = await _operatorService.GetAvailableOperatorForStationAsync(stationId, parsedDate);

                if (op == null)
                {
                    return NotFound(new ApiResponse<OperatorDto>
                    {
                        Success = false,
                        Message = "No available operator found for the specified time"
                    });
                }

                return Ok(new ApiResponse<OperatorDto>
                {
                    Success = true,
                    Data = op,
                    Message = "Available operator found"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding available operator for station {StationId}", stationId);
                return StatusCode(500, new ApiResponse<OperatorDto>
                {
                    Success = false,
                    Message = "An error occurred while finding available operator"
                });
            }
        }

        // Complete a charging session with energy consumption data (Operator only)
        [HttpPatch("session/{bookingId}/complete")]
        [Authorize(Roles = "Admin,StationOperator")]
        public async Task<ActionResult<ApiResponse<object>>> CompleteSession(string bookingId, [FromBody] FinalizeSessionDto finalizeDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Invalid session data",
                        Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                    });
                }

                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var isAdmin = User.IsInRole("Admin");

                if (string.IsNullOrEmpty(currentUserId) && !isAdmin)
                {
                    return Forbid();
                }

                var result = await _operatorService.CompleteSessionAsync(bookingId, finalizeDto, currentUserId ?? string.Empty, isAdmin);

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
                _logger.LogError(ex, "Error completing session for booking {BookingId}", bookingId);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred while completing the session"
                });
            }
        }

        // Get sessions assigned to the current operator with optional status filtering
        [HttpGet("sessions/assigned")]
        [Authorize(Roles = "StationOperator")]
        public async Task<ActionResult<ApiResponse<List<BookingSessionDto>>>> GetAssignedSessions([FromQuery] string? status = null)
        {
            try
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Forbid();
                }

                BookingStatus? statusFilter = null;
                if (!string.IsNullOrWhiteSpace(status))
                {
                    if (!Enum.TryParse<BookingStatus>(status, true, out var parsedStatus))
                    {
                        return BadRequest(new ApiResponse<List<BookingSessionDto>>
                        {
                            Success = false,
                            Message = "Invalid status value"
                        });
                    }
                    statusFilter = parsedStatus;
                }

                var sessions = await _operatorService.GetAssignedSessionsAsync(currentUserId, statusFilter);

                return Ok(new ApiResponse<List<BookingSessionDto>>
                {
                    Success = true,
                    Data = sessions,
                    Message = sessions.Count == 0
                        ? "No sessions found for the requested filter"
                        : $"Retrieved {sessions.Count} session(s)"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving assigned sessions");
                return StatusCode(500, new ApiResponse<List<BookingSessionDto>>
                {
                    Success = false,
                    Message = "An error occurred while retrieving assigned sessions"
                });
            }
        }
    }
}