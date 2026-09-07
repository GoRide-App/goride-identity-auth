using SRC.Dtos;
using SRC.Entities;

namespace SRC.Services.Interfaces;

public interface IDriverProfileService
{
    Task<DriverProfile> AddProfile(string sub, CreateDriverProfileRequestDto request);
    Task<List<DriverProfile>> getAllProfiles();
    Task<VehicleDto?> GetVehicleById(string sub);
    Task<DriverProfile?> updateStatus(string driverSub, int statusNum);
    Task<DriverProfile?> UpdateVehicle(string driverSub, string usrSub, UpdateVehicleDto request, bool isAdmin);
}