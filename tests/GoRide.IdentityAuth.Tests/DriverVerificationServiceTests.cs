using Microsoft.EntityFrameworkCore;
using SRC.Data;
using SRC.Entities;
using SRC.Enums;
using SRC.Services.Impl;
using SRC.Services.Interfaces;

namespace GoRide.IdentityAuth.Tests;

public class DriverVerificationServiceTests
{
    private const string DriverId = "driver-1";
    private const string AdminId = "admin-9";

    private readonly FakeClock _clock = new();

    private DriverVerificationService CreateSut(AppDbContext db) =>
        new(db, TestOptions.Logger<DriverVerificationService>(), _clock);

    private static async Task<DriverProfile> SeedDriverAsync(AppDbContext db, DriverStatus status = DriverStatus.PendingVerification)
    {
        var driver = new DriverProfile
        {
            DriverId = DriverId,
            VehicleMake = "Toyota",
            VehicleModel = "Aqua",
            VehiclePlate = "CAB-1234",
            VehicleTypeCode = "car",
            LicenseNumber = "B1234567",
            LicenseExpiry = new DateOnly(2030, 1, 1),
            Status = status
        };
        db.DriverProfile.Add(driver);
        await db.SaveChangesAsync();
        return driver;
    }

    private static DriverDocumentUpload Upload(DriverDocumentType type, byte[]? content = null, string fileName = "scan.jpg",
        string? number = null, DateOnly? expires = null) =>
        new(type, fileName, "image/jpeg", content ?? DriverDocumentValidatorTests.Jpeg(), number, expires);

    private async Task UploadRequiredSetAsync(DriverVerificationService sut)
    {
        foreach (var type in DriverDocumentTypes.RequiredForApproval)
            Assert.Equal(DocumentUploadOutcome.Stored, (await sut.UploadDocumentAsync(DriverId, Upload(type))).Outcome);
    }

    // ---------------------------------------------------------------- SCRUM-42

    [Fact]
    public async Task Upload_ValidDocument_IsPersisted_AndVisibleOnTheProfileImmediately()
    {
        using var db = TestDb.Create();
        await SeedDriverAsync(db);
        var sut = CreateSut(db);

        var result = await sut.UploadDocumentAsync(DriverId,
            Upload(DriverDocumentType.DrivingLicence, fileName: "C:\\scans\\licence front.jpg", number: " B1234567 ", expires: new DateOnly(2031, 6, 30)));

        Assert.Equal(DocumentUploadOutcome.Stored, result.Outcome);
        var doc = result.Document!;
        Assert.Equal(DriverDocumentType.DrivingLicence, doc.Type);
        Assert.Equal("licence front.jpg", doc.FileName); // path stripped
        Assert.Equal("image/jpeg", doc.ContentType);      // from the signature, not the client
        Assert.Equal("B1234567", doc.DocumentNumber);     // trimmed
        Assert.Equal(new DateOnly(2031, 6, 30), doc.ExpiresOn);
        Assert.Equal(_clock.Now.UtcDateTime, doc.UploadedAt);

        var profile = await sut.GetProfileAsync(DriverId);
        var listed = Assert.Single(profile!.Documents);
        Assert.Equal(doc.Id, listed.Id);
        Assert.Equal([DriverDocumentType.VehicleRegistration, DriverDocumentType.VehicleInsurance], profile.MissingDocuments);
        Assert.Equal(DriverStatus.PendingVerification, profile.Status); // set not complete yet
    }

