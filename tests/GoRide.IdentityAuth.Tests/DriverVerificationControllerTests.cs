using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SRC.Controllers;
using SRC.Dtos;
using SRC.Enums;
using SRC.Services.Interfaces;

namespace GoRide.IdentityAuth.Tests;

public class DriverVerificationControllerTests
{
    private const string DriverId = "driver-1";
    private const string AdminId = "admin-9";

    private sealed class StubVerificationService : IDriverVerificationService
    {
        public DocumentUploadResult UploadResult { get; set; } = DocumentUploadResult.Stored(new DriverDocumentDto { Id = 1, Type = DriverDocumentType.DrivingLicence, FileName = "x.jpg", ContentType = "image/jpeg" });
        public DriverDocumentUpload? LastUpload { get; private set; }
        public DriverDecisionResult DecisionResult { get; set; } = DriverDecisionResult.Applied(new DriverProfileDto { DriverId = DriverId, Status = DriverStatus.Active });
        public (string DriverId, string AdminId, string? Reason)? LastDecision { get; private set; }
        public DriverOnlineResult? OnlineResult { get; set; }

        public Task<DriverProfileDto?> GetProfileAsync(string driverId, CancellationToken ct = default) => Task.FromResult<DriverProfileDto?>(null);
        public Task<DocumentUploadResult> UploadDocumentAsync(string driverId, DriverDocumentUpload upload, CancellationToken ct = default)
        {
            LastUpload = upload;
            return Task.FromResult(UploadResult);
        }
        public Task<DriverDocumentFile?> GetDocumentAsync(string driverId, DriverDocumentType type, CancellationToken ct = default) => Task.FromResult<DriverDocumentFile?>(null);
        public Task<DriverDecisionResult> ApproveAsync(string driverId, string adminId, string? reason, CancellationToken ct = default)
        {
            LastDecision = (driverId, adminId, reason);
            return Task.FromResult(DecisionResult);
        }
        public Task<DriverDecisionResult> RejectAsync(string driverId, string adminId, string reason, CancellationToken ct = default)
        {
            LastDecision = (driverId, adminId, reason);
            return Task.FromResult(DecisionResult);
        }
        public Task<DriverStateEnforcementResponseDto?> GetEnforcementStateAsync(string driverId, CancellationToken ct = default) => Task.FromResult(OnlineResult?.State);
        public Task<DriverOnlineResult?> GoOnlineAsync(string driverId, CancellationToken ct = default) => Task.FromResult(OnlineResult);
        public Task<DriverOnlineResult?> GoOfflineAsync(string driverId, CancellationToken ct = default) => Task.FromResult(OnlineResult);
    }

    private sealed class NoopDriverProfileService : IDriverProfileService
    {
        public Task<SRC.Entities.DriverProfile?> AddProfile(string sub, CreateDriverProfileRequestDto request) => throw new NotSupportedException();
        public Task<SRC.Entities.DriverProfile?> UpdateVehicle(string driverSub, string usrSub, UpdateVehicleDto request, bool isAdmin) => throw new NotSupportedException();
    }

