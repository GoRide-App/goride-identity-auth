using System.Security.Claims;
using System.Text.Json;
using GoRide.Api.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SRC;
using SRC.Data;
using SRC.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------

builder.Services
    .AddOptions<AsgardeoOptions>()
    .Bind(builder.Configuration.GetSection("Asgardeo"))
    .ValidateDataAnnotations()
    .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Asgardeo:BaseUrl is required")
    .Validate(o => !string.IsNullOrWhiteSpace(o.ClientId), "Asgardeo:ClientId is required")
    .Validate(o => !string.IsNullOrWhiteSpace(o.ClientSecret), "Asgardeo:ClientSecret is required")
    .ValidateOnStart();

builder.Services
    .AddOptions<AsgardeoMgmtOptions>()
    .Bind(builder.Configuration.GetSection("AsgardeoMgmt"))
    .ValidateOnStart();

builder.Services
    .AddOptions<AsgardeoRolesOptions>()
    .Bind(builder.Configuration.GetSection("AsgardeoRoles"))
    .ValidateOnStart();

builder.Services
    .AddOptions<TripServiceOptions>()
    .Bind(builder.Configuration.GetSection("TripService"));

builder.Services
    .AddOptions<FrontendOptions>()
    .Bind(builder.Configuration.GetSection("Frontend"))
    .Validate(o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _), "Frontend:BaseUrl must be an absolute URL")
    .ValidateOnStart();

// Read once for the pieces of the OIDC handler and CORS that must be configured up front.
var asgardeo = builder.Configuration.GetSection("Asgardeo").Get<AsgardeoOptions>()
               ?? throw new InvalidOperationException("Asgardeo configuration section is missing");
var frontend = builder.Configuration.GetSection("Frontend").Get<FrontendOptions>() ?? new FrontendOptions();
var frontendBaseUrl = frontend.BaseUrl.TrimEnd('/');