    [Fact]
    public async Task Upload_SameTypeAgain_ReplacesInsteadOfDuplicating()
    {
        using var db = TestDb.Create();
        await SeedDriverAsync(db);
        var sut = CreateSut(db);

        await sut.UploadDocumentAsync(DriverId, Upload(DriverDocumentType.VehicleInsurance, fileName: "old.jpg"));
        _clock.Advance(TimeSpan.FromMinutes(5));
        await sut.UploadDocumentAsync(DriverId, Upload(DriverDocumentType.VehicleInsurance, DriverDocumentValidatorTests.Pdf(), fileName: "new.pdf"));

        var stored = await db.DriverDocuments.SingleAsync();
        Assert.Equal("new.pdf", stored.FileName);
        Assert.Equal("application/pdf", stored.ContentType);
        Assert.Equal(_clock.Now.UtcDateTime, stored.UploadedAt);
    }

    [Fact]
    public async Task Upload_CompletingTheRequiredSet_MovesPendingDriverToDocumentReview_WithAuditRow()
    {
        using var db = TestDb.Create();
        await SeedDriverAsync(db);
        var sut = CreateSut(db);

        await UploadRequiredSetAsync(sut);

        var driver = await db.DriverProfile.SingleAsync();
        Assert.Equal(DriverStatus.DocumentReview, driver.Status);
        var change = Assert.Single(db.DriverStatusChanges);
        Assert.Equal(DriverStatus.PendingVerification, change.FromStatus);
        Assert.Equal(DriverStatus.DocumentReview, change.ToStatus);
        Assert.Equal(DriverId, change.ChangedBy);

        var profile = await sut.GetProfileAsync(DriverId);
        Assert.Empty(profile!.MissingDocuments);
        Assert.Contains("awaiting admin review", profile.StatusReason);
    }

    [Fact]
    public async Task Upload_ByRejectedDriver_ReturnsToDocumentReview_ButApprovedDriverKeepsState()
    {
        using var db = TestDb.Create();
        await SeedDriverAsync(db, DriverStatus.Rejected);
        var sut = CreateSut(db);
        await UploadRequiredSetAsync(sut);
        Assert.Equal(DriverStatus.DocumentReview, (await db.DriverProfile.SingleAsync()).Status);

        using var db2 = TestDb.Create();
        await SeedDriverAsync(db2, DriverStatus.Active);
        var sut2 = CreateSut(db2);
        await UploadRequiredSetAsync(sut2);
        Assert.Equal(DriverStatus.Active, (await db2.DriverProfile.SingleAsync()).Status);
    }

    [Fact]
    public async Task Upload_InvalidFile_IsRejected_AndNothingIsWritten()
    {
        using var db = TestDb.Create();
        await SeedDriverAsync(db);
        var sut = CreateSut(db);

        var result = await sut.UploadDocumentAsync(DriverId,
            Upload(DriverDocumentType.DrivingLicence, System.Text.Encoding.ASCII.GetBytes("GIF89a not allowed")));

        Assert.Equal(DocumentUploadOutcome.InvalidFile, result.Outcome);
        Assert.Contains("JPEG, PNG or PDF", result.Error);
        Assert.False(await db.DriverDocuments.AnyAsync());
        Assert.Empty(db.DriverStatusChanges);
    }

    [Fact]
    public async Task Upload_WithoutDriverProfile_IsRefused_AndNothingIsWritten()
    {
        using var db = TestDb.Create();
        var sut = CreateSut(db);

        var result = await sut.UploadDocumentAsync("nobody", Upload(DriverDocumentType.DrivingLicence));

        Assert.Equal(DocumentUploadOutcome.NoDriverProfile, result.Outcome);
        Assert.False(await db.DriverDocuments.AnyAsync());
    }

    [Fact]
    public async Task GetDocument_ReturnsStoredBytes_AndNullForUnknownType()
    {
        using var db = TestDb.Create();
        await SeedDriverAsync(db);
        var sut = CreateSut(db);
        var bytes = DriverDocumentValidatorTests.Png(128);
        await sut.UploadDocumentAsync(DriverId, Upload(DriverDocumentType.VehicleRegistration, bytes, "cr.png"));

        var file = await sut.GetDocumentAsync(DriverId, DriverDocumentType.VehicleRegistration);

        Assert.NotNull(file);
        Assert.Equal("cr.png", file.FileName);
        Assert.Equal("image/png", file.ContentType);
        Assert.Equal(bytes, file.Content);
        Assert.Null(await sut.GetDocumentAsync(DriverId, DriverDocumentType.VehicleRevenueLicence));
    }

