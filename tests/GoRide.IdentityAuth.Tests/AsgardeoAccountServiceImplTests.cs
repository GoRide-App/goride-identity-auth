using System.Net;
using System.Text.Json;
using GoRide.Api.Options;
using SRC.Services.Impl;

namespace GoRide.IdentityAuth.Tests;

public class AsgardeoAccountServiceImplTests
{
    private static readonly AsgardeoOptions Asgardeo = new()
    {
        BaseUrl = "https://api.asgardeo.io/t/goride",
        ClientId = "app",
        ClientSecret = "secret"
    };

    [Fact]
    public async Task DisableAccount_SendsScim2PatchSettingAccountDisabledTrue()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{\"id\":\"u-1\"}", "application/scim+json");
        var tokens = new FakeTokenProvider { Token = "abc123" };
        var sut = new AsgardeoAccountServiceImpl(new HttpClient(handler), tokens, TestOptions.Of(Asgardeo),
            TestOptions.Logger<AsgardeoAccountServiceImpl>());

        await sut.DisableAccountAsync("u-1");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal("https://api.asgardeo.io/t/goride/scim2/Users/u-1", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("abc123", request.Headers.Authorization!.Parameter);
        Assert.Equal("application/scim+json", request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal(new[] { AsgardeoAccountServiceImpl.UserUpdateScope }, tokens.RequestedScopes);

        using var body = JsonDocument.Parse(handler.Bodies[0]);
        var root = body.RootElement;
        Assert.Equal("urn:ietf:params:scim:api:messages:2.0:PatchOp", root.GetProperty("schemas")[0].GetString());
        var op = Assert.Single(root.GetProperty("Operations").EnumerateArray());
        Assert.Equal("replace", op.GetProperty("op").GetString());
        Assert.True(op.GetProperty("value").GetProperty("urn:scim:wso2:schema").GetProperty("accountDisabled").GetBoolean());
    }

    [Fact]
    public async Task DisableAccount_UrlEncodesTheUserId()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var sut = new AsgardeoAccountServiceImpl(new HttpClient(handler), new FakeTokenProvider(), TestOptions.Of(Asgardeo),
            TestOptions.Logger<AsgardeoAccountServiceImpl>());

        await sut.DisableAccountAsync("weird/id?x");

        Assert.EndsWith("/scim2/Users/weird%2Fid%3Fx", handler.Requests[0].RequestUri!.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task DisableAccount_ThrowsHttpRequestException_WhenAsgardeoRejects(HttpStatusCode status)
    {
        var handler = new FakeHttpMessageHandler(status, "{\"detail\":\"nope\"}");
        var sut = new AsgardeoAccountServiceImpl(new HttpClient(handler), new FakeTokenProvider(), TestOptions.Of(Asgardeo),
            TestOptions.Logger<AsgardeoAccountServiceImpl>());

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => sut.DisableAccountAsync("u-1"));

        Assert.Equal(status, ex.StatusCode);
        Assert.Contains("nope", ex.Message);
    }
}
