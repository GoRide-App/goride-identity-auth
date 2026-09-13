using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRC.Entities;
using SRC.Services.Interfaces;

namespace SRC.Controllers
{
    [Route("api/adminActivity")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminAuditLogController : ControllerBase
    {

        private readonly IAdminAuditLogService _adminAuditLog;

        public AdminAuditLogController(IAdminAuditLogService adminAuditLog)
        {
            _adminAuditLog = adminAuditLog;
        }

        [HttpPut("{driverSub}/{statusNum}")]
        public async Task<ActionResult<DriverProfile>> UpdateDriverStatus(string driverSub,int statusNum)
        {
            var usrSub = User.FindFirstValue("sub");
            if (usrSub is null) return Unauthorized();

            var profile = await _adminAuditLog.UpdateStatus(driverSub, statusNum, usrSub);
            return profile is null ? NotFound() : Ok(profile);
        }

        [HttpGet]
        public async Task<ActionResult<List<DriverProfile>>> GetDrivers()
        {
            var usrSub = User.FindFirstValue("sub");
            if (usrSub is null) return Unauthorized();

            var profiles = await _adminAuditLog.GetAllProfiles();
            return profiles is null ? NotFound() : Ok(profiles);
        } 

        [HttpGet("getLogs")]
        public async Task<ActionResult<List<AdminActionAudit>>> GetLogs()
        {
            var logs = await _adminAuditLog.GetAdminLogs();
            return logs is null ? NotFound() : Ok(logs);
        }
    }
}