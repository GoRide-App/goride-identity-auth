using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRC.Services.Interfaces;

namespace SRC.Controllers
{
    [ApiController]
    [Route("api/onboarding")]
    public class RegistrationController: ControllerBase
    {
        private readonly IAsgardeoRoleService _roleService;
        private readonly IConfiguration _config;
        private readonly ClaimsPrincipal _user;

        public RegistrationController(IAsgardeoRoleService roleService, IConfiguration config, ClaimsPrincipal user)
        {
            _roleService = roleService;
            _config = config;
            _user = user;
        }

        [HttpPost("select-role")]
        [Authorize]
        public async Task<IActionResult> SelectRole([FromBody] RoleSelectionRequest req)
        {
            var asgardeoUserId = _user.FindFirstValue("sub");
            if (asgardeoUserId is null)
                return Unauthorized();

            var roleId = req.Role switch
            {
                "Driver" => _config["AsgardeoRoles:DriverRoleId"],
                "Rider" => _config["AsgardeoRoles:RiderRoleId"],
                _ => null
            };
            if (roleId is null)
                return BadRequest("Invalid role");

            await _roleService.AssignRoleAsync(asgardeoUserId, roleId);
            return Ok();
        }
    }

    public record RoleSelectionRequest(string Role);
}
