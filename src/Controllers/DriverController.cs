using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRC.Dtos;
using SRC.Entities;
using SRC.Services.Interfaces;

namespace SRC.Controllers
{
    [ApiController]
    [Route("api/internal-drivers")]
    public class DriverController : ControllerBase
    {
        private readonly IDriverService _service;
        private readonly IConfiguration _config;

        public DriverController(IDriverService service, IConfiguration config)
        {
            _service = service;
            _config = config;
        }

        /// <summary>
        /// Driver profiles, optionally narrowed to one vehicle type (?vehicleType=TUK).
        /// Called by other services (trip-matching) with the shared X-Internal-Api-Key, since
        /// they have no signed-in user; a signed-in user is still accepted as before.
        /// "No drivers" is a normal answer, so it returns 200 with an empty list.
        /// </summary>
        // Program.cs requires a signed-in user for every endpoint that isn't marked anonymous, which
        // would reject other services (they only have the API key) before this method runs. So the
        // framework check is lifted here and the method enforces "valid key OR signed-in user" itself.
        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<List<DriverProfile>>> GetDriversByVehicle(
            [FromQuery] DriverProfileRequestDto requestDto,
            [FromHeader(Name = "X-Internal-Api-Key")] string? apiKey)
        {
            var isInternalService = HasValidInternalKey(apiKey);
            if (!isInternalService && User.FindFirstValue("sub") is null) return Unauthorized();

            return Ok(await _service.GetDrivers(requestDto));
        }

        // An unset InternalServices:ApiKey must never match an empty header, so both must be non-empty.
        private bool HasValidInternalKey(string? presented)
        {
            var expected = _config["InternalServices:ApiKey"];
            if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(presented)) return false;

            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(presented));
        }
    }
}
