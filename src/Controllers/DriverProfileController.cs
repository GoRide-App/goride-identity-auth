using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRC.Dtos;
using SRC.Entities;
using SRC.Enums;
using SRC.Services.Interfaces;

namespace SRC.Controllers
{
    [ApiController]
    [Route("api/driver")]
    [Authorize(Roles = "Driver,Admin")]
    public class DriverProfileController : ControllerBase
    {
        private readonly IDriverProfileService _service;
        private readonly IDriverVerificationService _verification;

        public DriverProfileController(IDriverProfileService service, IDriverVerificationService verification)
        {
            _service = service;
            _verification = verification;
        }

        /// <summary>Creates the caller's driver profile (vehicle and licence details).</summary>
        /// <response code="200">Profile created in PendingVerification.</response>
        /// <response code="400">Validation failed; the body lists the offending fields.</response>
        /// <response code="409">A profile already exists for this account.</response>
        [HttpPost("addProfile")]
        [ProducesResponseType(typeof(DriverProfile), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<DriverProfile>> AddProfile([FromBody] CreateDriverProfileRequestDto request)
        {
            var sub = User.FindFirstValue("sub");
            if (sub is null) return Unauthorized();

            var newProfile = await _service.AddProfile(sub, request);
            if (newProfile is null)
                return Conflict(new { message = "A driver profile already exists for this account." });

            return Ok(newProfile);
        }

        /// <summary>
        /// Driver profile with verification state and uploaded documents. A driver may only read
        /// their own; admins may read any.
        /// </summary>
        [HttpGet("{sub}")]
        [ProducesResponseType(typeof(DriverProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DriverProfileDto>> GetProfile(string sub, CancellationToken cancellationToken)
        {
            if (!User.IsInRole("Admin") && User.FindFirstValue("sub") != sub)
                return Forbid();

            var profile = await _verification.GetProfileAsync(sub, cancellationToken);
            return profile is null ? NotFound() : Ok(profile);
        }

        /// <summary>Updates vehicle and licence details. Verification status cannot be changed here.</summary>
        [HttpPut("update/{driverSub}")]
        [ProducesResponseType(typeof(DriverProfile), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DriverProfile>> UpdateVehicle(string driverSub, [FromBody] UpdateVehicleDto request)
        {
            var usrSub = User.FindFirstValue("sub");
            if (usrSub is null) return Unauthorized();

            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && driverSub != usrSub)
                return Forbid();

            var updated = await _service.UpdateVehicle(driverSub, usrSub, request, isAdmin);
            return updated is null ? NotFound() : Ok(updated);
        }

        /// <summary>
        /// Uploads (or replaces) one verification document for the caller (SCRUM-42). JPEG, PNG or
        /// PDF up to 5 MB. Once the driving licence, vehicle registration and insurance are all
        /// present, a pending or rejected driver moves to DocumentReview.
        /// </summary>
        /// <response code="200">Stored; the body describes the document as it now appears on the profile.</response>
        /// <response code="400">Invalid data (missing file, wrong type, too large, past expiry, bad document type). Nothing was stored.</response>
        /// <response code="409">The caller has no driver profile yet.</response>
        [HttpPost("documents")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(DriverDocumentValidatorLimits.RequestBytes)]
        [ProducesResponseType(typeof(DriverDocumentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<DriverDocumentDto>> UploadDocument([FromForm] UploadDriverDocumentRequestDto request, CancellationToken cancellationToken)
        {
            var sub = User.FindFirstValue("sub");
            if (sub is null) return Unauthorized();

            var file = request.File!;
            if (file.Length > Services.Impl.DriverDocumentValidator.MaxBytes)
            {
                ModelState.AddModelError(nameof(request.File), "The file must be 5 MB or smaller.");
                return ValidationProblem(ModelState);
            }

            byte[] content;
            using (var buffer = new MemoryStream((int)file.Length))
            {
                await file.CopyToAsync(buffer, cancellationToken);
                content = buffer.ToArray();
            }

            var upload = new DriverDocumentUpload(
                request.Type!.Value, file.FileName, file.ContentType, content, request.DocumentNumber, request.ExpiresOn);

            var result = await _verification.UploadDocumentAsync(sub, upload, cancellationToken);

            switch (result.Outcome)
            {
                case DocumentUploadOutcome.InvalidFile:
                    ModelState.AddModelError(nameof(request.File), result.Error!);
                    return ValidationProblem(ModelState);

                case DocumentUploadOutcome.NoDriverProfile:
                    return Problem(statusCode: StatusCodes.Status409Conflict, title: "Driver profile required", detail: result.Error);
            }

            return Ok(result.Document);
        }

        /// <summary>Downloads a stored document. Drivers may only fetch their own; admins may fetch any.</summary>
        [HttpGet("{sub}/documents/{type}")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DownloadDocument(string sub, DriverDocumentType type, CancellationToken cancellationToken)
        {
            if (!User.IsInRole("Admin") && User.FindFirstValue("sub") != sub)
                return Forbid();

            var document = await _verification.GetDocumentAsync(sub, type, cancellationToken);
            if (document is null) return NotFound();

            return File(document.Content, document.ContentType, document.FileName);
        }

        /// <summary>
        /// The caller's verification state as the app must enforce it (SCRUM-43): status, the
        /// reason recorded with the latest change, and whether trips can be accepted.
        /// </summary>
        [HttpGet("verification-status")]
        [ProducesResponseType(typeof(DriverStateEnforcementResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DriverStateEnforcementResponseDto>> GetVerificationStatus(CancellationToken cancellationToken)
        {
            var sub = User.FindFirstValue("sub");
            if (sub is null) return Unauthorized();

            var state = await _verification.GetEnforcementStateAsync(sub, cancellationToken);
            return state is null ? NotFound() : Ok(state);
        }

        /// <summary>
        /// Go online to accept trips. Only an approved driver (Active or Offline) may; every other
        /// state is refused with 403 and the body explains why, e.g. the rejection reason.
        /// </summary>
        [HttpPost("go-online")]
        [ProducesResponseType(typeof(DriverStateEnforcementResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(DriverStateEnforcementResponseDto), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DriverStateEnforcementResponseDto>> GoOnline(CancellationToken cancellationToken)
        {
            var sub = User.FindFirstValue("sub");
            if (sub is null) return Unauthorized();

            var result = await _verification.GoOnlineAsync(sub, cancellationToken);
            if (result is null) return NotFound();

            return result.Allowed ? Ok(result.State) : StatusCode(StatusCodes.Status403Forbidden, result.State);
        }

        /// <summary>Go offline. Always succeeds for an existing driver profile.</summary>
        [HttpPost("go-offline")]
        [ProducesResponseType(typeof(DriverStateEnforcementResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DriverStateEnforcementResponseDto>> GoOffline(CancellationToken cancellationToken)
        {
            var sub = User.FindFirstValue("sub");
            if (sub is null) return Unauthorized();

            var result = await _verification.GoOfflineAsync(sub, cancellationToken);
            return result is null ? NotFound() : Ok(result.State);
        }
    }

    internal static class DriverDocumentValidatorLimits
    {
        // Slightly above the 5 MB file limit to leave room for multipart boundaries and form fields.
        public const long RequestBytes = 6L * 1024 * 1024;
    }
}
