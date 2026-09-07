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

        async Task<DriverProfile?> IAdminAuditLogService.updateStatus(string driverSub, int statusNum, string adminSub)
        {

            Dictionary<int, AdminActionType> pairs_a = new()
            {
                { 0, AdminActionType.SET_PENDING_VERIFICATION },
                { 1, AdminActionType.SET_DOCUMENT_REVIEW },
                { 2, AdminActionType.REJECTED },
                { 3, AdminActionType.SUSPENDED },
                { 4, AdminActionType.DEACTIVATED },
                { 5, AdminActionType.ACTIVATED },
                { 6, AdminActionType.SET_OFFLINE }
            };

            AdminActionAudit auditLog = new AdminActionAudit();

            auditLog.ActorId = adminSub;

            if(pairs_a.TryGetValue(statusNum, out AdminActionType status_a))
            {
                auditLog.Action = status_a;
            }
            else throw new Exception("Invalid driver status number");
            
            auditLog.TargetId = driverSub;

            _context.AdminActionAudits.Add(auditLog);

            await _context.SaveChangesAsync();


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

            var driver = await _context.DriverProfile.FirstOrDefaultAsync(v => v.DriverId == driverSub);
            if (driver is null) return null;


            if(pairs.TryGetValue(statusNum, out DriverStatus status))
            {
                driver.Status = status;
            }
            else throw new Exception("Invalid driver status number");

            await _context.SaveChangesAsync();
            return driver;
        }
    }
}