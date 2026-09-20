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
            var query = _context.DriverProfile.AsQueryable();

            // No vehicle type means "drivers of every type".
            if (!string.IsNullOrWhiteSpace(requestDto.VehicleType))
                query = query.Where(d => d.VehicleTypeCode == requestDto.VehicleType);

            return await query.ToListAsync();
        }
    }
}