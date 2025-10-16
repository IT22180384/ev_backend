/*
 * UserService.cs
 * IT22363916
 * Perers N. D. V. O.
 * 
 * Service for managing web user accounts (Admin and Station Operator roles)
 * Handles authentication, registration, and user management operations
 */

using MongoDB.Driver;
using EV_Charging.Api.Data;
using EV_Charging.Api.Models;
using EV_Charging.Api.DTOs.User;
using System.Security.Cryptography;
using System.Text;

namespace EV_Charging.Api.Services
{
    public class UserService : IUserService
    {
        private readonly MongoDbContext _context;

        public UserService(MongoDbContext context)
        {
            _context = context;
        }

        // Authenticates a web user (Admin/StationOperator) by email and password
        public async Task<User?> Authenticate(string email, string password)
        {
            var user = await _context.Users
                .Find(u => u.Email == email && u.IsActive)
                .FirstOrDefaultAsync();

            if (user == null)
                return null;

            // Verify password (in real scenario, use hashed passwords)
            // For now using plain text as per current implementation
            if (user.Password != password)
                return null;

            return user;
        }

        // Registers a new web user after checking for duplicate email/NIC
        public async Task<User?> Register(RegisterRequest request)
        {
            // Check for existing user by email or NIC
            var existingByEmail = await GetUserByEmail(request.Email);
            var existingByNIC = await GetUserByNIC(request.NIC);
            
            if (existingByEmail != null || existingByNIC != null)
                return null;

            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Email = request.Email,
                NIC = request.NIC,
                Password = request.Password, // In production, hash this password
                Role = request.Role ?? "StationOperator",
                Phone = request.Phone,
                StationId = request.StationId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.Users.InsertOneAsync(user);

            if (string.Equals(user.Role, UserRoles.StationOperator, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(user.StationId))
            {
                var operatorProfile = new Operator
                {
                    UserId = user.Id,
                    StationId = user.StationId,
                    Name = user.Name,
                    Email = user.Email,
                    Phone = user.Phone,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _context.Operators.InsertOneAsync(operatorProfile);
            }

            return user;
        }

        // Retrieves a web user by their National Identity Card (NIC) number
        public async Task<User?> GetUserByNIC(string nic)
        {
            return await _context.Users
                .Find(u => u.NIC == nic)
                .FirstOrDefaultAsync();
        }

        // Retrieves a web user by their email address
        public async Task<User?> GetUserByEmail(string email)
        {
            return await _context.Users
                .Find(u => u.Email == email)
                .FirstOrDefaultAsync();
        }

        // Updates a web user's complete information in the database
        public async Task<bool> UpdateUser(User user)
        {
            user.UpdatedAt = DateTime.UtcNow;
            var result = await _context.Users
                .ReplaceOneAsync(u => u.NIC == user.NIC, user);

            return result.IsAcknowledged && result.ModifiedCount > 0;
        }

        // Updates a web user's profile information (name, email, phone) by NIC
        public async Task<User?> UpdateUserProfile(string nic, UpdateUserRequest request)
        {
            var user = await GetUserByNIC(nic);
            if (user == null) return null;

            // Update only the allowed fields
            user.Name = request.Name;
            user.Email = request.Email;
            user.Phone = request.Phone;
            user.UpdatedAt = DateTime.UtcNow;

            var result = await _context.Users
                .ReplaceOneAsync(u => u.NIC == nic, user);

            return result.IsAcknowledged && result.ModifiedCount > 0 ? user : null;
        }

        // Deactivates a web user account (sets IsActive to false)
        public async Task<bool> DeactivateUser(string nic)
        {
            var user = await GetUserByNIC(nic);
            if (user == null) return false;

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            return await UpdateUser(user);
        }

        // Retrieves all web users sorted by creation date (newest first)
        public async Task<List<User>> GetAllUsers()
        {
            return await _context.Users
                .Find(_ => true)
                .SortByDescending(u => u.CreatedAt)
                .ToListAsync();
        }
        
        // Reactivates a deactivated web user account (sets IsActive to true)
        public async Task<bool> ActivateUser(string nic)
        {
            var user = await GetUserByNIC(nic);
            if (user == null) return false;
            
            user.IsActive = true;
            user.UpdatedAt = DateTime.UtcNow;
            return await UpdateUser(user);
        }

        // Retrieves all web users filtered by their role (Admin or StationOperator)
        public async Task<List<User>> GetUsersByRole(string role)
        {
            return await _context.Users
                .Find(u => u.Role == role)
                .SortByDescending(u => u.CreatedAt)
                .ToListAsync();
        }

        // Permanently deletes a web user from the database by NIC
        public async Task<bool> DeleteUser(string nic)
        {
            var result = await _context.Users
                .DeleteOneAsync(u => u.NIC == nic);
                
            return result.IsAcknowledged && result.DeletedCount > 0;
        }

        // Retrieves a web user by their unique system ID
        public async Task<User?> GetUserById(string id)
        {
            return await _context.Users
                .Find(u => u.Id == id)
                .FirstOrDefaultAsync();
        }

        // Updates a web user's profile information (name, email, phone) by ID
        public async Task<User?> UpdateUserById(string id, UpdateUserRequest request)
        {
            var user = await GetUserById(id);
            if (user == null) return null;

            // Update only the allowed fields
            user.Name = request.Name;
            user.Email = request.Email;
            user.Phone = request.Phone;
            user.UpdatedAt = DateTime.UtcNow;

            var result = await _context.Users
                .ReplaceOneAsync(u => u.Id == id, user);

            return result.IsAcknowledged && result.ModifiedCount > 0 ? user : null;
        }

        // Permanently deletes a web user from the database by ID
        public async Task<bool> DeleteUserById(string id)
        {
            var result = await _context.Users
                .DeleteOneAsync(u => u.Id == id);
                
            return result.IsAcknowledged && result.DeletedCount > 0;
        }

        // Changes a web user's role (Admin can switch between Admin and StationOperator)
        public async Task<bool> ChangeUserRoleAsync(string nic, string newRole)
        {
            // Validate that the new role is valid
            if (newRole != "Admin" && newRole != "StationOperator")
                return false;
            
            var user = await GetUserByNIC(nic);
            if (user == null) return false;

            // Update the user's role
            user.Role = newRole;
            user.UpdatedAt = DateTime.UtcNow;
            
            return await UpdateUser(user);
        }

        public async Task<bool> HasAnyUsersAsync()
        {
            var count = await _context.Users.CountDocumentsAsync(_ => true);
            return count > 0;
        }

        // Hashes a plain text password using SHA256 (for future security enhancement)
        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}