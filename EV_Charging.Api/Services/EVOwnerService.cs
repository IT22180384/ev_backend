/*
 * EVOwnerService.cs
 * IT22363916
 * Perers N. D. V. O.
 * 
 * Service implementation for EV Owner operations including registration, authentication,
 * and profile management for mobile app users
 */
using MongoDB.Driver;
using EV_Charging.Api.Data;
using EV_Charging.Api.Models;
using EV_Charging.Api.DTOs.EVOwner;

namespace EV_Charging.Api.Services
{
    public class EVOwnerService : IEVOwnerService
    {
        private readonly MongoDbContext _context;

        public EVOwnerService(MongoDbContext context)
        {
            _context = context;
        }

        // Registers a new EV owner after checking for duplicate email/NIC
        public async Task<EVOwner?> Register(EVOwnerRegisterRequest request)
        {
            // Check for existing EV owner by email or NIC
            var existingByEmail = await GetEVOwnerByEmail(request.Email);
            var existingByNIC = await GetEVOwnerByNIC(request.NIC);
            
            if (existingByEmail != null || existingByNIC != null)
                return null;

            var evOwner = new EVOwner
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Email = request.Email,
                NIC = request.NIC,
                Password = request.Password, 
                Phone = request.Phone,
                VehicleType = request.VehicleType,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.EVOwners.InsertOneAsync(evOwner);
            return evOwner;
        }

        // Authenticates an EV owner by email and password, returns user if valid and active
        public async Task<EVOwner?> Authenticate(string email, string password)
        {
            var evOwner = await _context.EVOwners
                .Find(u => u.Email == email && u.IsActive)
                .FirstOrDefaultAsync();

            if (evOwner == null)
                return null;

            // Verify password (in production, use hashed passwords)
            if (evOwner.Password != password)
                return null;

            return evOwner;
        }

        // Retrieves an EV owner by their National Identity Card (NIC) number
        public async Task<EVOwner?> GetEVOwnerByNIC(string nic)
        {
            return await _context.EVOwners
                .Find(u => u.NIC == nic)
                .FirstOrDefaultAsync();
        }

        // Retrieves an EV owner by their email address
        public async Task<EVOwner?> GetEVOwnerByEmail(string email)
        {
            return await _context.EVOwners
                .Find(u => u.Email == email)
                .FirstOrDefaultAsync();
        }

        // Retrieves an EV owner by their unique system ID
        public async Task<EVOwner?> GetEVOwnerById(string id)
        {
            return await _context.EVOwners
                .Find(u => u.Id == id)
                .FirstOrDefaultAsync();
        }

        // Retrieves all EV owners sorted by creation date (newest first)
        public async Task<List<EVOwner>> GetAllEVOwners()
        {
            return await _context.EVOwners
                .Find(_ => true)
                .SortByDescending(u => u.CreatedAt)
                .ToListAsync();
        }

        // Updates an EV owner's profile information (name, email, phone, vehicle type)
        public async Task<EVOwner?> UpdateEVOwner(string nic, EVOwnerUpdateRequest request)
        {
            var evOwner = await GetEVOwnerByNIC(nic);
            if (evOwner == null) return null;

            // Update only the allowed fields
            evOwner.Name = request.Name;
            evOwner.Email = request.Email;
            evOwner.Phone = request.Phone;
            evOwner.VehicleType = request.VehicleType;
            evOwner.UpdatedAt = DateTime.UtcNow;

            var result = await _context.EVOwners
                .ReplaceOneAsync(u => u.NIC == nic, evOwner);

            return result.IsAcknowledged && result.ModifiedCount > 0 ? evOwner : null;
        }

        // Deactivates an EV owner account (sets IsActive to false)
        public async Task<bool> DeactivateEVOwner(string nic)
        {
            var evOwner = await GetEVOwnerByNIC(nic);
            if (evOwner == null) return false;

            evOwner.IsActive = false;
            evOwner.UpdatedAt = DateTime.UtcNow;
            
            var result = await _context.EVOwners
                .ReplaceOneAsync(u => u.NIC == nic, evOwner);

            return result.IsAcknowledged && result.ModifiedCount > 0;
        }

        // Reactivates a deactivated EV owner account (sets IsActive to true)
        public async Task<bool> ActivateEVOwner(string nic)
        {
            var evOwner = await GetEVOwnerByNIC(nic);
            if (evOwner == null) return false;
            
            evOwner.IsActive = true;
            evOwner.UpdatedAt = DateTime.UtcNow;
            
            var result = await _context.EVOwners
                .ReplaceOneAsync(u => u.NIC == nic, evOwner);

            return result.IsAcknowledged && result.ModifiedCount > 0;
        }

        // Permanently deletes an EV owner from the database
        public async Task<bool> DeleteEVOwner(string nic)
        {
            var result = await _context.EVOwners
                .DeleteOneAsync(u => u.NIC == nic);
                
            return result.IsAcknowledged && result.DeletedCount > 0;
        }
    }
}