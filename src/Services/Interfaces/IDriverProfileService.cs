using SRC.Dtos;
using SRC.Entities;
using SRC.Enums;

namespace SRC.Services.Interfaces;

public interface IDriverProfileService
{
    Task<DriverProfile> AddProfile(string sub, CreateDriverProfileRequestDto request);
    Task<VehicleDto?> GetVehicleById(string sub);
    Task<DriverProfile?> UpdateVehicle(string driverSub, string usrSub, UpdateVehicleDto request, bool isAdmin);
    Task<IEnumerable<DriverAccountAdminDto>> GetAllDriverAccountsAsync(DriverStatus? statusFilter = null);
    Task<DriverAccountAdminDto?> GetDriverAccountByIdAsync(string driverId);
    Task<DriverProfile?> UpdateDriverStatusByAdminAsync(string driverId, DriverStatus newStatus, string reason);
    Task<DriverStateEnforcementResponseDto> CheckDriverStateEnforcementAsync(string driverId);
    Task<DriverStateEnforcementResponseDto> GoOnlineAsync(string driverId);
    Task<DriverStateEnforcementResponseDto> GoOfflineAsync(string driverId);
}