namespace SRC.Services.Interfaces;

public interface IAccountDeactivationService
{
    /// <summary>
    /// Disables the account in the Identity Server and soft-deletes the local profile row.
    /// Never throws for the business outcomes below; only infrastructure failures surface as exceptions.
    /// </summary>
    Task<DeactivationResult> DeactivateAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>True when the local profile row says the account has been deactivated.</summary>
    Task<bool> IsDeactivatedAsync(string userId, CancellationToken cancellationToken = default);
}

public enum DeactivationOutcome
{
    Deactivated,
    AlreadyDeactivated,
    BlockedByActiveTrip,
    TripStatusUnavailable,
    IdentityServerRejected
}

public sealed record DeactivationResult(DeactivationOutcome Outcome, DateTime? DeactivatedAt = null, string? Detail = null)
{
    public static DeactivationResult Deactivated(DateTime at) => new(DeactivationOutcome.Deactivated, at);
    public static DeactivationResult AlreadyDeactivated(DateTime? at) => new(DeactivationOutcome.AlreadyDeactivated, at);
    public static DeactivationResult BlockedByActiveTrip() => new(
        DeactivationOutcome.BlockedByActiveTrip,
        Detail: "You have a trip that is still in progress. Finish or cancel it before deactivating your account.");
    public static DeactivationResult TripStatusUnavailable(string detail) => new(DeactivationOutcome.TripStatusUnavailable, Detail: detail);
    public static DeactivationResult IdentityServerRejected(string detail) => new(DeactivationOutcome.IdentityServerRejected, Detail: detail);
}
