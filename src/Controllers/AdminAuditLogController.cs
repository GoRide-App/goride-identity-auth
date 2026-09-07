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
        public async Task<ActionResult<DriverProfile>> updateDriverStatus(string driverSub,int statusNum)
        {
            var usrSub = User.FindFirstValue("sub");
            if (usrSub is null) return Unauthorized();

            var profile = await _adminAuditLog.updateStatus(driverSub, statusNum, usrSub);
            return profile is null ? NotFound() : Ok(profile);
        }

        [HttpGet]
        public async Task<ActionResult<List<DriverProfile>>> getDrivers()
        {
            var usrSub = User.FindFirstValue("sub");
            if (usrSub is null) return Unauthorized();

            var profiles = await _adminAuditLog.getAllProfiles();
            return profiles is null ? NotFound() : Ok(profiles);
        } 
    }
}