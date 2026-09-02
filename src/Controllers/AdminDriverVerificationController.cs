using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRC.Dtos;
using SRC.Services.Interfaces;

namespace SRC.Controllers
{
    /// <summary>Admin approve/reject of driver registrations (SCRUM-43).</summary>
    [ApiController]
    [Route("api/admin/drivers")]
    [Authorize(Roles = "Admin")]
    public class AdminDriverVerificationController : ControllerBase
    {
        private readonly IDriverVerificationService _verification;

        public AdminDriverVerificationController(IDriverVerificationService verification) => _verification = verification;

        /// <summary>
        /// Approves a driver registration. Requires the driving licence, vehicle registration and
        /// insurance documents to be uploaded. The driver becomes Active immediately and the
        /// decision (with the optional note) is recorded.
        /// </summary>
        /// <response code="200">Approved; the body is the driver's updated profile.</response>
        /// <response code="404">No driver profile with that id.</response>
        /// <response code="409">Already approved, wrong state, or required documents missing.</response>
        [HttpPost("{driverId}/approve")]
        [ProducesResponseType(typeof(DriverProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<DriverProfileDto>> Approve(string driverId, [FromBody] ApproveDriverRequestDto request, CancellationToken cancellationToken)
        {
            var adminId = User.FindFirstValue("sub");
            if (adminId is null) return Unauthorized();

            var result = await _verification.ApproveAsync(driverId, adminId, request.Reason, cancellationToken);
            return ToResponse(result, "Approval not possible");
        }

        /// <summary>
        /// Rejects a driver registration with a mandatory reason. The driver loses verification
        /// immediately (cannot go online) and sees the reason on their profile.
        /// </summary>
        /// <response code="200">Rejected; the body is the driver's updated profile.</response>
        /// <response code="400">Reason missing or too short.</response>
        /// <response code="404">No driver profile with that id.</response>
        /// <response code="409">Already rejected, or the account is deactivated.</response>
        [HttpPost("{driverId}/reject")]
        [ProducesResponseType(typeof(DriverProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<DriverProfileDto>> Reject(string driverId, [FromBody] RejectDriverRequestDto request, CancellationToken cancellationToken)
        {
            var adminId = User.FindFirstValue("sub");
            if (adminId is null) return Unauthorized();

            var result = await _verification.RejectAsync(driverId, adminId, request.Reason, cancellationToken);
            return ToResponse(result, "Rejection not possible");
        }

        private ActionResult<DriverProfileDto> ToResponse(DriverDecisionResult result, string conflictTitle) => result.Outcome switch
        {
            DriverDecisionOutcome.Applied => Ok(result.Profile),
            DriverDecisionOutcome.NotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status409Conflict, title: conflictTitle, detail: result.Error)
        };
    }
}
