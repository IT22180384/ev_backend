/*
 * IUserService.cs
 * IT22363916
 * Perers N. D. V. O.
 * 
 * Defines the contract for user management operations (Backoffice & Station Operators only)
 * Follows dependency injection and interface segregation principles
 */
using EV_Charging.Api.Models;
using EV_Charging.Api.DTOs.User;

namespace EV_Charging.Api.Services
{
    public interface IUserService
    {
        Task<User?> Authenticate(string email, string password);
        Task<User?> Register(RegisterRequest request);
        Task<User?> GetUserByNIC(string nic);
        Task<User?> GetUserByEmail(string email);
        Task<bool> UpdateUser(User user);
        Task<bool> DeactivateUser(string nic);
        Task<List<User>> GetAllUsers();

        Task<bool> ActivateUser(string nic);
        Task<List<User>> GetUsersByRole(string role);
        Task<bool> DeleteUser(string nic);
        Task<User?> UpdateUserProfile(string nic, UpdateUserRequest request);
        Task<User?> GetUserById(string id);
        Task<User?> UpdateUserById(string id, UpdateUserRequest request);
        Task<bool> DeleteUserById(string id);
        Task<bool> ChangeUserRoleAsync(string nic, string newRole);
        Task<bool> HasAnyUsersAsync();
    }
}