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

        private readonly IDriverProfileService _driverProfService;

        public AdminAuditLogController(IDriverProfileService driverProfService)
        {
            _driverProfService = driverProfService;
        }

        [HttpPut("{driverSub}/{statusNum}")]
        public async Task<ActionResult<DriverProfile>> updateDriverStatus(string driverSub,int statusNum)
        {
            var usrSub = User.FindFirstValue("sub");
            if (usrSub is null) return Unauthorized();

            var profile = await _driverProfService.updateStatus(driverSub, statusNum);
            return profile is null ? NotFound() : Ok(profile);
        }
    }
}