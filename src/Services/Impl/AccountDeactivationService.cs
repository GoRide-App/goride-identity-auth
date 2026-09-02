using Microsoft.EntityFrameworkCore;
using SRC.Data;
using SRC.Entities;
using SRC.Enums;
using SRC.Services.Interfaces;

namespace SRC.Services.Impl;

public sealed class AccountDeactivationService : IAccountDeactivationService
{
    private readonly AppDbContext _db;
    private readonly IIdentityAccountService _identityAccounts;
    private readonly IActiveTripChecker _trips;
    private readonly ILogger<AccountDeactivationService> _logger;
    private readonly TimeProvider _clock;

    public AccountDeactivationService(
        AppDbContext db,
        IIdentityAccountService identityAccounts,
        IActiveTripChecker trips,
        ILogger<AccountDeactivationService> logger,
        TimeProvider? clock = null)
    {
        _db = db;
        _identityAccounts = identityAccounts;
        _trips = trips;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<DeactivationResult> DeactivateAsync(string userId, CancellationToken cancellationToken = default)
    {
        var account = await _db.UserAccounts.SingleOrDefaultAsync(a => a.UserId == userId, cancellationToken);
        if (account is { Status: AccountStatus.Deactivated })
            return DeactivationResult.AlreadyDeactivated(account.DeactivatedAt);

        // Scenario 3: a trip that is not in a terminal state blocks deactivation.
        bool hasActiveTrip;
        try
        {
            hasActiveTrip = await _trips.HasActiveTripAsync(userId, cancellationToken);
        }
        catch (TripStatusUnavailableException ex)
        {
            _logger.LogError(ex, "Could not verify trip status for {UserId}; refusing to deactivate.", userId);
            return DeactivationResult.TripStatusUnavailable(
                "We could not confirm that you have no trip in progress. Please try again in a few minutes.");
        }

        if (hasActiveTrip)
            return DeactivationResult.BlockedByActiveTrip();

        // Scenario 1, step 1: the Identity Server is the authority — disable there first so a
        // failure never leaves an account that is locally "deactivated" yet still able to log in.
        try
        {
            await _identityAccounts.DisableAccountAsync(userId, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Identity Server refused to disable account {UserId}.", userId);
            return DeactivationResult.IdentityServerRejected(
                "The identity provider could not disable the account. Nothing has been changed; please try again later.");
        }

        // Scenario 1, step 2: soft-delete locally. The row (and the driver profile) stay so that
        // trip history referencing this user id remains intact.
        var now = _clock.GetUtcNow().UtcDateTime;
        if (account is null)
        {
            _db.UserAccounts.Add(new UserAccount
            {
                UserId = userId,
                Status = AccountStatus.Deactivated,
                DeactivatedAt = now
            });
        }
        else
        {
            account.Status = AccountStatus.Deactivated;
            account.DeactivatedAt = now;
        }

        var driver = await _db.DriverProfile.SingleOrDefaultAsync(d => d.DriverId == userId, cancellationToken);
        if (driver is not null)
            driver.Status = DriverStatus.Deactivated;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Account {UserId} deactivated at {At:o}.", userId, now);
        return DeactivationResult.Deactivated(now);
    }

    public Task<bool> IsDeactivatedAsync(string userId, CancellationToken cancellationToken = default) =>
        _db.UserAccounts
            .AsNoTracking()
            .AnyAsync(a => a.UserId == userId && a.Status == AccountStatus.Deactivated, cancellationToken);
}
