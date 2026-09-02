using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GoRide.Api.Options;
using Microsoft.Extensions.Options;
using SRC.Services.Interfaces;

namespace SRC.Services.Impl;

public sealed class AsgardeoManagementTokenProvider : IAsgardeoManagementTokenProvider
{
    public const string HttpClientName = "asgardeo-management";

    private static readonly TimeSpan ExpirySafetyMargin = TimeSpan.FromSeconds(60);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<AsgardeoOptions> _asgardeo;
    private readonly IOptions<AsgardeoMgmtOptions> _mgmt;
    private readonly ILogger<AsgardeoManagementTokenProvider> _logger;
    private readonly TimeProvider _clock;
    private readonly ConcurrentDictionary<string, CachedToken> _cache = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AsgardeoManagementTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AsgardeoOptions> asgardeo,
        IOptions<AsgardeoMgmtOptions> mgmt,
        ILogger<AsgardeoManagementTokenProvider> logger,
        TimeProvider? clock = null)
    {
        _httpClientFactory = httpClientFactory;
        _asgardeo = asgardeo;
        _mgmt = mgmt;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<string> GetTokenAsync(string scope, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(scope, out var cached) && cached.IsValid(_clock.GetUtcNow()))
            return cached.AccessToken;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(scope, out cached) && cached.IsValid(_clock.GetUtcNow()))
                return cached.AccessToken;

            var fresh = await RequestTokenAsync(scope, cancellationToken);
            _cache[scope] = fresh;
            return fresh.AccessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<CachedToken> RequestTokenAsync(string scope, CancellationToken cancellationToken)
    {
        var clientId = _mgmt.Value.ClientId;
        var clientSecret = _mgmt.Value.ClientSecret;
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException("AsgardeoMgmt:ClientId and AsgardeoMgmt:ClientSecret must be configured.");

        var request = new HttpRequestMessage(HttpMethod.Post, _asgardeo.Value.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["scope"] = scope
            })
        };

        // client_secret_basic - matches the Token Request example in Asgardeo's console
        var basicAuthValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuthValue);

        var http = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Asgardeo management token request failed ({(int)response.StatusCode}): {body}");

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        var accessToken = root.GetProperty("access_token").GetString()
                          ?? throw new HttpRequestException("Asgardeo token response had no access_token.");
        var expiresIn = root.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var seconds)
            ? seconds
            : 3600;

        // Asgardeo echoes back only the scopes the M2M app is actually authorised for.
        var grantedScope = root.TryGetProperty("scope", out var s) ? s.GetString() : null;
        if (grantedScope is not null && !ScopeGranted(grantedScope, scope))
        {
            _logger.LogWarning(
                "Asgardeo granted scopes '{Granted}' but '{Requested}' was requested; check the M2M app's API authorisations.",
                grantedScope, scope);
        }

        return new CachedToken(accessToken, _clock.GetUtcNow().AddSeconds(expiresIn) - ExpirySafetyMargin);
    }

    private static bool ScopeGranted(string granted, string requested)
    {
        var grantedSet = granted.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        return requested.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(grantedSet.Contains);
    }

    private sealed record CachedToken(string AccessToken, DateTimeOffset ValidUntil)
    {
        public bool IsValid(DateTimeOffset now) => now < ValidUntil;
    }
}
