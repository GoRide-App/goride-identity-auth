using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SRC.Controllers;
using SRC.Dtos;
using SRC.Services.Interfaces;

namespace GoRide.IdentityAuth.Tests;

public class AccountControllerTests
{
    private const string UserId = "user-123";

    private sealed class StubDeactivationService : IAccountDeactivationService
    {
        public DeactivationResult Result { get; set; } = DeactivationResult.Deactivated(new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc));
        public List<string> Calls { get; } = new();

        public Task<DeactivationResult> DeactivateAsync(string userId, CancellationToken cancellationToken = default)
        {
            Calls.Add(userId);
            return Task.FromResult(Result);
        }

        public Task<bool> IsDeactivatedAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class RecordingAuthenticationService : IAuthenticationService
    {
        public List<string?> SignedOutSchemes { get; } = new();

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignedOutSchemes.Add(scheme);
            return Task.CompletedTask;
        }
    }

    private static (AccountController controller, RecordingAuthenticationService auth) CreateController(
        IAccountDeactivationService service, bool authenticated = true)
    {
        var auth = new RecordingAuthenticationService();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers(); // brings ProblemDetailsFactory used by ControllerBase.Problem()
        services.AddSingleton<IAuthenticationService>(auth);

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        if (authenticated)
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", UserId) }, "cookie"));

        var controller = new AccountController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
        return (controller, auth);
    }

    [Fact]
    public async Task WithoutConfirm_Returns400_AndNeverCallsTheService()
    {
        var service = new StubDeactivationService();
        var (controller, auth) = CreateController(service);

        var result = await controller.Deactivate(new DeactivateAccountRequestDto { Confirm = false }, default);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Empty(service.Calls);
        Assert.Empty(auth.SignedOutSchemes);
    }

    [Fact]
    public async Task WithoutSubClaim_Returns401()
    {
        var (controller, _) = CreateController(new StubDeactivationService(), authenticated: false);

        var result = await controller.Deactivate(new DeactivateAccountRequestDto { Confirm = true }, default);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Success_Returns200_AndEndsTheCookieSession()
    {
        var service = new StubDeactivationService();
        var (controller, auth) = CreateController(service);

        var result = await controller.Deactivate(new DeactivateAccountRequestDto { Confirm = true }, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<DeactivateAccountResponse>(ok.Value);
        Assert.Equal("deactivated", body.Status);
        Assert.False(body.AlreadyDeactivated);
        Assert.Equal(service.Result.DeactivatedAt, body.DeactivatedAt);
        Assert.Equal(new[] { UserId }, service.Calls);
        Assert.Equal(new[] { CookieAuthenticationDefaults.AuthenticationScheme }, auth.SignedOutSchemes);
    }

    [Fact]
    public async Task ActiveTrip_Returns409_WithExplanatoryMessage_AndKeepsTheSession()
    {
        var service = new StubDeactivationService { Result = DeactivationResult.BlockedByActiveTrip() };
        var (controller, auth) = CreateController(service);

        var result = await controller.Deactivate(new DeactivateAccountRequestDto { Confirm = true }, default);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Contains("trip", problem.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(auth.SignedOutSchemes);
    }

    [Fact]
    public async Task TripStatusUnavailable_Returns503()
    {
        var service = new StubDeactivationService { Result = DeactivationResult.TripStatusUnavailable("try later") };
        var (controller, _) = CreateController(service);

        var result = await controller.Deactivate(new DeactivateAccountRequestDto { Confirm = true }, default);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task IdentityServerRejected_Returns502()
    {
        var service = new StubDeactivationService { Result = DeactivationResult.IdentityServerRejected("idp down") };
        var (controller, auth) = CreateController(service);

        var result = await controller.Deactivate(new DeactivateAccountRequestDto { Confirm = true }, default);

        Assert.Equal(StatusCodes.Status502BadGateway, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Empty(auth.SignedOutSchemes);
    }
}
