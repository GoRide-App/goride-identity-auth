using Microsoft.EntityFrameworkCore;
using SRC.Data;
using SRC.Dtos;
using SRC.Entities;
using SRC.Services.Interfaces;

namespace SRC.Services.Impl
{
    public class DriverServiceImpl : IDriverService
    {
        private readonly AppDbContext _context;

        public DriverServiceImpl(AppDbContext context)
        {
            _context = context;
        }

        async Task<List<DriverProfile>> IDriverService.GetDrivers(DriverProfileRequestDto requestDto)
        {
            var drivers = await _context.DriverProfile.Where(d => d.VehicleTypeCode == requestDto.VehicleType).ToListAsync();

            // if(drivers is null) throw new Exception("There are no drivers with the selected vehicle type!!");

            return drivers;
        }
    }
}