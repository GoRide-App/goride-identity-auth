using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRC.Services.Interfaces;

namespace SRC.Controllers
{
    [ApiController]
    [Route("api/profile")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profileService;
        public ProfileController(IProfileService profileService) => _profileService = profileService;

        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            if (accessToken is null) return Unauthorized();

            var profileJson = await _profileService.GetProfileAsync(accessToken);
            return Content(profileJson, "application/json");
        }

        [HttpPatch]
        public async Task<IActionResult> UpdateProfile([FromBody] ProfileUpdateRequest req)
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            if (accessToken is null) return Unauthorized();

            await _profileService.UpdateProfileAsync(accessToken, req);
            return Ok(new
            {
                phoneNumber = req.PhoneNumber
            });
        }
    }
}