    // ---------------------------------------------------------------- SCRUM-43: decisions

    [Fact]
    public async Task Approve_WithDocuments_ActivatesDriver_RecordsDecision_AndStampsVerifiedAt()
    {
        using var db = TestDb.Create();
        await SeedDriverAsync(db);
        var sut = CreateSut(db);
        await UploadRequiredSetAsync(sut);
        _clock.Advance(TimeSpan.FromHours(2));

        var result = await sut.ApproveAsync(DriverId, AdminId, "  Checked against DMT records ");

        Assert.Equal(DriverDecisionOutcome.Applied, result.Outcome);
        Assert.Equal(DriverStatus.Active, result.Profile!.Status);
        Assert.True(result.Profile.CanAcceptTrips);
        Assert.Equal(_clock.Now.UtcDateTime, result.Profile.VerifiedAt);
        Assert.Equal("Checked against DMT records", result.Profile.StatusReason);

        var decision = db.DriverStatusChanges.OrderByDescending(c => c.Id).First();
        Assert.Equal(DriverStatus.DocumentReview, decision.FromStatus);
        Assert.Equal(DriverStatus.Active, decision.ToStatus);
        Assert.Equal(AdminId, decision.ChangedBy);
    }

    [Fact]
    public async Task Approve_WithoutReason_UsesADefaultReason()
    {
        using var db = TestDb.Create();
        await SeedDriverAsync(db);
        var sut = CreateSut(db);
        await UploadRequiredSetAsync(sut);

        var result = await sut.ApproveAsync(DriverId, AdminId, null);

        Assert.Equal(DriverDecisionOutcome.Applied, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.Profile!.StatusReason));
    }

    [Fact]
    public async Task Approve_WithMissingDocuments_IsRefused_AndNamesThem()
    {
        using var db = TestDb.Create();
        await SeedDriverAsync(db);
        var sut = CreateSut(db);
        await sut.UploadDocumentAsync(DriverId, Upload(DriverDocumentType.DrivingLicence));

        var result = await sut.ApproveAsync(DriverId, AdminId, null);

        Assert.Equal(DriverDecisionOutcome.MissingDocuments, result.Outcome);
        Assert.Contains("VehicleRegistration", result.Error);
        Assert.Contains("VehicleInsurance", result.Error);
        Assert.Equal(DriverStatus.PendingVerification, (await db.DriverProfile.SingleAsync()).Status);
    }

    [Theory]
    [InlineData(DriverStatus.Active)]
    [InlineData(DriverStatus.Offline)]
    [InlineData(DriverStatus.Suspended)]
    [InlineData(DriverStatus.Deactivated)]
    public async Task Approve_FromNonApprovableState_IsRefused(DriverStatus status)
    {
        using var db = TestDb.Create();
        await SeedDriverAsync(db, status);
        var sut = CreateSut(db);
        await UploadRequiredSetAsync(sut);

        var result = await sut.ApproveAsync(DriverId, AdminId, null);

        Assert.Equal(DriverDecisionOutcome.InvalidTransition, result.Outcome);
        Assert.Equal(status, (await db.DriverProfile.SingleAsync()).Status);
    }

    [Fact]
    public async Task Approve_UnknownDriver_IsNotFound()
    {
        using var db = TestDb.Create();
        Assert.Equal(DriverDecisionOutcome.NotFound, (await CreateSut(db).ApproveAsync("ghost", AdminId, null)).Outcome);
        Assert.Equal(DriverDecisionOutcome.NotFound, (await CreateSut(db).RejectAsync("ghost", AdminId, "x")).Outcome);
    }

    [Fact]
    public async Task Reject_RecordsReason_ClearsVerification_AndDriverSeesTheReason()
    {
        using var db = TestDb.Create();
        var driver = await SeedDriverAsync(db, DriverStatus.Active);
        driver.VerifiedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var result = await sut.RejectAsync(DriverId, AdminId, "Insurance policy expired on 2026-05-01.");

        Assert.Equal(DriverDecisionOutcome.Applied, result.Outcome);
        Assert.Equal(DriverStatus.Rejected, result.Profile!.Status);
        Assert.Null(result.Profile.VerifiedAt);
        Assert.False(result.Profile.CanAcceptTrips);
        Assert.Equal("Insurance policy expired on 2026-05-01.", result.Profile.StatusReason);

        var state = await sut.GetEnforcementStateAsync(DriverId);
        Assert.Contains("Insurance policy expired", state!.Message);
        Assert.False(state.CanAcceptTrips);
    }

    [Theory]
    [InlineData(DriverStatus.Rejected)]
    [InlineData(DriverStatus.Deactivated)]
    public async Task Reject_FromRejectedOrDeactivated_IsRefused(DriverStatus status)
    {
        using var db = TestDb.Create();
        await SeedDriverAsync(db, status);

        var result = await CreateSut(db).RejectAsync(DriverId, AdminId, "again");

        Assert.Equal(DriverDecisionOutcome.InvalidTransition, result.Outcome);
        Assert.Empty(db.DriverStatusChanges);
    }

    // ---------------------------------------------------------------- SCRUM-43: enforcement

    [Fact]
    public async Task GoOnline_AfterApproval_Succeeds_AndAfterRejection_IsRefusedImmediately()
    {
        using var db = TestDb.Create();
        await SeedDriverAsync(db);
        var sut = CreateSut(db);
        await UploadRequiredSetAsync(sut);
        await sut.ApproveAsync(DriverId, AdminId, null);

        var offline = await sut.GoOfflineAsync(DriverId);
        Assert.True(offline!.Allowed);
        Assert.Equal(DriverStatus.Offline, offline.State.Status);

        var online = await sut.GoOnlineAsync(DriverId);
        Assert.True(online!.Allowed);
        Assert.Equal(DriverStatus.Active, online.State.Status);
        Assert.True(online.State.CanAcceptTrips);

        await sut.RejectAsync(DriverId, AdminId, "Plate does not match registration.");

        var refused = await sut.GoOnlineAsync(DriverId);
        Assert.False(refused!.Allowed);
        Assert.Equal(DriverStatus.Rejected, refused.State.Status);
        Assert.False(refused.State.CanAcceptTrips);
        Assert.Contains("Plate does not match registration.", refused.State.Message);
        Assert.Equal(DriverStatus.Rejected, (await db.DriverProfile.SingleAsync()).Status); // not flipped
    }

    [Theory]
    [InlineData(DriverStatus.PendingVerification)]
    [InlineData(DriverStatus.DocumentReview)]
    [InlineData(DriverStatus.Suspended)]
    [InlineData(DriverStatus.Deactivated)]
    public async Task GoOnline_FromUnapprovedState_IsRefused(DriverStatus status)
    {
        using var db = TestDb.Create();
        await SeedDriverAsync(db, status);

        var result = await CreateSut(db).GoOnlineAsync(DriverId);

        Assert.False(result!.Allowed);
        Assert.False(result.State.CanAcceptTrips);
        Assert.False(string.IsNullOrWhiteSpace(result.State.Message));
    }

    [Fact]
    public async Task Enforcement_ForUnknownDriver_IsNull()
    {
        using var db = TestDb.Create();
        var sut = CreateSut(db);
        Assert.Null(await sut.GetEnforcementStateAsync("ghost"));
        Assert.Null(await sut.GoOnlineAsync("ghost"));
        Assert.Null(await sut.GoOfflineAsync("ghost"));
    }
}
