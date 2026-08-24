// using System.Security.Claims;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using SRC.Services.Interfaces;

// namespace SRC.Controllers
// {
//     [ApiController]
//     [Route("api/onboarding")]
//     public class RegistrationController: ControllerBase
//     {
//         private readonly IAsgardeoRoleService _roleService;
//         private readonly IConfiguration _config;

//         public RegistrationController(IAsgardeoRoleService roleService, IConfiguration config)
//         {
//             _roleService = roleService;
//             _config = config;
//         }

//         [HttpPost("select-role")]
//         [Authorize]
//         public async Task<IActionResult> SelectRole([FromBody] RoleSelectionRequest req)
//         {
//             var asgardeoUserId = User.FindFirstValue("sub");
//             if (asgardeoUserId is null)
//                 return Unauthorized();

//             var displayName = User.FindFirstValue("name") ?? User.FindFirstValue("email") ?? asgardeoUserId;
            
//             // var email = User.Claims.FirstOrDefault(c => 
//             //     c.Type.Contains("email", StringComparison.OrdinalIgnoreCase)
//             // )?.Value;


//             var roleId = req.Role switch
//             {
//                 "Driver" => _config["AsgardeoRoles:DriverRoleId"],
//                 "Rider" => _config["AsgardeoRoles:RiderRoleId"],
//                 _ => null
//             };
//             if (roleId is null)
//                 return BadRequest("Invalid role");

//             // if(email == null) return BadRequest("Email Not found in RegistrationController!!!");
//             await _roleService.AssignRoleAsync(asgardeoUserId, displayName, roleId);
//             return Ok();
//         }
//     }

//     public record RoleSelectionRequest(string Role);
// }






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

            var displayName = User.FindFirstValue("name") ?? User.FindFirstValue("email") ?? asgardeoUserId;

            var email = User.FindFirstValue("email");

            var roleId = req.Role switch
            {
                "Driver" => _config["AsgardeoRoles:DriverRoleId"],
                "Rider" => _config["AsgardeoRoles:RiderRoleId"],
                _ => null
            };
            if (roleId is null)
                return BadRequest("Invalid role");

            if(email == null) return BadRequest("Email Not found in RegistrationController!!!");
            await _roleService.AssignRoleAsync(asgardeoUserId, email, roleId);
            return Ok();
        }
    }

    public record RoleSelectionRequest(string Role);
}
