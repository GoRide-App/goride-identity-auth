using SRC.Entities;

namespace SRC.Services.Interfaces;

public interface IAdminAuditLogService
{
    Task<List<DriverProfile>> getAllProfiles();
    Task<DriverProfile?> updateStatus(string driverSub, int statusNum);
}