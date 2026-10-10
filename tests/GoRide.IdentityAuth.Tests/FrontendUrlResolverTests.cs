using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SRC.Controllers;
using SRC.Services.Impl;

namespace GoRide.IdentityAuth.Tests;

public class FrontendUrlResolverTests
{
    private const string Primary = "https://goride-my-client.vercel.app";
    private const string Demo = "https://goride-demo.vercel.app";

    [Theory]
    [InlineData("goride-demo.vercel.app", "https", Demo)]
    [InlineData("goride-my-client.vercel.app", "https", Primary)]
    [InlineData("localhost:3000", "http", "http://localhost:3000")]
    [InlineData("unknown.example", "https", Primary)]
    [InlineData("goride-demo.vercel.app.unknown.example", "https", Primary)]
    [InlineData("goride-demo.vercel.app", "http", Primary)]
    [InlineData("goride-demo.vercel.app:8443", "https", Primary)]
    [InlineData("goride-demo.vercel.app", null, Primary)]
    [InlineData(null, "https", Primary)]
    [InlineData(null, null, Primary)]
    [InlineData(" GORIDE-DEMO.VERCEL.APP:443 ", " HTTPS ", Demo)]
    [InlineData("goride-demo.vercel.app, internal.example", "https, http", Demo)]
    [InlineData("unknown.example, goride-demo.vercel.app", "https, https", Primary)]
    [InlineData("goride-demo.vercel.app", "http, https", Primary)]
    [InlineData("goride-demo.vercel.app/path", "https", Primary)]
    [InlineData("unknown.example@goride-demo.vercel.app", "https", Primary)]
    public void Resolve_UsesOnlyAllowedForwardedOrigin(string? host, string? proto, string expected)
    {
        var request = CreateRequest(host, proto);

        Assert.Equal(expected, CreateResolver().Resolve(request));
    }

    [Theory]
    [InlineData("goride-demo.vercel.app", "https", Primary, Primary + "/page", Demo)]
    [InlineData("unknown.example", "https", Demo, Primary + "/page", Demo)]
    [InlineData(null, null, Demo, Primary + "/page", Demo)]
    [InlineData("goride-demo.vercel.app", null, Primary, Demo + "/page", Primary)]
    [InlineData(null, null, "https://unknown.example", Demo + "/page?q=1#part", Demo)]
    [InlineData(null, null, null, "HTTPS://GORIDE-DEMO.VERCEL.APP:443/page", Demo)]
    [InlineData(null, null, "null", Demo + "/page", Demo)]
    [InlineData(null, null, "invalid", "https://unknown.example", Primary)]
    [InlineData(null, null, "https://unknown.example", null, Primary)]
    public void Resolve_FallsBackThroughOriginRefererAndDefault(
        string? host, string? proto, string? origin, string? referer, string expected)
    {
        var request = CreateRequest(host, proto);
        request.Headers.Origin = origin;
        request.Headers.Referer = referer;

        Assert.Equal(expected, CreateResolver().Resolve(request));
    }

