using Microsoft.AspNetCore.Mvc;
using SRC.Services.Impl;

namespace SRC.Controllers
{
    [ApiController]
    [Route("api/internal-users")]
    public class InternalUserController(IUserDirectoryService userDirectoryService, IConfiguration config) : ControllerBase
    {
        private readonly IUserDirectoryService _userDirectoryService = userDirectoryService;
        private readonly IConfiguration _config = config;

        [HttpGet("{sub}")]
        public async Task<IActionResult> GetUserBySub(string sub, [FromHeader(Name = "X-Internal-Api-Key")] string? apiKey)
        {
            var expectedKey = _config["InternalServices:ApiKey"];
            if (string.IsNullOrEmpty(expectedKey) || apiKey != expectedKey)
                return Unauthorized();

            var userJson = await _userDirectoryService.GetUserByIdAsync(sub);
            return Content(userJson, "application/json");
        }
    }
}