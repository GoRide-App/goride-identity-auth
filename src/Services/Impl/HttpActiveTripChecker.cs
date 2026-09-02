using System.Net;
using System.Text.Json;
using GoRide.Api.Options;
using Microsoft.Extensions.Options;
using SRC.Services.Interfaces;

namespace SRC.Services.Impl;

/// <summary>
/// Asks the trip service whether the user has an in-flight trip. Unconfigured means the
/// check is skipped (and logged); configured-but-broken means we refuse to guess.
/// </summary>
public sealed class HttpActiveTripChecker : IActiveTripChecker
{
    private readonly HttpClient _http;
    private readonly IOptions<TripServiceOptions> _options;
    private readonly ILogger<HttpActiveTripChecker> _logger;

    public HttpActiveTripChecker(HttpClient http, IOptions<TripServiceOptions> options, ILogger<HttpActiveTripChecker> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public async Task<bool> HasActiveTripAsync(string userId, CancellationToken cancellationToken = default)
    {
        var opts = _options.Value;
        if (!opts.IsConfigured)
        {
            _logger.LogWarning(
                "TripService:BaseUrl is not configured; skipping the active-trip check for user {UserId}.", userId);
            return false;
        }

        var path = opts.ActiveTripPath.Replace("{userId}", Uri.EscapeDataString(userId));
        var url = opts.BaseUrl!.TrimEnd('/') + (path.StartsWith('/') ? path : "/" + path);

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrWhiteSpace(opts.ApiKey))
            request.Headers.Add("X-Internal-Api-Key", opts.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new TripStatusUnavailableException("The trip service could not be reached.", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
                return false; // user has no trips at all

            if (!response.IsSuccessStatusCode)
                throw new TripStatusUnavailableException(
                    $"The trip service answered {(int)response.StatusCode} while checking for active trips.");

            try
            {
                using var json = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                if (json.RootElement.TryGetProperty("hasActiveTrip", out var flag)
                    && flag.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    return flag.GetBoolean();
            }
            catch (JsonException ex)
            {
                throw new TripStatusUnavailableException("The trip service returned an unreadable response.", ex);
            }

            throw new TripStatusUnavailableException("The trip service response did not contain hasActiveTrip.");
        }
    }
}
