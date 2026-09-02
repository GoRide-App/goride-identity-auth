using SRC.Dtos;
using SRC.Entities;

namespace SRC.Services.Interfaces;

public interface IDriverProfileService
{
    /// <summary>Creates the driver profile. Returns null when one already exists for <paramref name="sub"/>.</summary>
    Task<DriverProfile?> AddProfile(string sub, CreateDriverProfileRequestDto request);
    Task<VehicleDto?> GetVehicleById(string sub);
    Task<DriverProfile?> UpdateVehicle(string driverSub, string usrSub, UpdateVehicleDto request, bool isAdmin);
}
