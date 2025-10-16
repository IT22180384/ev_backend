/*
 * EVOwnerDto.cs
 * IT22363916
 * Perers N. D. V. O.
 * 
 * Data Transfer Objects for EV Owner operations
 * Supports EV Owner authentication and management
 */
using EV_Charging.Api.Models;

namespace EV_Charging.Api.DTOs.EVOwner
{
    public class EVOwnerLoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class EVOwnerRegisterRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string NIC { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
    }

    public class EVOwnerUpdateRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
    }

    public class EVOwnerResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Models.EVOwner? EVOwner { get; set; }
        public string Token { get; set; } = string.Empty;
    }

    public class EVOwnerDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string NIC { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}