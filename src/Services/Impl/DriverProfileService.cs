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

        async Task<DriverProfile?> IDriverProfileService.AddProfile(string sub, CreateDriverProfileRequestDto request)
        {
            var exists = await _context.DriverProfile.AnyAsync(d => d.DriverId == sub);
            if (exists) return null; // the controller turns this into 409 Conflict

            var profile = new DriverProfile
            {
                DriverId = sub,
                VehicleMake = request.VehicleMake.Trim(),
                VehicleModel = request.VehicleModel.Trim(),
                VehiclePlate = request.VehiclePlate.Trim().ToUpperInvariant(),
                VehicleTypeCode = request.VehicleTypeCode.Trim(),
                LicenseNumber = request.LicenseNumber.Trim(),
                LicenseExpiry = request.LicenseExpiry,
                Status = DriverStatus.PendingVerification
            };

            _context.DriverProfile.Add(profile);
            await _context.SaveChangesAsync();
            return profile;
        }

        async Task<DriverProfile?> IDriverProfileService.UpdateVehicle(string sub, string usrSub, UpdateVehicleDto request, bool isAdmin)
        {
            var driver = await _context.DriverProfile.FirstOrDefaultAsync(v => v.DriverId == sub);
            if (driver is null) return null;

            if (!isAdmin && driver.DriverId != usrSub)
                throw new UnauthorizedAccessException("You can only update your own vehicle.");

            driver.VehicleMake = request.VehicleMake.Trim();
            driver.VehicleModel = request.VehicleModel.Trim();
            driver.VehiclePlate = request.VehiclePlate.Trim().ToUpperInvariant();
            driver.VehicleTypeCode = request.VehicleTypeCode.Trim();
            driver.LicenseNumber = request.LicenseNumber.Trim();
            driver.LicenseExpiry = request.LicenseExpiry;

            await _context.SaveChangesAsync();
            return driver;
        }
    }
}