    [Fact]
    public void Resolve_NormalizesConfiguredUrlsAndKeepsNonDefaultPorts()
    {
        var resolver = new FrontendUrlResolver(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Frontend:BaseUrl"] = " HTTPS://GORIDE-MY-CLIENT.VERCEL.APP:443/ ",
                ["Frontend:AllowedBaseUrls:0"] = "HTTPS://GORIDE-DEMO.VERCEL.APP:8443/path/",
                ["Frontend:AllowedBaseUrls:1"] = "not a URL",
                ["Frontend:AllowedBaseUrls:2"] = "https://*.example.com"
            }).Build());

        Assert.Equal(Primary, resolver.Resolve(CreateRequest()));
        Assert.Equal("https://goride-demo.vercel.app:8443", resolver.Resolve(CreateRequest("goride-demo.vercel.app:8443", "https")));
        Assert.Equal(Primary, resolver.Resolve(CreateRequest("goride-demo.vercel.app", "https")));
        Assert.Equal(Primary, resolver.Resolve(CreateRequest("unlisted.example.com", "https")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/relative")]
    [InlineData("ftp://example.com")]
    public void Constructor_RequiresValidPrimaryFrontend(string? baseUrl)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Frontend:BaseUrl"] = baseUrl }).Build();

        Assert.Throws<InvalidOperationException>(() => new FrontendUrlResolver(configuration));
    }

    [Theory]
    [InlineData("goride-demo.vercel.app", "https", "https")]
    [InlineData("goride-demo.vercel.app, internal.example", "https, http", "http")]
    public async Task Resolve_PreservesFirstProxyOriginWhenForwardedHeadersMiddlewareConsumesProto(
        string host, string proto, string expectedScheme)
    {
        var request = CreateRequest(host, proto);
        var resolver = CreateResolver();
        Assert.Equal(Demo, resolver.Resolve(request));
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        };
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask,
            NullLoggerFactory.Instance, Options.Create(options));

        await middleware.Invoke(request.HttpContext);

        Assert.Equal(expectedScheme, request.Scheme);
        Assert.NotEqual(proto, request.Headers["X-Forwarded-Proto"].ToString());
        Assert.Equal(Demo, resolver.Resolve(request));
    }

    [Fact]
    public void Resolve_DoesNotTrustRequestHostWithoutFrontendHeaders()
    {
        var request = CreateRequest();
        request.Host = new HostString("goride-demo.vercel.app");

        Assert.Equal(Primary, CreateResolver().Resolve(request));
    }

    [Theory]
    [InlineData("goride-demo.vercel.app", Demo)]
    [InlineData("goride-my-client.vercel.app", Primary)]
    [InlineData("unknown.example", Primary)]
    public void Login_SavesFrontendAndUsesItForDefaultReturnUrl(string host, string expected)
    {
        var controller = CreateLoginController(CreateRequest(host, "https"));

        var result = Assert.IsType<ChallengeResult>(controller.Login(null, "login"));

        Assert.Equal(expected, result.Properties!.RedirectUri);
        Assert.Equal(expected, result.Properties.Items[FrontendUrlResolver.FrontendPropertyKey]);
        Assert.Equal("true", result.Properties.Items["forceFresh"]);
        Assert.Equal(OpenIdConnectDefaults.AuthenticationScheme, Assert.Single(result.AuthenticationSchemes));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/rides?tab=active#details")]
    [InlineData("rides/123")]
    [InlineData("../profile")]
    [InlineData("?tab=active")]
    [InlineData("#details")]
    [InlineData(Primary + "/account")]
    [InlineData(Demo + "/rides?tab=active#details")]
    [InlineData("HTTPS://GORIDE-DEMO.VERCEL.APP:443/rides")]
    [InlineData("http://localhost:3000/rides")]
    public void Login_AcceptsRelativeOrAllowedReturnUrl(string returnUrl)
    {
        var controller = CreateLoginController(CreateRequest("goride-demo.vercel.app", "https"));

        var result = Assert.IsType<ChallengeResult>(controller.Login(returnUrl, null));

        Assert.Equal(returnUrl, result.Properties!.RedirectUri);
        Assert.Equal(Demo, result.Properties.Items[FrontendUrlResolver.FrontendPropertyKey]);
        Assert.False(result.Properties.Items.ContainsKey("forceFresh"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("https://unknown.example/path")]
    [InlineData("https://goride-demo.vercel.app.unknown.example")]
    [InlineData("https://goride-demo.vercel.app@unknown.example")]
    [InlineData("https://user@goride-demo.vercel.app")]
    [InlineData("https://goride-demo.vercel.app:8443/path")]
    [InlineData("http://goride-demo.vercel.app")]
    [InlineData("//unknown.example/path")]
    [InlineData("///unknown.example/path")]
    [InlineData("/\\unknown.example/path")]
    [InlineData("\\\\unknown.example/path")]
    [InlineData("https://goride-demo.vercel.app\\@unknown.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https:unknown.example")]
    [InlineData("https:///unknown.example")]
    [InlineData("data:text/html,example")]
    [InlineData(" https://unknown.example")]
    [InlineData("/\r\n/unknown.example")]
    [InlineData("\t//unknown.example")]
    public void Login_IgnoresUnsafeReturnUrl(string? returnUrl)
    {
        var controller = CreateLoginController(CreateRequest("goride-demo.vercel.app", "https"));

        var result = Assert.IsType<ChallengeResult>(controller.Login(returnUrl, null));

        Assert.Equal(Demo, result.Properties!.RedirectUri);
    }

    [Theory]
    [InlineData(Demo, Demo)]
    [InlineData("HTTPS://GORIDE-DEMO.VERCEL.APP:443/", Demo)]
    [InlineData("https://unknown.example", Primary)]
    [InlineData(null, Primary)]
    public void Resolve_UsesOnlyAllowedFrontendFromAuthenticationProperties(string? saved, string expected)
    {
        var properties = new AuthenticationProperties();
        properties.Items[FrontendUrlResolver.FrontendPropertyKey] = saved;

        Assert.Equal(expected, CreateResolver().Resolve(CreateRequest(), properties));
    }

    private static LoginController CreateLoginController(HttpRequest request) => new(CreateResolver())
    {
        ControllerContext = new ControllerContext { HttpContext = request.HttpContext }
    };

    private static HttpRequest CreateRequest(string? host = null, string? proto = null)
    {
        var request = new DefaultHttpContext().Request;
        request.Scheme = "https";
        request.Host = new HostString("identity.example");
        request.Headers["X-Forwarded-Host"] = host;
        request.Headers["X-Forwarded-Proto"] = proto;
        return request;
    }

    private static FrontendUrlResolver CreateResolver() => new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Frontend:BaseUrl"] = Primary,
            ["Frontend:AllowedBaseUrls:0"] = Demo,
            ["Frontend:AllowedBaseUrls:1"] = "http://localhost:3000"
        }).Build());
}