    private static HttpContext Context(string sub, params string[] roles)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        var claims = new List<Claim> { new("sub", sub) };
        claims.AddRange(roles.Select(r => new Claim("roles", r)));
        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "cookie", "name", "roles"))
        };
    }

    private static DriverProfileController DriverController(StubVerificationService service, string sub = DriverId, params string[] roles) =>
        new(new NoopDriverProfileService(), service) { ControllerContext = new ControllerContext { HttpContext = Context(sub, roles.Length == 0 ? ["Driver"] : roles) } };

    private static AdminDriverVerificationController AdminController(StubVerificationService service) =>
        new(service) { ControllerContext = new ControllerContext { HttpContext = Context(AdminId, "Admin") } };

    private static FormFile Form(byte[] bytes, string name = "licence.jpg") =>
        new(new MemoryStream(bytes), 0, bytes.Length, "file", name) { Headers = new HeaderDictionary(), ContentType = "image/jpeg" };

    // ---------------------------------------------------------------- SCRUM-42 upload mapping

    [Fact]
    public async Task Upload_Valid_Returns200_WithTheStoredDocument_AndPassesFormFieldsThrough()
    {
        var service = new StubVerificationService();
        var controller = DriverController(service);
        var bytes = DriverDocumentValidatorTests.Jpeg();

        var result = await controller.UploadDocument(new UploadDriverDocumentRequestDto
        {
            Type = DriverDocumentType.DrivingLicence,
            File = Form(bytes),
            DocumentNumber = "B1234567",
            ExpiresOn = new DateOnly(2031, 1, 1)
        }, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<DriverDocumentDto>(ok.Value);
        Assert.Equal(DriverDocumentType.DrivingLicence, service.LastUpload!.Type);
        Assert.Equal("licence.jpg", service.LastUpload.FileName);
        Assert.Equal(bytes, service.LastUpload.Content);
        Assert.Equal("B1234567", service.LastUpload.DocumentNumber);
        Assert.Equal(new DateOnly(2031, 1, 1), service.LastUpload.ExpiresOn);
    }

    [Fact]
    public async Task Upload_InvalidFile_Returns400_WithFieldError()
    {
        var service = new StubVerificationService { UploadResult = DocumentUploadResult.InvalidFile("Only JPEG, PNG or PDF files are accepted.") };
        var controller = DriverController(service);

        var result = await controller.UploadDocument(new UploadDriverDocumentRequestDto { Type = DriverDocumentType.VehicleInsurance, File = Form([1, 2, 3]) }, default);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problem = Assert.IsType<ValidationProblemDetails>(bad.Value);
        Assert.Contains("Only JPEG", problem.Errors["File"][0]);
    }

    [Fact]
    public async Task Upload_OverSizeLimit_Returns400_WithoutReadingOrCallingTheService()
    {
        var service = new StubVerificationService();
        var controller = DriverController(service);
        var huge = new FormFile(Stream.Null, 0, SRC.Services.Impl.DriverDocumentValidator.MaxBytes + 1, "file", "huge.pdf");

        var result = await controller.UploadDocument(new UploadDriverDocumentRequestDto { Type = DriverDocumentType.VehicleRegistration, File = huge }, default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Null(service.LastUpload);
    }

    [Fact]
    public async Task Upload_WithoutDriverProfile_Returns409()
    {
        var service = new StubVerificationService { UploadResult = DocumentUploadResult.NoDriverProfile() };
        var controller = DriverController(service);

        var result = await controller.UploadDocument(new UploadDriverDocumentRequestDto { Type = DriverDocumentType.DrivingLicence, File = Form(DriverDocumentValidatorTests.Jpeg()) }, default);

        var conflict = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Download_OtherDriversDocument_IsForbidden_ButAdminMay()
    {
        var service = new StubVerificationService();

        var asDriver = await DriverController(service, "driver-2").DownloadDocument(DriverId, DriverDocumentType.DrivingLicence, default);
        Assert.IsType<ForbidResult>(asDriver);

        var asAdmin = await DriverController(service, AdminId, "Admin").DownloadDocument(DriverId, DriverDocumentType.DrivingLicence, default);
        Assert.IsType<NotFoundResult>(asAdmin); // stub has no file, but access was allowed
    }

    // ---------------------------------------------------------------- SCRUM-43 admin mapping

    [Fact]
    public async Task Approve_PassesAdminIdAndReason_AndReturnsProfile()
    {
        var service = new StubVerificationService();
        var controller = AdminController(service);

        var result = await controller.Approve(DriverId, new ApproveDriverRequestDto { Reason = "ok" }, default);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal((DriverId, AdminId, "ok"), service.LastDecision);
    }

    [Fact]
    public async Task Reject_MapsOutcomes()
    {
        var notFound = new StubVerificationService { DecisionResult = DriverDecisionResult.NotFound() };
        Assert.IsType<NotFoundResult>((await AdminController(notFound).Reject(DriverId, new RejectDriverRequestDto { Reason = "bad" }, default)).Result);

        var conflict = new StubVerificationService { DecisionResult = DriverDecisionResult.InvalidTransition("already rejected") };
        var conflictResult = Assert.IsType<ObjectResult>((await AdminController(conflict).Reject(DriverId, new RejectDriverRequestDto { Reason = "bad" }, default)).Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflictResult.StatusCode);
        Assert.Equal("already rejected", Assert.IsType<ProblemDetails>(conflictResult.Value).Detail);

        var missing = new StubVerificationService { DecisionResult = DriverDecisionResult.MissingDocuments([DriverDocumentType.VehicleInsurance]) };
        var missingResult = Assert.IsType<ObjectResult>((await AdminController(missing).Approve(DriverId, new ApproveDriverRequestDto(), default)).Result);
        Assert.Equal(StatusCodes.Status409Conflict, missingResult.StatusCode);
    }

    // ---------------------------------------------------------------- SCRUM-43 enforcement mapping

    [Fact]
    public async Task GoOnline_Refused_Returns403_WithTheStateBody()
    {
        var state = new DriverStateEnforcementResponseDto { DriverId = DriverId, Status = DriverStatus.Rejected, CanAcceptTrips = false, Message = "Your registration was rejected: expired insurance." };
        var service = new StubVerificationService { OnlineResult = new DriverOnlineResult(false, state) };

        var result = await DriverController(service).GoOnline(default);

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Same(state, forbidden.Value);
    }

    [Fact]
    public async Task GoOnline_Allowed_Returns200_AndUnknownDriver_Returns404()
    {
        var state = new DriverStateEnforcementResponseDto { DriverId = DriverId, Status = DriverStatus.Active, CanAcceptTrips = true, Message = "online" };
        var allowed = await DriverController(new StubVerificationService { OnlineResult = new DriverOnlineResult(true, state) }).GoOnline(default);
        Assert.IsType<OkObjectResult>(allowed.Result);

        var unknown = await DriverController(new StubVerificationService { OnlineResult = null }).GoOnline(default);
        Assert.IsType<NotFoundResult>(unknown.Result);
    }
}
