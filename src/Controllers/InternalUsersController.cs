using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRC.Services.Interfaces;

namespace SRC.Controllers
{
    /// <summary>Service-to-service lookup, authenticated with a shared API key rather than a user session.</summary>
    [ApiController]
    [Route("api/internal-users")]
    [AllowAnonymous]
    public class InternalUserController(IUserDirectoryService userDirectoryService, IConfiguration config) : ControllerBase
    {
        private readonly IUserDirectoryService _userDirectoryService = userDirectoryService;
        private readonly IConfiguration _config = config;

        [HttpGet("{sub}")]
        public async Task<IActionResult> GetUserBySub(string sub, [FromHeader(Name = "X-Internal-Api-Key")] string? apiKey)
        {
            var expectedKey = _config["InternalServices:ApiKey"];
            if (string.IsNullOrEmpty(expectedKey) || apiKey is null || !KeysMatch(apiKey, expectedKey))
                return Unauthorized();

            var userJson = await _userDirectoryService.GetUserByIdAsync(sub);
            return Content(userJson, "application/json");
        }

        // Constant-time comparison so the key cannot be guessed byte by byte from response timing.
        private static bool KeysMatch(string provided, string expected) =>
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));
    }
}
