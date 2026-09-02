using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRC.Services.Interfaces;

namespace SRC.Controllers
{
    [ApiController]
    [Route("api/onboarding")]
    public class RegistrationController : ControllerBase
    {
        private readonly IAsgardeoRoleService _roleService;
        private readonly IConfiguration _config;

        public RegistrationController(IAsgardeoRoleService roleService, IConfiguration config)
        {
            _roleService = roleService;
            _config = config;
        }

        [HttpPost("select-role")]
        [Authorize]
        public async Task<IActionResult> SelectRole([FromBody] RoleSelectionRequest req)
        {
            var asgardeoUserId = User.FindFirstValue("sub");
            if (asgardeoUserId is null)
                return Unauthorized();

            var username = User.FindFirstValue("username") ?? asgardeoUserId;

            var roleId = req.Role switch
            {
                "Driver" => _config["AsgardeoRoles:DriverRoleId"],
                "Rider" => _config["AsgardeoRoles:RiderRoleId"],
                _ => null
            };
            if (string.IsNullOrWhiteSpace(roleId))
                return BadRequest("Invalid role");

            if (User.FindFirstValue("email") is null)
                return BadRequest("The session has no email claim; sign in again.");

            await _roleService.AssignRoleAsync(asgardeoUserId, username, roleId);
            return Ok();
        }

        [HttpGet("debug-claims")]
        [Authorize]
        public IActionResult DebugClaims()
        {
            var claims = User.Claims.Select(c => new { c.Type, c.Value });
            return Ok(claims);
        }
    }




    public record RoleSelectionRequest(string Role);
}
