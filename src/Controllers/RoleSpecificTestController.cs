using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SRC.Controllers
{
    [ApiController]
    [Route("api/role-specific")]
    public class RoleSpecificTestController : ControllerBase
    {
        [HttpGet("driver")]
        [Authorize(Roles = "Driver")]
        public IActionResult DriverOnlyAction()
        {
            return Ok();
        }
    }
}