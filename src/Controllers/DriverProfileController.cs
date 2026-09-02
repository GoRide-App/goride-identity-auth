using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRC.Dtos;
using SRC.Entities;
using SRC.Services.Interfaces;

namespace SRC.Controllers
{
    [ApiController]
    [Route("api/driver")]
    [Authorize(Roles = "Driver,Admin")]
    public class DriverProfileController : ControllerBase
    {
        private readonly IDriverProfileService _service;

        public DriverProfileController(IDriverProfileService service)
        {
            _service = service;
        }

        [HttpPost("addProfile")]
        [ProducesResponseType(typeof(DriverProfile), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<DriverProfile>> AddProfile([FromBody] CreateDriverProfileRequestDto request)
        {
            var sub = User.FindFirstValue("sub");
            if (sub is null) return Unauthorized();

            var newProfile = await _service.AddProfile(sub, request);
            if (newProfile is null)
                return Conflict(new { message = "A driver profile already exists for this account." });

            return Ok(newProfile);
        }

        [HttpGet("{sub}")]
        [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<VehicleDto>> GetVehicleById(string sub)
        {
            // A driver may only read their own profile; admins may read any.
            if (!User.IsInRole("Admin") && User.FindFirstValue("sub") != sub)
                return Forbid();

            var vehicle = await _service.GetVehicleById(sub);
            return vehicle is null ? NotFound() : Ok(vehicle);
        }

        [HttpPut("update/{driverSub}")]
        [ProducesResponseType(typeof(DriverProfile), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DriverProfile>> UpdateVehicle(string driverSub, [FromBody] UpdateVehicleDto request)
        {
            var usrSub = User.FindFirstValue("sub");
            if (usrSub is null) return Unauthorized();

            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && driverSub != usrSub)
                return Forbid();

            var updated = await _service.UpdateVehicle(driverSub, usrSub, request, isAdmin);
            return updated is null ? NotFound() : Ok(updated);
        }
    }
}
