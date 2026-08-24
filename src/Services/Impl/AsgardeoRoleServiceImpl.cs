using System.Net.Http.Headers;
using System.Text.Json;
using SRC.Services.Interfaces;

namespace SRC.Services.Impl
{
    public class AsgardeoRoleService : IAsgardeoRoleService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        public AsgardeoRoleService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;
        }

        // Internal helper only - not part of the interface
        private async Task<string> GetManagementTokenAsync()
        {
            var tokenEndpoint = "https://api.asgardeo.io/t/cspproject/oauth2/token";
            var body = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _config["AsgardeoMgmt:ClientId"] ?? throw new InvalidOperationException("Missing AsgardeoMgmt:ClientId"),
                ["client_secret"] = _config["AsgardeoMgmt:ClientSecret"] ?? throw new InvalidOperationException("Missing AsgardeoMgmt:ClientSecret"),
                ["scope"] = "internal_role_mgt_update" // confirm exact scope name in your console
            };

            var response = await _http.PostAsync(tokenEndpoint, new FormUrlEncodedContent(body));
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            return json.GetProperty("access_token").GetString()!;
        }

        public async Task AssignRoleAsync(string asgardeoUserId, string roleId)
        {
            var accessToken = await GetManagementTokenAsync();

            var patchBody = new
            {
                schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:PatchOp" },
                Operations = new[]
                {
                    new
                    {
                        op = "add",
                        path = "users",
                        value = new[] { new { value = asgardeoUserId } }
                    }
                }
            };

            var request = new HttpRequestMessage(
                HttpMethod.Patch,
                $"https://api.asgardeo.io/t/cspproject/scim2/v2/Roles/{roleId}")
            {
                Content = JsonContent.Create(patchBody)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
    }
}