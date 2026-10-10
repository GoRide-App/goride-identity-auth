using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using SRC.Services.Impl;

namespace GoRide.IdentityAuth.Tests;

public class FrontendAuthenticationTests
{
    private const string Primary = "https://goride-my-client.vercel.app";
    private const string Demo = "https://goride-demo.vercel.app";

    [Theory]
    [InlineData("goride-demo.vercel.app", Demo, false)]
    [InlineData("goride-my-client.vercel.app", Primary, false)]
    [InlineData("unknown.example", Primary, false)]
    [InlineData("goride-demo.vercel.app", Demo, true)]
    [InlineData("goride-my-client.vercel.app", Primary, true)]
    [InlineData("unknown.example", Primary, true)]
    public async Task Challenge_UsesSameFrontendForAuthorizationAndCodeRedemption(
        string host, string expected, bool usePar)
    {
        using var backchannel = new RecordingBackchannel();
        using var services = CreateServices(backchannel, usePar);
        using var scope = services.CreateScope();
        var context = CreateContext(scope.ServiceProvider, "/login", host);
        var resolver = scope.ServiceProvider.GetRequiredService<FrontendUrlResolver>();
        var properties = resolver.CreateProperties(context.Request);
        properties.Items["forceFresh"] = "true";

        await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, properties);

        var redirect = new Uri(context.Response.Headers.Location.ToString());
        var query = QueryHelpers.ParseQuery(redirect.Query);
        var authorization = usePar ? Assert.Single(backchannel.Requests).Parameters : query;
        Assert.Equal(expected + "/signin-oidc", authorization["redirect_uri"].ToString());
        Assert.Equal("login", authorization["prompt"].ToString());
        Assert.Equal("S256", authorization["code_challenge_method"].ToString());
        if (usePar)
        {
            Assert.Equal("urn:test:request", query["request_uri"].ToString());
        }

        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
        var state = authorization["state"].ToString();
        var saved = options.StateDataFormat.Unprotect(state);
        Assert.NotNull(saved);
        Assert.Equal(expected, saved.RedirectUri);
        Assert.Equal(expected, saved.Items[FrontendUrlResolver.FrontendPropertyKey]);
        Assert.Equal(expected + "/signin-oidc", saved.Items[OpenIdConnectDefaults.RedirectUriForCodePropertiesKey]);

        // The callback has different frontend headers: redemption must use protected state.
        using var callbackScope = services.CreateScope();
        var callback = CreateContext(callbackScope.ServiceProvider, "/signin-oidc", "unknown.example");
        callback.Request.QueryString = QueryString.Create(new Dictionary<string, string?>
        {
            ["state"] = state,
            ["code"] = "test-code"
        });
        callback.Request.Headers.Cookie = string.Join("; ", context.Response.Headers.SetCookie
            .Select(cookie => cookie!.Split(';', 2)[0]));
        var handler = await callback.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>()
            .GetHandlerAsync(callback, OpenIdConnectDefaults.AuthenticationScheme);

        Assert.True(await Assert.IsAssignableFrom<IAuthenticationRequestHandler>(handler).HandleRequestAsync());

