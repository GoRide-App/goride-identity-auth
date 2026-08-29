using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SRC.Services.Interfaces;

namespace SRC.Services.Impl
{
    public class ProfileServiceImpl : IProfileService
    {
        private readonly HttpClient _http;
        public ProfileServiceImpl(HttpClient http) => _http = http;

        async Task<string> IProfileService.GetProfileAsync(string accessToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.asgardeo.io/t/goride/scim2/Me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Asgardeo profile GET failed ({(int)response.StatusCode}): {body}");

            return body;
        }

        async Task IProfileService.UpdateProfileAsync(string accessToken, ProfileUpdateRequest req)
        {
            var operations = new List<object>();

            if (req.GivenName is not null || req.FamilyName is not null)
            {
                var nameValue = new Dictionary<string, string>();
                if (req.GivenName is not null) nameValue["givenName"] = req.GivenName;
                if (req.FamilyName is not null) nameValue["familyName"] = req.FamilyName;

                operations.Add(new
                {
                    op = "replace",
                    value = new { name = nameValue }
                });
            }

            if (req.PhoneNumber is not null)
            {
                operations.Add(new
                {
                    op = "replace",
                    value = new { phoneNumbers = new[] { new { type = "mobile", value = req.PhoneNumber } } }
                });
            }  

            if (operations.Count == 0)
                throw new InvalidOperationException("No fields provided to update.");

            var patchBody = new
            {
                schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:PatchOp" },
                Operations = operations
            };

            var request = new HttpRequestMessage(HttpMethod.Patch, "https://api.asgardeo.io/t/goride/scim2/Me")
            {
                // Content = JsonContent.Create(patchBody)
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
                throw new HttpRequestException($"Asgardeo profile PATCH failed ({(int)response.StatusCode}): {errorBody}");
            }
        }
    }
}