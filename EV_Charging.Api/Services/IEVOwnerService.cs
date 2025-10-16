/*
 * IEVOwnerService.cs
 * IT22363916
 * Perers N. D. V. O.
 * 
 * Service interface for EV Owner operations
 */
using EV_Charging.Api.Models;
using EV_Charging.Api.DTOs.EVOwner;

namespace EV_Charging.Api.Services
{
    public interface IEVOwnerService
    {
        Task<EVOwner?> Register(EVOwnerRegisterRequest request);
        Task<EVOwner?> Authenticate(string email, string password);
        Task<EVOwner?> GetEVOwnerByNIC(string nic);
        Task<EVOwner?> GetEVOwnerByEmail(string email);
        Task<EVOwner?> GetEVOwnerById(string id);
        Task<List<EVOwner>> GetAllEVOwners();
        Task<EVOwner?> UpdateEVOwner(string nic, EVOwnerUpdateRequest request);
        Task<bool> DeactivateEVOwner(string nic);
        Task<bool> ActivateEVOwner(string nic);
        Task<bool> DeleteEVOwner(string nic);
    }
}