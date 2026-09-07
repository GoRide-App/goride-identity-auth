using Microsoft.EntityFrameworkCore;
using SRC.Data;
using SRC.Dtos;
using SRC.Entities;
using SRC.Enums;
using SRC.Services.Interfaces;

namespace SRC.Services.Impl
{
    public class DriverProfileServiceImpl : IDriverProfileService
    {
        private readonly AppDbContext _context;

        public DriverProfileServiceImpl(AppDbContext context)
        {
            _context = context;
        }

        async Task<DriverProfile> IDriverProfileService.AddProfile(string sub, CreateDriverProfileRequestDto request)
        {
            var exists = await _context.DriverProfile.AnyAsync(d => d.DriverId == sub);
            if (exists) throw new HttpRequestException("A profile already exists for this driver!!!");

            var profile = new DriverProfile
            {
                DriverId = sub,
                VehicleMake = request.VehicleMake,
                VehicleModel = request.VehicleModel,
                VehiclePlate = request.VehiclePlate,
                VehicleTypeCode = request.VehicleTypeCode,
                LicenseNumber = request.LicenseNumber,
                LicenseExpiry = request.LicenseExpiry,
                Status = DriverStatus.PendingVerification
            };

            _context.DriverProfile.Add(profile);
            await _context.SaveChangesAsync();
            return profile;
        }

        async Task<VehicleDto?> IDriverProfileService.GetVehicleById(string sub)
        {
            return await _context.DriverProfile
                .AsNoTracking()
                .Where(d => d.DriverId == sub)
                .Select(d => new VehicleDto
                {
                    VehicleMake = d.VehicleMake,
                    VehicleModel = d.VehicleModel,
                    VehiclePlate = d.VehiclePlate,
                    VehicleTypeCode = d.VehicleTypeCode,
                    LicenseNumber = d.LicenseNumber,
                    LicenseExpiry = d.LicenseExpiry
                }).FirstOrDefaultAsync();
        }

        async Task<DriverProfile?> IDriverProfileService.UpdateVehicle(string sub, string usrSub, UpdateVehicleDto request, bool isAdmin)
        {
            var driver = await _context.DriverProfile.FirstOrDefaultAsync(v => v.DriverId == sub);
            if (driver is null) return null;

            if (!isAdmin)
            {
                if (driver.DriverId != usrSub)
                    throw new UnauthorizedAccessException("You can only update your own vehicle.");
            }

            driver.VehicleMake = request.VehicleMake;
            driver.VehicleModel = request.VehicleModel;
            driver.VehiclePlate = request.VehiclePlate;
            driver.VehicleTypeCode = request.VehicleTypeCode;
            driver.LicenseNumber = request.LicenseNumber;
            driver.LicenseExpiry = request.LicenseExpiry;

            await _context.SaveChangesAsync();
            return driver;
        }
    }
}