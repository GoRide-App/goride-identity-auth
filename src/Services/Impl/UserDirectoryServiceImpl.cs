using System.Net.Http.Headers;
using GoRide.Api.Options;
using Microsoft.Extensions.Options;
using SRC.Services.Interfaces;

namespace SRC.Services.Impl
{
    public class UserDirectoryServiceImpl : IUserDirectoryService
    {
        private const string UserViewScope = "internal_user_mgt_view";

        private readonly HttpClient _http;
        private readonly IAsgardeoManagementTokenProvider _tokens;
        private readonly IOptions<AsgardeoOptions> _asgardeo;

        public UserDirectoryServiceImpl(HttpClient http, IAsgardeoManagementTokenProvider tokens, IOptions<AsgardeoOptions> asgardeo)
        {
            _http = http;
            _tokens = tokens;
            _asgardeo = asgardeo;
        }

        async Task<string> IUserDirectoryService.GetUserByIdAsync(string userId)
        {
            var accessToken = await _tokens.GetTokenAsync(UserViewScope);
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_asgardeo.Value.ScimUsersEndpoint}/{Uri.EscapeDataString(userId)}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Asgardeo user lookup failed ({(int)response.StatusCode}): {body}");

            return body;
        }
    }
}
