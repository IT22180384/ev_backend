/*
 * IJwtService.cs
 * IT22363916
 * Perers N. D. V. O.
 * 
 * Defines contract for JWT token generation and validation
 */
using EV_Charging.Api.Models;

namespace EV_Charging.Api.Services
{
    public interface IJwtService
    {
        string GenerateToken(User user);
        string GenerateToken(EVOwner evOwner);
        string? ValidateToken(string token);
    }
}