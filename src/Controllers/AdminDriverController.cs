using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRC.Dtos;
using SRC.Enums;
using SRC.Services.Interfaces;

namespace SRC.Controllers
{
    [ApiController]
    [Route("api/admin/drivers")]
    [Authorize(Roles = "Admin")]
    public class AdminDriverController : ControllerBase
    {
        private readonly IDriverProfileService _driverProfileService;

        public AdminDriverController(IDriverProfileService driverProfileService)
        {
            _driverProfileService = driverProfileService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DriverAccountAdminDto>>> GetDriverAccounts([FromQuery] DriverStatus? status)
        {
            var accounts = await _driverProfileService.GetAllDriverAccountsAsync(status);
            return Ok(accounts);
        }

        [HttpGet("{driverId}")]
        public async Task<ActionResult<DriverAccountAdminDto>> GetDriverAccountById(string driverId)
        {
            var account = await _driverProfileService.GetDriverAccountByIdAsync(driverId);
            return account is null ? NotFound($"Driver profile for ID '{driverId}' was not found.") : Ok(account);
        }

        [HttpPut("{driverId}/status")]
        public async Task<ActionResult<DriverAccountAdminDto>> UpdateDriverStatus(string driverId, [FromBody] UpdateDriverStatusRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return BadRequest("A reason for the status change action must be recorded.");
            }

            var updatedDriver = await _driverProfileService.UpdateDriverStatusByAdminAsync(driverId, request.Status, request.Reason);
            if (updatedDriver is null)
            {
                return NotFound($"Driver profile for ID '{driverId}' was not found.");
            }

            var adminAccountDto = await _driverProfileService.GetDriverAccountByIdAsync(driverId);
            return Ok(adminAccountDto);
        }
    }
}
