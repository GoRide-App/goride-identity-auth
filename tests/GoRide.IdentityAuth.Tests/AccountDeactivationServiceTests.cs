using Microsoft.EntityFrameworkCore;
using SRC.Entities;
using SRC.Enums;
using SRC.Services.Impl;
using SRC.Services.Interfaces;

namespace GoRide.IdentityAuth.Tests;

public class AccountDeactivationServiceTests
{
    private const string UserId = "3f8a4b1e-1111-2222-3333-444455556666";

    private readonly FakeIdentityAccountService _idp = new();
    private readonly FakeActiveTripChecker _trips = new();
    private readonly FakeClock _clock = new();

    private AccountDeactivationService CreateSut(SRC.Data.AppDbContext db) =>
        new(db, _idp, _trips, TestOptions.Logger<AccountDeactivationService>(), _clock);

    // Scenario 1: successful deactivation
    [Fact]
    public async Task Deactivate_DisablesIdentityServerAccount_AndMarksLocalRowDeactivated()
    {
        using var db = TestDb.Create();
        var sut = CreateSut(db);

        var result = await sut.DeactivateAsync(UserId);

        Assert.Equal(DeactivationOutcome.Deactivated, result.Outcome);
        Assert.Equal(_clock.Now.UtcDateTime, result.DeactivatedAt);
        Assert.Equal(new[] { UserId }, _idp.DisabledUserIds);

        var row = await db.UserAccounts.SingleAsync();
        Assert.Equal(UserId, row.UserId);
        Assert.Equal(AccountStatus.Deactivated, row.Status);
        Assert.Equal(_clock.Now.UtcDateTime, row.DeactivatedAt);
    }

    [Fact]
    public async Task Deactivate_KeepsExistingRow_AndFlipsStatus_SoftDelete()
    {
        using var db = TestDb.Create();
        db.UserAccounts.Add(new UserAccount { UserId = UserId, Status = AccountStatus.Active });
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var result = await sut.DeactivateAsync(UserId);

        Assert.Equal(DeactivationOutcome.Deactivated, result.Outcome);
        var row = await db.UserAccounts.SingleAsync(); // still exactly one row, never deleted
        Assert.Equal(AccountStatus.Deactivated, row.Status);
        Assert.NotNull(row.DeactivatedAt);
    }

    [Fact]
    public async Task Deactivate_AlsoMarksDriverProfileDeactivated_WhenUserIsADriver()
    {
        using var db = TestDb.Create();
        db.DriverProfile.Add(new DriverProfile
        {
            DriverId = UserId,
            VehicleMake = "Toyota",
            VehicleModel = "Prius",
            VehiclePlate = "CAB-1234",
            VehicleTypeCode = "car",
            LicenseNumber = "B1234567",
            LicenseExpiry = new DateOnly(2030, 1, 1),
            Status = DriverStatus.Active
        });
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.DeactivateAsync(UserId);

        var driver = await db.DriverProfile.SingleAsync();
        Assert.Equal(DriverStatus.Deactivated, driver.Status);
        Assert.Equal("CAB-1234", driver.VehiclePlate); // record retained for history
    }

    // Scenario 3: active trip blocks deactivation
    [Fact]
    public async Task Deactivate_WithActiveTrip_IsRefused_AndNothingChanges()
    {
        using var db = TestDb.Create();
        _trips.HasActiveTrip = true;
        var sut = CreateSut(db);

        var result = await sut.DeactivateAsync(UserId);

        Assert.Equal(DeactivationOutcome.BlockedByActiveTrip, result.Outcome);
        Assert.Contains("trip", result.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_idp.DisabledUserIds);
        Assert.False(await db.UserAccounts.AnyAsync());
    }

    [Fact]
    public async Task Deactivate_WhenTripStatusCannotBeVerified_IsRefused_AndIdentityServerUntouched()
    {
        using var db = TestDb.Create();
        _trips.Throw = new TripStatusUnavailableException("boom");
        var sut = CreateSut(db);

        var result = await sut.DeactivateAsync(UserId);

        Assert.Equal(DeactivationOutcome.TripStatusUnavailable, result.Outcome);
        Assert.Empty(_idp.DisabledUserIds);
        Assert.False(await db.UserAccounts.AnyAsync());
    }

    [Fact]
    public async Task Deactivate_WhenIdentityServerRejects_DoesNotSoftDeleteLocally()
    {
        using var db = TestDb.Create();
        _idp.ThrowOnDisable = new HttpRequestException("403 forbidden");
        var sut = CreateSut(db);

        var result = await sut.DeactivateAsync(UserId);

        Assert.Equal(DeactivationOutcome.IdentityServerRejected, result.Outcome);
        Assert.False(await db.UserAccounts.AnyAsync());
    }

    [Fact]
    public async Task Deactivate_Twice_IsIdempotent_AndDoesNotCallIdentityServerAgain()
    {
        using var db = TestDb.Create();
        var sut = CreateSut(db);
        var first = await sut.DeactivateAsync(UserId);

        _clock.Advance(TimeSpan.FromHours(1));
        var second = await sut.DeactivateAsync(UserId);

        Assert.Equal(DeactivationOutcome.AlreadyDeactivated, second.Outcome);
        Assert.Equal(first.DeactivatedAt, second.DeactivatedAt);
        Assert.Single(_idp.DisabledUserIds);
        Assert.Equal(1, _trips.Calls); // the second call short-circuits before the trip check
    }

    // Scenario 2 support: the auth pipeline asks this to refuse sessions/logins
    [Fact]
    public async Task IsDeactivated_ReflectsLocalRow()
    {
        using var db = TestDb.Create();
        var sut = CreateSut(db);

        Assert.False(await sut.IsDeactivatedAsync(UserId));
        await sut.DeactivateAsync(UserId);
        Assert.True(await sut.IsDeactivatedAsync(UserId));
        Assert.False(await sut.IsDeactivatedAsync("someone-else"));
    }
}
