using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GoRide.Api.Options;
using Microsoft.Extensions.Options;
using SRC.Services.Interfaces;

namespace SRC.Services.Impl;

public sealed class AsgardeoAccountServiceImpl : IIdentityAccountService
{
    /// <summary>Scope the M2M application needs for PATCH /scim2/Users/{id}.</summary>
    public const string UserUpdateScope = "internal_user_mgt_update";

    private readonly HttpClient _http;
    private readonly IAsgardeoManagementTokenProvider _tokens;
    private readonly IOptions<AsgardeoOptions> _asgardeo;
    private readonly ILogger<AsgardeoAccountServiceImpl> _logger;

    public AsgardeoAccountServiceImpl(
        HttpClient http,
        IAsgardeoManagementTokenProvider tokens,
        IOptions<AsgardeoOptions> asgardeo,
        ILogger<AsgardeoAccountServiceImpl> logger)
    {
        _http = http;
        _tokens = tokens;
        _asgardeo = asgardeo;
        _logger = logger;
    }

    public async Task DisableAccountAsync(string userId, CancellationToken cancellationToken = default)
    {
        var accessToken = await _tokens.GetTokenAsync(UserUpdateScope, cancellationToken);

        // WSO2's SCIM2 extension schema carries the account-disable flag.
        var patchBody = new
        {
            schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:PatchOp" },
            Operations = new object[]
            {
                new
                {
                    op = "replace",
                    value = new Dictionary<string, object>
                    {
                        ["urn:scim:wso2:schema"] = new { accountDisabled = true }
                    }
                }
            }
        };

        var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"{_asgardeo.Value.ScimUsersEndpoint}/{Uri.EscapeDataString(userId)}")
        {
            Content = new StringContent(JsonSerializer.Serialize(patchBody), Encoding.UTF8, "application/scim+json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Asgardeo account disable PATCH failed ({(int)response.StatusCode}): {errorBody}",
                inner: null,
                statusCode: response.StatusCode);
        }

        _logger.LogInformation("Asgardeo account {UserId} set to accountDisabled=true.", userId);
    }
}
