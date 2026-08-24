using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SRC.Services.Interfaces;

namespace SRC.Services.Impl
{
    public class AsgardeoRoleServiceImpl : IAsgardeoRoleService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        public AsgardeoRoleServiceImpl(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;
        }

        // Internal helper only - not part of the interface
        private async Task<string> GetManagementTokenAsync()
        {
            var clientId = _config["AsgardeoMgmt:ClientId"] ?? throw new InvalidOperationException("Missing AsgardeoMgmt:ClientId");
            var clientSecret = _config["AsgardeoMgmt:ClientSecret"] ?? throw new InvalidOperationException("Missing AsgardeoMgmt:ClientSecret");

            var tokenEndpoint = "https://api.asgardeo.io/t/goride/oauth2/token";

            var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["scope"] = "internal_role_mgt_users_update"
                })
            };

            // client_secret_basic - matches the Token Request example in Asgardeo's console
            var basicAuthValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuthValue);

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            return json.GetProperty("access_token").GetString()!;
        }

        public async Task AssignRoleAsync(string asgardeoUserId, string displayName, string roleId)
        {
            Console.WriteLine($"[Asgardeo] Assigning role {roleId} to user '{asgardeoUserId}' (display: {displayName})");

            var accessToken = await GetManagementTokenAsync();

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
                $"https://api.asgardeo.io/t/goride/scim2/v2/Roles/{roleId}")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(patchBody),
                    Encoding.UTF8,
                    "application/scim+json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Asgardeo Role PATCH failed ({(int)response.StatusCode}): {errorBody}");
            }
        }
    }
}