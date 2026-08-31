using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SRC.Dtos;
using SRC.Entities;
using SRC.Services.Interfaces;

namespace SRC.Controllers
{
    [ApiController]
    [Route("api/driver")]
    [Authorize(Roles = "Driver,Admin")]
    public class DriverProfileController: ControllerBase
    {
        private readonly IDriverProfileService _service;

        public DriverProfileController(IDriverProfileService service)
        {
            _service = service;
        }

        [HttpPost("addProfile")]
        public async Task<ActionResult<DriverProfile>> AddProfile([FromBody] CreateDriverProfileRequestDto request)
        {
            var sub = User.FindFirstValue("sub");
            if(sub is null) return Unauthorized();


            var newProfile = await _service.AddProfile(sub, request);
            if(newProfile is null) return BadRequest();
            return Ok(newProfile);
        }

        [HttpGet("{sub}")]
        public async Task<ActionResult<VehicleDto>> GetVehicleById(string sub)
        {
            var vehicle = await _service.GetVehicleById(sub);
            return vehicle is null ? NotFound() : Ok(vehicle);
        }

        [HttpPut("update/{driverSub}")]
        public async Task<ActionResult<DriverProfile>> UpdateVehicle(string driverSub, [FromBody] UpdateVehicleDto request)
        {
            var usrSub = User.FindFirstValue("sub");
            if (usrSub is null) return Unauthorized();

            var isAdmin = User.IsInRole("Admin");

            var updated = await _service.UpdateVehicle(driverSub, usrSub, request, isAdmin);
            return updated is null ? NotFound() : Ok(updated);
        }

        [HttpGet("verification-status")]
        public async Task<ActionResult<DriverStateEnforcementResponseDto>> GetVerificationStatus()
        {
            var driverSub = User.FindFirstValue("sub");
            if (driverSub is null) return Unauthorized();

            var statusDto = await _service.CheckDriverStateEnforcementAsync(driverSub);
            return Ok(statusDto);
        }

        [HttpPost("go-online")]
        public async Task<ActionResult<DriverStateEnforcementResponseDto>> GoOnline()
        {
            var driverSub = User.FindFirstValue("sub");
            if (driverSub is null) return Unauthorized();

            var response = await _service.GoOnlineAsync(driverSub);
            if (!response.CanAcceptTrips)
            {
                return StatusCode(StatusCodes.Status403Forbidden, response);
            }
            return Ok(response);
        }

        [HttpPost("go-offline")]
        public async Task<ActionResult<DriverStateEnforcementResponseDto>> GoOffline()
        {
            var driverSub = User.FindFirstValue("sub");
            if (driverSub is null) return Unauthorized();

            var response = await _service.GoOfflineAsync(driverSub);
            return Ok(response);
        }
    }
}