        var tokenRequest = Assert.Single(backchannel.Requests, request => request.Path == "/token");
        Assert.Equal(expected + "/signin-oidc", tokenRequest.Parameters["redirect_uri"].ToString());
        Assert.Equal("authorization_code", tokenRequest.Parameters["grant_type"].ToString());
        Assert.False(StringValues.IsNullOrEmpty(tokenRequest.Parameters["code_verifier"]));
    }

    [Theory]
    [InlineData("goride-demo.vercel.app", Demo)]
    [InlineData("goride-my-client.vercel.app", Primary)]
    [InlineData("unknown.example", Primary)]
    public async Task SignOut_PreservesFrontendForCallbackAndFinalRedirect(string host, string expected)
    {
        using var backchannel = new RecordingBackchannel();
        using var services = CreateServices(backchannel);
        using var scope = services.CreateScope();
        var context = CreateContext(scope.ServiceProvider, "/logout", host);
        var properties = scope.ServiceProvider.GetRequiredService<FrontendUrlResolver>().CreateProperties(context.Request);
        var result = Results.SignOut(properties,
            [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);

        await result.ExecuteAsync(context);

        var query = QueryHelpers.ParseQuery(new Uri(context.Response.Headers.Location.ToString()).Query);
        Assert.Equal(expected + "/signout-callback-oidc", query["post_logout_redirect_uri"].ToString());
        Assert.Equal(expected, properties.RedirectUri);
        Assert.Equal(expected, properties.Items[FrontendUrlResolver.FrontendPropertyKey]);

        using var callbackScope = services.CreateScope();
        var callback = CreateContext(callbackScope.ServiceProvider, "/signout-callback-oidc", "unknown.example");
        callback.Request.QueryString = QueryString.Create("state", query["state"].ToString());
        var handler = await callback.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>()
            .GetHandlerAsync(callback, OpenIdConnectDefaults.AuthenticationScheme);

        Assert.True(await Assert.IsAssignableFrom<IAuthenticationRequestHandler>(handler).HandleRequestAsync());
        Assert.Equal(expected, callback.Response.Headers.Location.ToString());
        Assert.Empty(backchannel.Requests);
    }

    [Theory]
    [InlineData("goride-demo.vercel.app", null, Demo)]
    [InlineData("goride-my-client.vercel.app", null, Primary)]
    [InlineData("unknown.example", null, Primary)]
    [InlineData("unknown.example", Demo, Demo)]
    [InlineData("goride-demo.vercel.app", "https://unknown.example", Demo)]
    public async Task RedirectEvents_UseSavedAllowedFrontendOrResolveRequest(string host, string? saved, string expected)
    {
        using var backchannel = new RecordingBackchannel();
        using var services = CreateServices(backchannel);
        using var scope = services.CreateScope();
        var context = CreateContext(scope.ServiceProvider, "/login", host);
        var properties = new AuthenticationProperties();
        properties.Items[FrontendUrlResolver.FrontendPropertyKey] = saved;
        var redirectContext = new RedirectContext(context,
            new AuthenticationScheme(OpenIdConnectDefaults.AuthenticationScheme, null, typeof(OpenIdConnectHandler)),
            new OpenIdConnectOptions(), properties)
        {
            ProtocolMessage = new OpenIdConnectMessage()
        };
        var events = scope.ServiceProvider.GetRequiredService<FrontendOpenIdConnectEvents>();

        await events.RedirectToIdentityProvider(redirectContext);
        await events.RedirectToIdentityProviderForSignOut(redirectContext);

        Assert.Equal(expected + "/signin-oidc", redirectContext.ProtocolMessage.RedirectUri);
        Assert.Equal(expected + "/signout-callback-oidc", redirectContext.ProtocolMessage.PostLogoutRedirectUri);
        Assert.Null(redirectContext.ProtocolMessage.Prompt);
    }

    private static ServiceProvider CreateServices(RecordingBackchannel backchannel, bool usePar = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Frontend:BaseUrl"] = Primary,
                ["Frontend:AllowedBaseUrls:0"] = Demo
            }).Build());
        services.AddScoped<FrontendUrlResolver>();
        services.AddScoped(provider => new FrontendOpenIdConnectEvents(provider.GetRequiredService<FrontendUrlResolver>())
        {
            // The fake token endpoint deliberately stops after recording code redemption.
            OnRemoteFailure = context =>
            {
                context.HandleResponse();
                return Task.CompletedTask;
            }
        });
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie()
            .AddOpenIdConnect(options =>
            {
                options.ClientId = "test-client";
                options.ResponseType = "code";
                options.UsePkce = true;
                options.EventsType = typeof(FrontendOpenIdConnectEvents);
                options.Configuration = new OpenIdConnectConfiguration
                {
                    Issuer = "https://identity-provider.example",
                    AuthorizationEndpoint = "https://identity-provider.example/authorize",
                    TokenEndpoint = "https://identity-provider.example/token",
                    EndSessionEndpoint = "https://identity-provider.example/logout",
                    PushedAuthorizationRequestEndpoint = usePar ? "https://identity-provider.example/par" : null
                };
                options.Backchannel = new HttpClient(backchannel);
            });
        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext CreateContext(IServiceProvider services, string path, string host)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("identity.example");
        context.Request.Method = "GET";
        context.Request.Path = path;
        context.Request.Headers["X-Forwarded-Host"] = host;
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        return context;
    }

    private sealed class RecordingBackchannel : HttpMessageHandler
    {
        public List<(string Path, Dictionary<string, StringValues> Parameters)> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add((path, QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken))));
            return path switch
            {
                "/par" => new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("{\"request_uri\":\"urn:test:request\",\"expires_in\":60}",
                        System.Text.Encoding.UTF8, "application/json")
                },
                "/token" => new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{\"error\":\"invalid_grant\"}", System.Text.Encoding.UTF8, "application/json")
                },
                _ => throw new InvalidOperationException("Unexpected backchannel request.")
            };
        }
    }
}
