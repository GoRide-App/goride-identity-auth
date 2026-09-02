using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GoRide.Api.Options;
using Microsoft.Extensions.Options;
using SRC.Services.Interfaces;

namespace SRC.Services.Impl
{
    public class AsgardeoRoleServiceImpl : IAsgardeoRoleService
    {
        private const string RoleUsersUpdateScope = "internal_role_mgt_users_update";

        private readonly HttpClient _http;
        private readonly IAsgardeoManagementTokenProvider _tokens;
        private readonly IOptions<AsgardeoOptions> _asgardeo;
        private readonly ILogger<AsgardeoRoleServiceImpl> _logger;

        public AsgardeoRoleServiceImpl(
            HttpClient http,
            IAsgardeoManagementTokenProvider tokens,
            IOptions<AsgardeoOptions> asgardeo,
            ILogger<AsgardeoRoleServiceImpl> logger)
        {
            _http = http;
            _tokens = tokens;
            _asgardeo = asgardeo;
            _logger = logger;
        }

        public async Task AssignRoleAsync(string asgardeoUserId, string displayName, string roleId)
        {
            _logger.LogInformation("Assigning Asgardeo role {RoleId} to user {UserId}.", roleId, asgardeoUserId);

            var accessToken = await _tokens.GetTokenAsync(RoleUsersUpdateScope);

            var patchBody = new
            {
                schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:PatchOp" },
                Operations = new[]
                {
                    new
                    {
                        op = "add",
                        value = new
                        {
                            users = new[]
                            {
                                new { display = $"DEFAULT/{displayName}", value = asgardeoUserId }
                            }
                        }
                    }
                }
            };

            var request = new HttpRequestMessage(
                HttpMethod.Patch,
                $"{_asgardeo.Value.ScimRolesEndpoint}/{Uri.EscapeDataString(roleId)}")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(patchBody),
                    Encoding.UTF8,
                    "application/scim+json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Asgardeo Role PATCH failed ({(int)response.StatusCode}): {errorBody}");
            }
        }
    }
}
