using SRC.Dtos;
using SRC.Entities;

namespace SRC.Services.Interfaces;

public interface IDriverProfileService
{
    Task<DriverProfile> AddProfile(string sub, CreateDriverProfileRequestDto request);
    Task<VehicleDto?> GetVehicleById(string sub);
    Task<DriverProfile?> UpdateVehicle(string driverSub, string usrSub, UpdateVehicleDto request, bool isAdmin);
}