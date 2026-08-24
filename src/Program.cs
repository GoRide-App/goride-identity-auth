using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using SRC;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.


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
})
.AddOpenIdConnect(options =>
{
    options.Authority = "https://api.asgardeo.io/t/goride/oauth2/token";
    options.ClientId = builder.Configuration["Asgardeo:ClientId"];
    options.ClientSecret = builder.Configuration["Asgardeo:ClientSecret"];
    options.ResponseType = "code";
    options.UsePkce = true;
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;
    

    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("email");
    options.Scope.Add("profile");
    options.Scope.Add("roles");

    options.CallbackPath = "/signin-oidc"; // must exactly match what you registered in Step 1
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
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});


builder.Services.AddControllers();
builder.Services.AddOpenApi();

ServiceExtentions.AddApplicationServices(builder.Services);

//==============================================================================================================================
var app = builder.Build();

// Configure the HTTP request pipeline.


app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/login", (string? returnUrl, string? prompt) =>
{
    var properties = new AuthenticationProperties { RedirectUri = returnUrl ?? "http://localhost:3000" };
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
            new AuthenticationProperties { RedirectUri = "http://localhost:3000" },
            [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]
        ));


app.MapGet("/api/me", (ClaimsPrincipal user) =>
{
    if (!user.Identity!.IsAuthenticated) return Results.Unauthorized();


    return Results.Ok(new
    {
        name = user.FindFirstValue("name"),
        email = user.FindFirstValue("email"),
        roles = user.FindAll("roles").Select(c => c.Value),
    });
}).RequireAuthorization();

app.MapGet("/api/admin-check", () => Results.Ok(new { message = "You're an admin" }))
   .RequireAuthorization(policy => policy.RequireRole("Admin"));



app.UseHttpsRedirection();



app.MapControllers();


app.Run();
