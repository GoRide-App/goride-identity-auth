namespace GoRide.Api.Options;

public class AsgardeoOptions
{
    public string BaseUrl { get; set; } = default!;
    public string ClientId { get; set; } = default!;
    public string ClientSecret { get; set; } = default!;

    public string TokenEndpoint => $"{BaseUrl}/oauth2/token";
    public string AuthorizeEndpoint => $"{BaseUrl}/oauth2/authorize";
    public string JwksUri => $"{BaseUrl}/oauth2/jwks";
    public string UserInfoEndpoint => $"{BaseUrl}/oauth2/userinfo";
    public string ScimUsersEndpoint => $"{BaseUrl}/scim2/Users";
}