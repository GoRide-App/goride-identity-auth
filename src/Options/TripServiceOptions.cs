namespace GoRide.Api.Options;

/// <summary>
/// Location of the trip service used to check for in-flight trips before an account
/// is deactivated. When <see cref="BaseUrl"/> is empty the check is skipped (logged),
/// which is the state of the project until the trip service is deployed.
/// </summary>
public class TripServiceOptions
{
    public string? BaseUrl { get; set; }

    /// <summary>Sent as the X-Internal-Api-Key header, mirroring the internal-users endpoint.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Relative path template; <c>{userId}</c> is replaced with the URL-encoded Asgardeo user id.
    /// Expected reply: 200 with <c>{ "hasActiveTrip": true|false }</c>, or 404 when the user has no trips.
    /// </summary>
    public string ActiveTripPath { get; set; } = "/api/internal/users/{userId}/active-trip";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl);
}
