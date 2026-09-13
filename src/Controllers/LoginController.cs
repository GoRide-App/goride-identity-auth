using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SRC.Controllers
{
    [ApiController]
    public class LoginController: ControllerBase
    {
        [HttpGet("/api/me")]
        public ActionResult GetScope()
        {
            

            if (!User.Identity!.IsAuthenticated) return Unauthorized();
            return Ok(new
            {
                userId = User.FindFirstValue("sub"),
                name = User.FindFirstValue("username"),
                email = User.FindFirstValue("email"),
                phone_number = User.FindFirstValue("phone_number"),
                roles = User.FindAll("roles").Select(c => c.Value),
            });
        }

        [HttpGet("/login")]
        [AllowAnonymous]
        public ActionResult Login(string? returnUrl, string? prompt)
        {
            var properties = new AuthenticationProperties { RedirectUri = returnUrl ?? "https://goride-my-client.vercel.app" };
            if (prompt == "login")
            {
                properties.Items["forceFresh"] = "true";
            }
            return Challenge(properties, [OpenIdConnectDefaults.AuthenticationScheme]);
        }
    }
}