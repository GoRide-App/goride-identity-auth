using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SRC.Data;
using Microsoft.EntityFrameworkCore;

namespace SRC.Services.Impl
{
    public class UserDirectoryServiceImpl : IUserDirectoryService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly AppDbContext _context;

        public UserDirectoryServiceImpl(HttpClient http, IConfiguration config, AppDbContext context)
        {
            _http = http;
            _config = config;
            _context = context;
        }

        private async Task<string> GetManagementTokenAsync()
        {
            var clientId = _config["AsgardeoMgmt:ClientId"] ?? throw new InvalidOperationException("Missing AsgardeoMgmt:ClientId");
            var clientSecret = _config["AsgardeoMgmt:ClientSecret"] ?? throw new InvalidOperationException("Missing AsgardeoMgmt:ClientSecret");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.asgardeo.io/t/goride/oauth2/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["scope"] = "internal_user_mgt_view"
                })
            };

            var basicAuthValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuthValue);

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            return json.GetProperty("access_token").GetString()!;
        }

        async Task<string> IUserDirectoryService.GetUserByIdAsync(string userId)
        {

            var accessToken = await GetManagementTokenAsync();
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.asgardeo.io/t/goride/scim2/Users/{userId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode){
                var driver = await _context.DriverProfile.FirstOrDefaultAsync(v => v.DriverId == userId);
                if(driver is not null)
                    _context.DriverProfile.Remove(driver);
                await _context.SaveChangesAsync();
                throw new HttpRequestException($"Asgardeo user lookup failed and user got deleted from system DB!! ({(int)response.StatusCode}): {body}");
            }

            return body;
        }
    }
}