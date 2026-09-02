using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SRC.Data;
using SRC.Services.Interfaces;

namespace GoRide.IdentityAuth.Tests;

/// <summary>Records every request and answers with whatever the test supplies.</summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) => _responder = responder;

    public FakeHttpMessageHandler(HttpStatusCode status, string body = "", string mediaType = "application/json")
        : this(_ => Task.FromResult(Json(status, body, mediaType))) { }

    public List<HttpRequestMessage> Requests { get; } = new();
    public List<string> Bodies { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
        return await _responder(request);
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string body, string mediaType = "application/json") =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) };
}

internal sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;
    public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
}

internal sealed class FakeClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now = Now.Add(by);
}

internal sealed class FakeTokenProvider : IAsgardeoManagementTokenProvider
{
    public List<string> RequestedScopes { get; } = new();
    public string Token { get; set; } = "mgmt-token";

    public Task<string> GetTokenAsync(string scope, CancellationToken cancellationToken = default)
    {
        RequestedScopes.Add(scope);
        return Task.FromResult(Token);
    }
}

internal sealed class FakeIdentityAccountService : IIdentityAccountService
{
    public List<string> DisabledUserIds { get; } = new();
    public Exception? ThrowOnDisable { get; set; }

    public Task DisableAccountAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (ThrowOnDisable is not null) throw ThrowOnDisable;
        DisabledUserIds.Add(userId);
        return Task.CompletedTask;
    }
}

internal sealed class FakeActiveTripChecker : IActiveTripChecker
{
    public bool HasActiveTrip { get; set; }
    public Exception? Throw { get; set; }
    public int Calls { get; private set; }

    public Task<bool> HasActiveTripAsync(string userId, CancellationToken cancellationToken = default)
    {
        Calls++;
        if (Throw is not null) throw Throw;
        return Task.FromResult(HasActiveTrip);
    }
}

internal static class TestDb
{
    public static AppDbContext Create(string? name = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}

internal static class TestOptions
{
    public static IOptions<T> Of<T>(T value) where T : class => Options.Create(value);
    public static NullLogger<T> Logger<T>() => NullLogger<T>.Instance;
}
