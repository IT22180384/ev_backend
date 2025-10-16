/*
 * AuthController.cs
 * IT22363916
 * Perers N. D. V. O.
 * 
 * Controller for handling user authentication operations with JWT token generation,
 * registration, and user management for Admin and Station Operator roles
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EV_Charging.Api.Models;
using EV_Charging.Api.Services;
using EV_Charging.Api.DTOs.User;

namespace EV_Charging.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IJwtService _jwtService;
        
        public AuthController(IUserService userService, IJwtService jwtService)
        {
            _userService = userService;
            _jwtService = jwtService;
        }

        // Maps User entity to response object for API output
        private object MapUserResponse(User user)
        {
            return new
            {
                id = user.Id,
                name = user.Name,
                email = user.Email,
                nic = user.NIC,
                password = user.Password,
                role = user.Role,
                isActive = user.IsActive,
                phone = user.Phone,
                stationId = user.StationId,
                createdAt = user.CreatedAt,
                updatedAt = user.UpdatedAt
            };
        }
        
        // Authenticates web user (Admin or Station Operator) and returns JWT token
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
                {
                    return BadRequest(new AuthResponse { 
                        Success = false, 
                        Message = "Email and password are required" 
                    });
                }

                var user = await _userService.Authenticate(request.Email, request.Password);
                
                if (user == null)
                    return Unauthorized(new AuthResponse { 
                        Success = false, 
                        Message = "Invalid email or password" 
                    });
                
                if (!user.IsActive)
                    return Unauthorized(new AuthResponse { 
                        Success = false, 
                        Message = "Account is deactivated. Please contact backoffice." 
                    });
                
                // Generate real JWT token
                var token = _jwtService.GenerateToken(user);
                
                return Ok(new AuthResponse {
                    Success = true,
                    Message = "Login successful",
                    User = MapUserResponse(user),
                    Token = token
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new AuthResponse {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }
        
        // Creates a new web user (Admin or Station Operator) - Admin only
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password) || 
                    string.IsNullOrEmpty(request.NIC) || string.IsNullOrEmpty(request.Name))
                {
                    return BadRequest(new AuthResponse {
                        Success = false,
                        Message = "Name, Email, NIC and Password are required"
                    });
                }

                var hasAnyUser = await _userService.HasAnyUsersAsync();
                if (hasAnyUser)
                {
                    if (!User.Identity?.IsAuthenticated ?? true)
                    {
                        return Forbid();
                    }

                    if (!User.IsInRole(UserRoles.Admin))
                    {
                        return Forbid();
                    }
                }

                if (string.Equals(request.Role, UserRoles.StationOperator, StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrEmpty(request.StationId))
                {
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "StationId is required when creating a station operator"
                    });
                }

                var user = await _userService.Register(request);
                
                if (user == null)
                    return Conflict(new AuthResponse {
                        Success = false,
                        Message = "User with this email or NIC already exists"
                    });
                
                // Generate real JWT token
                var token = _jwtService.GenerateToken(user);
                
                return Ok(new AuthResponse {
                    Success = true,
                    Message = "Registration successful",
                    User = MapUserResponse(user),
                    Token = token
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new AuthResponse {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }
        
        
        // Retrieves all web users in the system (Admin only)
        [HttpGet("users")]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<User>>> GetAllUsers()
        {
            try
            {
                var users = await _userService.GetAllUsers();
                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false, 
                    message = $"Internal server error: {ex.Message}" 
                });
            }
        }
        
        // Retrieves a specific web user by their ID (Admin only)
        [HttpGet("users/{id}")]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public async Task<ActionResult<User>> GetUserById(string id)
        {
            try
            {
                var user = await _userService.GetUserById(id);
                
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });
                    
                return Ok(user);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false, 
                    message = $"Internal server error: {ex.Message}" 
                });
            }
        }
        
        // Updates an existing web user's information (Admin only)
        [HttpPut("users/{id}")]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public async Task<ActionResult<User>> UpdateUser(string id, [FromBody] UpdateUserRequest request)
        {
            try
            {
                var user = await _userService.UpdateUserById(id, request);
                
                if (user == null)
                    return NotFound(new { success = false, message = "User not found" });
                
                return Ok(new {
                    success = true,
                    message = "User updated successfully",
                    user = user
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
        
        // Permanently deletes a web user from the system - Admin access only
        [HttpDelete("users/{id}")]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeleteUser(string id)
        {
            try
            {
                var success = await _userService.DeleteUserById(id);
                
                return success ? Ok(new { success = true, message = "User deleted successfully" }) 
                            : NotFound(new { success = false, message = "User not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false, 
                    message = $"Internal server error: {ex.Message}" 
                });
            }
        }

        // Changes user role between Admin and StationOperator - Admin access only
        [HttpPatch("users/{id}/role")]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public async Task<ActionResult> ChangeUserRole(string id, [FromBody] ChangeRoleRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.NewRole) || 
                    (request.NewRole != "Admin" && request.NewRole != "StationOperator"))
                {
                    return BadRequest(new { 
                        success = false, 
                        message = "Role must be either 'Admin' or 'StationOperator'" 
                    });
                }

                var success = await _userService.ChangeUserRoleAsync(id, request.NewRole);
                
                return success ? Ok(new { 
                        success = true, 
                        message = $"User role changed to {request.NewRole} successfully" 
                    }) 
                    : NotFound(new { success = false, message = "User not found" });
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

    // DTO for role change request
    public class ChangeRoleRequest
    {
        public string NewRole { get; set; } = string.Empty;
    }
}