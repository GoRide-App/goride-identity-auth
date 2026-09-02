using System.Net;
using GoRide.Api.Options;
using SRC.Services.Impl;
using SRC.Services.Interfaces;

namespace GoRide.IdentityAuth.Tests;

public class HttpActiveTripCheckerTests
{
    private static HttpActiveTripChecker CreateSut(FakeHttpMessageHandler handler, TripServiceOptions? options = null) =>
        new(new HttpClient(handler),
            TestOptions.Of(options ?? new TripServiceOptions { BaseUrl = "https://trips.internal", ApiKey = "k3y" }),
            TestOptions.Logger<HttpActiveTripChecker>());

    [Fact]
    public async Task Unconfigured_SkipsTheCheck_AndReportsNoActiveTrip()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{\"hasActiveTrip\":true}");
        var sut = CreateSut(handler, new TripServiceOptions { BaseUrl = "" });

        Assert.False(await sut.HasActiveTripAsync("u-1"));
        Assert.Empty(handler.Requests); // nothing was called
    }

    [Fact]
    public async Task CallsConfiguredPath_WithInternalApiKey()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{\"hasActiveTrip\":false}");
        var sut = CreateSut(handler);

        await sut.HasActiveTripAsync("user/1");

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://trips.internal/api/internal/users/user%2F1/active-trip", request.RequestUri!.ToString());
        Assert.Equal("k3y", Assert.Single(request.Headers.GetValues("X-Internal-Api-Key")));
    }

    [Theory]
    [InlineData("{\"hasActiveTrip\":true}", true)]
    [InlineData("{\"hasActiveTrip\":false}", false)]
    public async Task ReadsHasActiveTripFlag(string body, bool expected)
    {
        var sut = CreateSut(new FakeHttpMessageHandler(HttpStatusCode.OK, body));
        Assert.Equal(expected, await sut.HasActiveTripAsync("u-1"));
    }

    [Fact]
    public async Task NotFound_MeansNoTripsAtAll()
    {
        var sut = CreateSut(new FakeHttpMessageHandler(HttpStatusCode.NotFound, ""));
        Assert.False(await sut.HasActiveTripAsync("u-1"));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.Unauthorized, "")]
    [InlineData(HttpStatusCode.OK, "not json")]
    [InlineData(HttpStatusCode.OK, "{\"somethingElse\":1}")]
    public async Task UnusableAnswers_ThrowTripStatusUnavailable(HttpStatusCode status, string body)
    {
        var sut = CreateSut(new FakeHttpMessageHandler(status, body));
        await Assert.ThrowsAsync<TripStatusUnavailableException>(() => sut.HasActiveTripAsync("u-1"));
    }

    [Fact]
    public async Task NetworkFailure_ThrowsTripStatusUnavailable()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var sut = CreateSut(handler);
        var ex = await Assert.ThrowsAsync<TripStatusUnavailableException>(() => sut.HasActiveTripAsync("u-1"));
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }
}
