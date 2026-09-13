using SRC.Entities;

namespace SRC.Services.Interfaces;

public interface IAdminAuditLogService
{
    Task<List<AdminActionAudit>> GetAdminLogs();
    Task<List<DriverProfile>> GetAllProfiles();
    Task<DriverProfile?> UpdateStatus(string driverSub, int statusNum, string usrSub);
}