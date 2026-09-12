using System.Security.Claims;
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

        public DriverController(IDriverService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<List<DriverProfile>>> GetDriversByVehicle([FromBody] DriverProfileRequestDto requestDto)
        {
            var sub = User.FindFirstValue("sub");
            if (sub is null) return Unauthorized();

            var drivers = await _service.GetDrivers(requestDto);
            if(drivers.Count == 0) return NotFound("No Drivers with vehicle-type: " + requestDto.VehicleType + "found!!");
            return Ok(drivers);
        }
    }
}