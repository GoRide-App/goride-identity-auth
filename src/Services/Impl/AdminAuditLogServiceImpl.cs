using Microsoft.EntityFrameworkCore;
using SRC.Data;
using SRC.Entities;
using SRC.Enums;
using SRC.Services.Interfaces;

namespace SRC.Services.Impl
{
    public class AdminAuditLogServiceImpl : IAdminAuditLogService
    {
        private readonly AppDbContext _context;

        public AdminAuditLogServiceImpl(AppDbContext context)
        {
            _context = context;
        }

        async Task<List<DriverProfile>> IAdminAuditLogService.getAllProfiles()
        {
            return await _context.DriverProfile.ToListAsync();
        }

        async Task<DriverProfile?> IAdminAuditLogService.updateStatus(string driverSub, int statusNum)
        {
            var driver = await _context.DriverProfile.FirstOrDefaultAsync(v => v.DriverId == driverSub);
            if (driver is null) return null;

            Dictionary<int, DriverStatus> pairs = new()
            {
                { 0, DriverStatus.PendingVerification },
                { 1, DriverStatus.DocumentReview },
                { 2, DriverStatus.Rejected },
                { 3, DriverStatus.Suspended },
                { 4, DriverStatus.Deactivated },
                { 5, DriverStatus.Active },
                { 6, DriverStatus.Offline }
            };
            
            if(statusNum < 0 || statusNum > 6) throw new Exception("Invalid driver status number");

            if(pairs.TryGetValue(statusNum, out DriverStatus status))
            {
                driver.Status = status;
            }

            await _context.SaveChangesAsync();
            return driver;
        }
    }
}