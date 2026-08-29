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
            if(exists) throw new HttpRequestException("A profile already exists for this driver!!!");

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
    }
}