// ---------------------------------------------------------------------------
// Authentication
// ---------------------------------------------------------------------------

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.Name = "app_session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None; // frontend and backend are different origins in dev
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;

    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };

    options.Events.OnValidatePrincipal = async context =>
    {
        var services = context.HttpContext.RequestServices;

        // SCRUM-35: a session that outlived its account's deactivation is dropped immediately,
        // without waiting for the access token to expire and the refresh to be refused.
        var sub = context.Principal?.FindFirstValue("sub");
        if (sub is not null && await IsLocallyDeactivatedAsync(services, sub, context.HttpContext.RequestAborted))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        var expiresAt = context.Properties.GetTokens()
            .FirstOrDefault(t => t.Name == "expires_at")?.Value;

        if (expiresAt is null || DateTimeOffset.Parse(expiresAt) > DateTimeOffset.UtcNow.AddMinutes(2))
            return; // token still valid, nothing to do

        var refreshToken = context.Properties.GetTokens()
            .FirstOrDefault(t => t.Name == "refresh_token")?.Value;

        if (refreshToken is null)
        {
            context.RejectPrincipal(); // no refresh token — force re-login
            return;
        }

        var asgardeoOptions = services.GetRequiredService<IOptions<AsgardeoOptions>>().Value;
        var http = services.GetRequiredService<IHttpClientFactory>().CreateClient();

        var response = await http.PostAsync(
            asgardeoOptions.TokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = asgardeoOptions.ClientId,
                ["client_secret"] = asgardeoOptions.ClientSecret
            }));

        if (!response.IsSuccessStatusCode)
        {
            context.RejectPrincipal(); // refresh failed (revoked, expired or account disabled) — force re-login
            return;
        }

        var tokens = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Important: WSO2 rotates refresh tokens — always persist the NEW one
        context.Properties.UpdateTokenValue("access_token", tokens.GetProperty("access_token").GetString()!);
        context.Properties.UpdateTokenValue("refresh_token", tokens.GetProperty("refresh_token").GetString()!);
        context.Properties.UpdateTokenValue("expires_at",
            DateTimeOffset.UtcNow.AddSeconds(tokens.GetProperty("expires_in").GetInt32()).ToString("o"));

        context.ShouldRenew = true; // re-issues the cookie with updated properties
    };
})
.AddOpenIdConnect(options =>
{
    // Asgardeo publishes its discovery document under the token endpoint:
    // {BaseUrl}/oauth2/token/.well-known/openid-configuration
    options.Authority = asgardeo.TokenEndpoint;
    options.ClientId = asgardeo.ClientId;
    options.ClientSecret = asgardeo.ClientSecret;
    options.ResponseType = "code";
    options.UsePkce = true;
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;

    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("email");
    options.Scope.Add("profile");
    options.Scope.Add("roles");
    options.Scope.Add("offline_access");
    options.Scope.Add("internal_login");
    options.Scope.Add("phone");

    options.CallbackPath = "/signin-oidc"; // must exactly match the redirect URI registered in Asgardeo
    options.SignedOutCallbackPath = "/signout-callback-oidc";

    options.MapInboundClaims = false;
    options.ClaimActions.MapUniqueJsonKey("roles", "roles");

    options.TokenValidationParameters.NameClaimType = "name";
    options.TokenValidationParameters.RoleClaimType = "roles"; // Asgardeo's claim name — without this, [Authorize(Roles=...)] never matches

    options.Events.OnRedirectToIdentityProvider = context =>
    {
        if (context.Properties.Items.TryGetValue("forceFresh", out var forceFresh) && forceFresh == "true")
        {
            context.ProtocolMessage.Prompt = "login";
        }
        return Task.CompletedTask;
    };

    // SCRUM-35 scenario 2: the Identity Server refuses disabled accounts itself; this is the
    // local backstop so a deactivated profile can never be signed in even if the IdP flag lags.
    options.Events.OnTokenValidated = async context =>
    {
        var sub = context.Principal?.FindFirstValue("sub");
        if (sub is not null && await IsLocallyDeactivatedAsync(context.HttpContext.RequestServices, sub, context.HttpContext.RequestAborted))
        {
            context.Fail("account_deactivated");
        }
    };

    options.Events.OnRemoteFailure = context =>
    {
        var reason = context.Failure?.Message == "account_deactivated" ? "account_deactivated" : "login_failed";
        context.Response.Redirect($"{frontendBaseUrl}/?error={reason}");
        context.HandleResponse();
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    // Named policies for role checks you'll reuse a lot
    options.AddPolicy("RequireAdmin", p => p.RequireRole("Admin"));
    options.AddPolicy("RequireDriver", p => p.RequireRole("Driver"));
    options.AddPolicy("RequireRider", p => p.RequireRole("Rider"));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
        policy.WithOrigins(frontendBaseUrl)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------

builder.Services.AddControllers();
builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required");
var serverVersion = ServerVersion.AutoDetect(connectionString);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

builder.Services.AddOpenApi();

ServiceExtentions.AddApplicationServices(builder.Services);

//==============================================================================================================================
var app = builder.Build();

// Configure the HTTP request pipeline.

// Unhandled exceptions become RFC 7807 problem responses instead of leaking stack traces.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    // Only meaningful locally: the container listens on plain HTTP behind the
    // Container Apps ingress, which terminates TLS itself.
    app.UseHttpsRedirection();
}

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/login", (string? returnUrl, string? prompt) =>
{
    var properties = new AuthenticationProperties { RedirectUri = returnUrl ?? frontendBaseUrl };
    if (prompt == "login")
    {
        properties.Items["forceFresh"] = "true";
    }
    return Results.Challenge(properties, [OpenIdConnectDefaults.AuthenticationScheme]);
}).AllowAnonymous();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous(); // so you can still browse API docs
}

app.MapGet("/logout", () =>
        Results.SignOut(
            new AuthenticationProperties { RedirectUri = frontendBaseUrl },
            [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]
        ));

app.MapGet("/api/me", (ClaimsPrincipal user) =>
{
    if (!user.Identity!.IsAuthenticated) return Results.Unauthorized();

    return Results.Ok(new
    {
        userId = user.FindFirstValue("sub"),
        name = user.FindFirstValue("username"),
        email = user.FindFirstValue("email"),
        phone_number = user.FindFirstValue("phone_number"),
        roles = user.FindAll("roles").Select(c => c.Value),
    });
}).RequireAuthorization();

app.MapGet("/api/admin-check", () => Results.Ok(new { message = "You're an admin" }))
   .RequireAuthorization(policy => policy.RequireRole("Admin"));

app.MapControllers();

app.Run();

// Looks up the local soft-delete flag. The Identity Server remains the authority, so a
// database outage logs an error and lets the request through rather than locking everyone out.
static async Task<bool> IsLocallyDeactivatedAsync(IServiceProvider services, string userId, CancellationToken cancellationToken)
{
    try
    {
        var accounts = services.GetRequiredService<IAccountDeactivationService>();
        return await accounts.IsDeactivatedAsync(userId, cancellationToken);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AccountStatus")
            .LogError(ex, "Could not read local account status for {UserId}; treating as active.", userId);
        return false;
    }
}
