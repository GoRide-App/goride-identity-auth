using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRC.Dtos;
using SRC.Services.Interfaces;

namespace SRC.Controllers
{
    [ApiController]
    [Route("api/account")]
    [Authorize]
    public class AccountController : ControllerBase
    {
        private readonly IAccountDeactivationService _deactivation;

        public AccountController(IAccountDeactivationService deactivation) => _deactivation = deactivation;

        /// <summary>
        /// Deactivates the caller's own account (SCRUM-35). Disables the account in the WSO2
        /// Identity Server through SCIM2, marks the local profile row as deactivated (soft delete)
        /// and ends the current session.
        /// </summary>
        /// <response code="200">Account disabled; the session cookie has been cleared.</response>
        /// <response code="400">The request did not carry <c>confirm: true</c>.</response>
        /// <response code="401">No authenticated session.</response>
        /// <response code="409">A trip is still in progress, so deactivation is refused.</response>
        /// <response code="502">The Identity Server refused the disable operation; nothing was changed.</response>
        /// <response code="503">The trip service could not confirm the caller has no active trip.</response>
        [HttpPost("deactivate")]
        [ProducesResponseType(typeof(DeactivateAccountResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Deactivate([FromBody] DeactivateAccountRequestDto request, CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue("sub");
            if (userId is null) return Unauthorized();

            if (!request.Confirm)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Confirmation required",
                    detail: "Send { \"confirm\": true } to deactivate your account.");
            }

            var result = await _deactivation.DeactivateAsync(userId, cancellationToken);

            switch (result.Outcome)
            {
                case DeactivationOutcome.BlockedByActiveTrip:
                    return Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Active trip blocks deactivation",
                        detail: result.Detail);

                case DeactivationOutcome.TripStatusUnavailable:
                    return Problem(
                        statusCode: StatusCodes.Status503ServiceUnavailable,
                        title: "Trip status unavailable",
                        detail: result.Detail);

                case DeactivationOutcome.IdentityServerRejected:
                    return Problem(
                        statusCode: StatusCodes.Status502BadGateway,
                        title: "Identity provider error",
                        detail: result.Detail);
            }

            // Deactivated or AlreadyDeactivated: the account must not keep a live session.
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return Ok(new DeactivateAccountResponse(
                Status: "deactivated",
                DeactivatedAt: result.DeactivatedAt,
                AlreadyDeactivated: result.Outcome == DeactivationOutcome.AlreadyDeactivated));
        }
    }

    public record DeactivateAccountResponse(string Status, DateTime? DeactivatedAt, bool AlreadyDeactivated);
}
