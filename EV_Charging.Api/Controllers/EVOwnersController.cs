/*
 * EVOwnersController.cs
 * IT22363916
 * Perers N. D. V. O.
 * 
 * Controller for managing EV owner accounts, authentication, and profile management
 * Handles mobile app user registration, login, and account operations
 */

using Microsoft.AspNetCore.Mvc;
using EV_Charging.Api.Models;
using EV_Charging.Api.Services;
using EV_Charging.Api.DTOs.EVOwner;

namespace EV_Charging.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EVOwnersController : ControllerBase
    {
        private readonly IEVOwnerService _evOwnerService;
        private readonly IJwtService _jwtService;

        public EVOwnersController(IEVOwnerService evOwnerService, IJwtService jwtService)
        {
            _evOwnerService = evOwnerService;
            _jwtService = jwtService;
        }

        // Authenticate EV owner with email and password (Mobile app login)
        [HttpPost("login")]
        public async Task<ActionResult<EVOwnerResponse>> Login([FromBody] EVOwnerLoginRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
                {
                    return BadRequest(new EVOwnerResponse { 
                        Success = false, 
                        Message = "Email and password are required" 
                    });
                }

                var evOwner = await _evOwnerService.Authenticate(request.Email, request.Password);
                
                if (evOwner == null)
                    return Unauthorized(new EVOwnerResponse { 
                        Success = false, 
                        Message = "Invalid email or password" 
                    });
                
                if (!evOwner.IsActive)
                    return Unauthorized(new EVOwnerResponse { 
                        Success = false, 
                        Message = "Account is deactivated. Please contact support." 
                    });
                
                // Generate JWT token for EV owner
                var token = _jwtService.GenerateToken(evOwner);
                
                return Ok(new EVOwnerResponse {
                    Success = true,
                    Message = "Login successful",
                    EVOwner = evOwner,
                    Token = token
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new EVOwnerResponse {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        // Register a new EV owner account (Mobile app or Admin)
        [HttpPost("register")]
        public async Task<ActionResult<EVOwnerResponse>> CreateEVOwner([FromBody] EVOwnerRegisterRequest request)
        {
            try
            {
                var evOwner = await _evOwnerService.Register(request);
                
                if (evOwner == null)
                    return Conflict(new EVOwnerResponse {
                        Success = false,
                        Message = "EV Owner with this email or NIC already exists"
                    });
                
                var token = _jwtService.GenerateToken(evOwner);
                
                return Ok(new EVOwnerResponse {
                    Success = true,
                    Message = "EV Owner created successfully",
                    EVOwner = evOwner,
                    Token = token
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new EVOwnerResponse {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        // Get all EV owners in the system (Admin only)
        [HttpGet]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<EVOwner>>> GetAllEVOwners()
        {
            try
            {
                var evOwners = await _evOwnerService.GetAllEVOwners();
                return Ok(evOwners);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false, 
                    message = $"Internal server error: {ex.Message}" 
                });
            }
        }

        // Get a specific EV owner by their NIC number
        [HttpGet("{nic}")]
        public async Task<ActionResult<EVOwner>> GetEVOwnerByNIC(string nic)
        {
            try
            {
                var evOwner = await _evOwnerService.GetEVOwnerByNIC(nic);
                
                if (evOwner == null)
                    return NotFound(new { success = false, message = "EV Owner not found" });
                    
                return Ok(evOwner);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false, 
                    message = $"Internal server error: {ex.Message}" 
                });
            }
        }

        // Update EV owner profile and vehicle information
        [HttpPut("{nic}")]
        public async Task<ActionResult<EVOwner>> UpdateEVOwner(string nic, [FromBody] EVOwnerUpdateRequest request)
        {
            try
            {
                var evOwner = await _evOwnerService.UpdateEVOwner(nic, request);
                
                if (evOwner == null)
                    return NotFound(new { success = false, message = "EV Owner not found" });
                
                return Ok(new {
                    success = true,
                    message = "EV Owner updated successfully",
                    evOwner = evOwner
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false, 
                    message = $"Internal server error: {ex.Message}" 
                });
            }
        }

        // Deactivate EV owner account (prevents login and new reservations)
        [HttpPatch("{nic}/deactivate")]
        public async Task<ActionResult> DeactivateEVOwner(string nic)
        {
            try
            {
                var evOwner = await _evOwnerService.GetEVOwnerByNIC(nic);
                
                if (evOwner == null)
                    return NotFound(new { success = false, message = "EV Owner not found" });
                
                var success = await _evOwnerService.DeactivateEVOwner(nic);
                
                return success ? Ok(new { success = true, message = "EV Owner deactivated successfully" }) 
                            : BadRequest(new { success = false, message = "Failed to deactivate EV Owner" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false, 
                    message = $"Internal server error: {ex.Message}" 
                });
            }
        }

        // Reactivate a deactivated EV owner account (Admin only)
        [HttpPatch("{nic}/activate")]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public async Task<ActionResult> ActivateEVOwner(string nic)
        {
            try
            {
                var evOwner = await _evOwnerService.GetEVOwnerByNIC(nic);
                
                if (evOwner == null)
                    return NotFound(new { success = false, message = "EV Owner not found" });
                
                var success = await _evOwnerService.ActivateEVOwner(nic);
                
                return success ? Ok(new { success = true, message = "EV Owner activated successfully" }) 
                            : BadRequest(new { success = false, message = "Failed to activate EV Owner" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false, 
                    message = $"Internal server error: {ex.Message}" 
                });
            }
        }

        // Permanently delete EV owner and all their data
        [HttpDelete("{nic}")]
        public async Task<ActionResult> DeleteEVOwner(string nic)
        {
            try
            {
                var evOwner = await _evOwnerService.GetEVOwnerByNIC(nic);
                
                if (evOwner == null)
                    return NotFound(new { success = false, message = "EV Owner not found" });
                
                var success = await _evOwnerService.DeleteEVOwner(nic);
                
                return success ? Ok(new { success = true, message = "EV Owner deleted successfully" }) 
                            : BadRequest(new { success = false, message = "Failed to delete EV Owner" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false, 
                    message = $"Internal server error: {ex.Message}" 
                });
            }
        }
    }
}