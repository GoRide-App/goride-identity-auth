namespace GoRide.Api.Options;

/// <summary>Where the browser is sent after login/logout and which origin CORS trusts.</summary>
public class FrontendOptions
{
    public string BaseUrl { get; set; } = "http://localhost:3000";
}
