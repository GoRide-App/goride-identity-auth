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
    }
}