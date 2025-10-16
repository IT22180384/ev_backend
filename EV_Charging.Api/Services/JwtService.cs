/*
 * JwtService.cs
 * IT22363916
 * Perers N. D. V. O.
 * 
 * Implements JWT token generation and validation using .env configuration
 * Handles token creation for both web users and EV owners with role-based claims
 */
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using EV_Charging.Api.Models;

namespace EV_Charging.Api.Services
{
    public class JwtService : IJwtService
    {
        private readonly string _secretKey;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly int _expireHours;

        public JwtService(IConfiguration configuration)
        {
            // Get values from environment variables (loaded from .env)
            _secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
                        ?? throw new InvalidOperationException("JWT_SECRET_KEY environment variable is not set");
            _issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") 
                     ?? "EVCharging-API";
            _audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") 
                       ?? "EVCharging-Client";
            _expireHours = int.Parse(Environment.GetEnvironmentVariable("JWT_EXPIRE_HOURS") 
                       ?? "24");
            
            // Validate secret key length
            if (_secretKey.Length < 32)
            {
                throw new InvalidOperationException("JWT secret key must be at least 32 characters long");
            }
        }

        // Generates a JWT token for web users (Admin/StationOperator) with role-based claims
        public string GenerateToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_secretKey);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id ?? string.Empty),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Name, user.Name ?? string.Empty),
                new Claim(ClaimTypes.Role, user.Role ?? UserRoles.StationOperator),
                new Claim("NIC", user.NIC ?? string.Empty),
                new Claim("IsActive", user.IsActive.ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(_expireHours),
                Issuer = _issuer,
                Audience = _audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        // Generates a JWT token for EV owners with vehicle-specific claims
        public string GenerateToken(EVOwner evOwner)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_secretKey);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, evOwner.Id ?? string.Empty),
                new Claim(ClaimTypes.Email, evOwner.Email ?? string.Empty),
                new Claim(ClaimTypes.Name, evOwner.Name ?? string.Empty),
                new Claim(ClaimTypes.Role, "EVOwner"),
                new Claim("NIC", evOwner.NIC ?? string.Empty),
                new Claim("IsActive", evOwner.IsActive.ToString()),
                new Claim("VehicleType", evOwner.VehicleType ?? string.Empty)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(_expireHours),
                Issuer = _issuer,
                Audience = _audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        // Validates JWT token and returns user ID if valid, null if invalid or expired
        public string? ValidateToken(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_secretKey);

            try
            {
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _issuer,
                    ValidateAudience = true,
                    ValidAudience = _audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                var jwtToken = (JwtSecurityToken)validatedToken;
                var userId = jwtToken.Claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value;

                return string.IsNullOrEmpty(userId) ? null : userId;
            }
            catch (SecurityTokenExpiredException)
            {
                Console.WriteLine("Token has expired");
                return null;
            }
            catch (SecurityTokenInvalidIssuerException)
            {
                Console.WriteLine("Token issuer is invalid");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Token validation failed: {ex.Message}");
                return null;
            }
        }
        
        // Helper method to extract and return the expiration date from a JWT token
        public DateTime GetTokenExpiration(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            return jwtToken.ValidTo;
        }
    }
}