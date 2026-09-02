using System.Net;
using GoRide.Api.Options;
using SRC.Services.Impl;

namespace GoRide.IdentityAuth.Tests;

public class AsgardeoManagementTokenProviderTests
{
    private static readonly AsgardeoOptions Asgardeo = new()
    {
        BaseUrl = "https://api.asgardeo.io/t/goride",
        ClientId = "app",
        ClientSecret = "secret"
    };
    private static readonly AsgardeoMgmtOptions Mgmt = new() { ClientId = "m2m-id", ClientSecret = "m2m-secret" };

    private static AsgardeoManagementTokenProvider CreateSut(FakeHttpMessageHandler handler, FakeClock clock,
        AsgardeoMgmtOptions? mgmt = null) =>
        new(new FakeHttpClientFactory(handler), TestOptions.Of(Asgardeo), TestOptions.Of(mgmt ?? Mgmt),
            TestOptions.Logger<AsgardeoManagementTokenProvider>(), clock);

    private static FakeHttpMessageHandler TokenServer(int expiresIn = 3600)
    {
        var counter = 0;
        return new FakeHttpMessageHandler(_ =>
        {
            counter++;
            return Task.FromResult(FakeHttpMessageHandler.Json(HttpStatusCode.OK,
                $"{{\"access_token\":\"tok-{counter}\",\"token_type\":\"Bearer\",\"expires_in\":{expiresIn},\"scope\":\"internal_user_mgt_update\"}}"));
        });
    }

    [Fact]
    public async Task RequestsClientCredentials_WithBasicAuth_AtTheTenantTokenEndpoint()
    {
        var handler = TokenServer();
        var sut = CreateSut(handler, new FakeClock());

        var token = await sut.GetTokenAsync("internal_user_mgt_update");

        Assert.Equal("tok-1", token);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.asgardeo.io/t/goride/oauth2/token", request.RequestUri!.ToString());
        Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
        Assert.Equal(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("m2m-id:m2m-secret")),
            request.Headers.Authorization!.Parameter);
        Assert.Contains("grant_type=client_credentials", handler.Bodies[0]);
        Assert.Contains("scope=internal_user_mgt_update", handler.Bodies[0]);
    }

    [Fact]
    public async Task CachesTokenPerScope_UntilShortlyBeforeExpiry()
    {
        var handler = TokenServer(expiresIn: 600);
        var clock = new FakeClock();
        var sut = CreateSut(handler, clock);

        var first = await sut.GetTokenAsync("scope-a");
        var again = await sut.GetTokenAsync("scope-a");
        var other = await sut.GetTokenAsync("scope-b");

        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
        Assert.Equal(2, handler.Requests.Count);

        clock.Advance(TimeSpan.FromSeconds(600 - 30)); // inside the 60s safety margin => refresh
        var refreshed = await sut.GetTokenAsync("scope-a");

        Assert.NotEqual(first, refreshed);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task MissingCredentials_FailFast_WithoutCallingAsgardeo()
    {
        var handler = TokenServer();
        var sut = CreateSut(handler, new FakeClock(), new AsgardeoMgmtOptions { ClientId = "", ClientSecret = "" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetTokenAsync("x"));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task TokenEndpointError_SurfacesAsHttpRequestException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.Unauthorized, "{\"error\":\"invalid_client\"}");
        var sut = CreateSut(handler, new FakeClock());

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetTokenAsync("x"));
        Assert.Contains("invalid_client", ex.Message);
    }
}
