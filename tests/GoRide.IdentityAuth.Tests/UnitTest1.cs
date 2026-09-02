using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SRC;
using SRC.Controllers;
using SRC.Dtos;
using SRC.Entities;
using SRC.Enums;
using SRC.Services.Impl;
using SRC.Services.Interfaces;

namespace GoRide.IdentityAuth.Tests;

public class ControllerEndpointTests
{
    [Fact]
    public async Task AddProfile_WhenUserHasSubAndServiceReturnsProfile_ReturnsOk()
    {
        var request = CreateDriverProfileRequest();
        var expected = CreateDriverProfile();
        var service = new FakeDriverProfileService { AddProfileResult = expected };
        var controller = CreateDriverController(service, "driver-123");

        var result = await controller.AddProfile(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
        Assert.Equal("driver-123", service.LastAddedSub);
    }

    [Fact]
    public async Task AddProfile_WhenSubClaimMissing_ReturnsUnauthorized()
    {
        var controller = CreateDriverController(new FakeDriverProfileService());

        var result = await controller.AddProfile(CreateDriverProfileRequest());

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task AddProfile_WhenServiceReturnsNull_ReturnsBadRequest()
    {
        var service = new FakeDriverProfileService { AddProfileResult = null! };
        var controller = CreateDriverController(service, "driver-123");

        var result = await controller.AddProfile(CreateDriverProfileRequest());

        Assert.IsType<BadRequestResult>(result.Result);
    }

    [Fact]
    public async Task GetVehicleById_WhenVehicleExists_ReturnsOk()
    {
        var expected = CreateVehicleDto();
        var service = new FakeDriverProfileService { VehicleResult = expected };
        var controller = CreateDriverController(service);

        var result = await controller.GetVehicleById("driver-123");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
    }

    [Fact]
    public async Task GetVehicleById_WhenVehicleDoesNotExist_ReturnsNotFound()
    {
        var controller = CreateDriverController(new FakeDriverProfileService { VehicleResult = null });

        var result = await controller.GetVehicleById("missing-driver");

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateVehicle_WhenCallerIsAuthorizedAndServiceReturnsProfile_ReturnsOk()
    {
        var request = CreateUpdateVehicleDto();
        var expected = CreateDriverProfile();
        var service = new FakeDriverProfileService { UpdateVehicleResult = expected };
        var controller = CreateDriverController(service, "driver-456", isAdmin: true);

        var result = await controller.UpdateVehicle("driver-123", request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
        Assert.Equal("driver-123", service.LastDriverSub);
        Assert.Equal("driver-456", service.LastUsrSub);
        Assert.True(service.LastIsAdmin);
    }

    [Fact]
    public async Task UpdateVehicle_WhenSubClaimMissing_ReturnsUnauthorized()
    {
        var controller = CreateDriverController(new FakeDriverProfileService());

        var result = await controller.UpdateVehicle("driver-123", CreateUpdateVehicleDto());

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task UpdateVehicle_WhenServiceReturnsNull_ReturnsNotFound()
    {
        var service = new FakeDriverProfileService { UpdateVehicleResult = null };
        var controller = CreateDriverController(service, "driver-456");

        var result = await controller.UpdateVehicle("driver-123", CreateUpdateVehicleDto());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task InternalUserController_GetUserBySub_WhenApiKeyValid_ReturnsJsonContent()
    {
        var service = new FakeUserDirectoryService { JsonResponse = "{\"user\":\"abc123\"}" };
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["InternalServices:ApiKey"] = "super-secret"
        });
        var controller = new InternalUserController(service, config)
        {
            ControllerContext = CreateControllerContext(principal: CreatePrincipal("user-123"), accessToken: null)
        };

        var result = await controller.GetUserBySub("user-123", "super-secret");

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal("application/json", content.ContentType);
        Assert.Equal("{\"user\":\"abc123\"}", content.Content);
        Assert.Equal("user-123", service.LastUserId);
    }

    [Fact]
    public async Task InternalUserController_GetUserBySub_WhenApiKeyInvalid_ReturnsUnauthorized()
    {
        var controller = new InternalUserController(new FakeUserDirectoryService(), CreateConfiguration(new Dictionary<string, string?>
        {
            ["InternalServices:ApiKey"] = "super-secret"
        }))
        {
            ControllerContext = CreateControllerContext(principal: CreatePrincipal("user-123"), accessToken: null)
        };

        var result = await controller.GetUserBySub("user-123", "wrong-key");

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task ProfileController_GetProfile_WhenAccessTokenPresent_ReturnsJsonContent()
    {
        var service = new FakeProfileService { ProfileJson = "{\"name\":\"Ada\"}" };
        var controller = new ProfileController(service)
        {
            ControllerContext = CreateControllerContext(accessToken: "abc-token")
        };

        var result = await controller.GetProfile();

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal("application/json", content.ContentType);
        Assert.Equal("{\"name\":\"Ada\"}", content.Content);
        Assert.Equal("abc-token", service.LastAccessToken);
    }

    [Fact]
    public async Task ProfileController_GetProfile_WhenAccessTokenMissing_ReturnsUnauthorized()
    {
        var controller = new ProfileController(new FakeProfileService())
        {
            ControllerContext = CreateControllerContext(accessToken: null)
        };

        var result = await controller.GetProfile();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task ProfileController_UpdateProfile_WhenAccessTokenPresent_UpdatesAndReturnsPhoneNumber()
    {
        var service = new FakeProfileService();
        var controller = new ProfileController(service)
        {
            ControllerContext = CreateControllerContext(accessToken: "abc-token")
        };
        var request = new ProfileUpdateRequest("Ada", "Lovelace", "+15551234567");

        var result = await controller.UpdateProfile(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("+15551234567", ok.Value?.GetType().GetProperty("phoneNumber")?.GetValue(ok.Value));
        Assert.Equal("abc-token", service.LastAccessToken);
        Assert.Equal(request, service.LastRequest);
    }

    [Fact]
    public async Task ProfileController_UpdateProfile_WhenAccessTokenMissing_ReturnsUnauthorized()
    {
        var controller = new ProfileController(new FakeProfileService())
        {
            ControllerContext = CreateControllerContext(accessToken: null)
        };

        var result = await controller.UpdateProfile(new ProfileUpdateRequest("Ada", "Lovelace", "+1"));

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task RegistrationController_SelectRole_WhenRoleValid_AssignsRoleAndReturnsOk()
    {
        var roleService = new FakeAsgardeoRoleService();
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["AsgardeoRoles:DriverRoleId"] = "driver-role-id",
            ["AsgardeoRoles:RiderRoleId"] = "rider-role-id"
        });
        var principal = CreatePrincipal("asgardeo-user-123", new[]
        {
            new Claim("email", "driver@example.com"),
            new Claim("name", "Driver User")
        });
        var controller = new RegistrationController(roleService, config)
        {
            ControllerContext = CreateControllerContext(principal: principal, accessToken: null)
        };

        var result = await controller.SelectRole(new RoleSelectionRequest("Driver"));

        Assert.IsType<OkResult>(result);
        Assert.Equal("asgardeo-user-123", roleService.LastAsgardeoUserId);
        Assert.Equal("asgardeo-user-123", roleService.LastDisplayName);
        Assert.Equal("driver-role-id", roleService.LastRoleId);
    }

    [Fact]
    public async Task RegistrationController_SelectRole_WhenSubClaimMissing_ReturnsUnauthorized()
    {
        var controller = new RegistrationController(new FakeAsgardeoRoleService(), CreateConfiguration(new Dictionary<string, string?>
        {
            ["AsgardeoRoles:DriverRoleId"] = "driver-role-id"
        }))
        {
            ControllerContext = CreateControllerContext(principal: new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("email", "driver@example.com") }, "TestAuth")), accessToken: null)
        };

        var result = await controller.SelectRole(new RoleSelectionRequest("Driver"));

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task RegistrationController_SelectRole_WhenRoleIsInvalid_ReturnsBadRequest()
    {
        var controller = new RegistrationController(new FakeAsgardeoRoleService(), CreateConfiguration(new Dictionary<string, string?>
        {
            ["AsgardeoRoles:DriverRoleId"] = "driver-role-id"
        }))
        {
            ControllerContext = CreateControllerContext(principal: CreatePrincipal("asgardeo-user-123", new[] { new Claim("email", "driver@example.com") }), accessToken: null)
        };

        var result = await controller.SelectRole(new RoleSelectionRequest("UnknownRole"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid role", badRequest.Value);
    }

    [Fact]
    public void RegistrationController_DebugClaims_ReturnsClaimsPayload()
    {
        var principal = CreatePrincipal("asgardeo-user-123", new[]
        {
            new Claim("sub", "asgardeo-user-123"),
            new Claim("email", "driver@example.com")
        });
        var controller = new RegistrationController(new FakeAsgardeoRoleService(), CreateConfiguration())
        {
            ControllerContext = CreateControllerContext(principal: principal, accessToken: null)
        };

        var result = controller.DebugClaims();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public void RoleSpecificTestController_DriverOnlyAction_ReturnsOk()
    {
        var controller = new RoleSpecificTestController();

        var result = controller.DriverOnlyAction();

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public void WeatherForecastController_Get_ReturnsFiveForecasts()
    {
        var controller = new WeatherForecastController();

        var result = controller.Get().ToList();

        Assert.Equal(5, result.Count);
        Assert.All(result, item => Assert.NotNull(item.Summary));
        Assert.All(result, item => Assert.InRange(item.TemperatureF, 0, 1000));
    }

    private static DriverProfileController CreateDriverController(IDriverProfileService service, string? sub = null, bool isAdmin = false)
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    sub is not null ? new Claim("sub", sub) : null,
                    isAdmin ? new Claim(ClaimTypes.Role, "Admin") : null
                }.OfType<Claim>(),
                "TestAuth"));

        return new DriverProfileController(service)
        {
            ControllerContext = CreateControllerContext(principal: principal, accessToken: null)
        };
    }

    private static ControllerContext CreateControllerContext(ClaimsPrincipal? principal = null, string? accessToken = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = principal ?? new ClaimsPrincipal(new ClaimsIdentity());

        var authService = new StubAuthenticationService(accessToken, httpContext.User);
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(authService);
        httpContext.RequestServices = services.BuildServiceProvider();

        return new ControllerContext { HttpContext = httpContext };
    }

    private static ClaimsPrincipal CreatePrincipal(string? sub = null, IEnumerable<Claim>? extraClaims = null)
    {
        var claims = new List<Claim>();
        if (sub is not null)
            claims.Add(new Claim("sub", sub));

        if (extraClaims is not null)
            claims.AddRange(extraClaims);

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?>? values = null)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        values ??= new Dictionary<string, string?>();
        foreach (var item in values)
        {
            data[item.Key] = item.Value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    private static CreateDriverProfileRequestDto CreateDriverProfileRequest() => new()
    {
        VehicleMake = "Toyota",
        VehicleModel = "Corolla",
        VehiclePlate = "ABC123",
        VehicleTypeCode = "CAR",
        LicenseNumber = "LIC-001",
        LicenseExpiry = new DateOnly(2030, 5, 15)
    };

    private static UpdateVehicleDto CreateUpdateVehicleDto() => new()
    {
        VehicleMake = "Honda",
        VehicleModel = "Civic",
        VehiclePlate = "XYZ789",
        VehicleTypeCode = "CAR",
        LicenseNumber = "LIC-002",
        LicenseExpiry = new DateOnly(2031, 7, 20),
        Status = DriverStatus.Active
    };

    private static DriverProfile CreateDriverProfile() => new()
    {
        DriverId = "driver-123",
        VehicleMake = "Toyota",
        VehicleModel = "Corolla",
        VehiclePlate = "ABC123",
        VehicleTypeCode = "CAR",
        LicenseNumber = "LIC-001",
        LicenseExpiry = new DateOnly(2030, 5, 15),
        Status = DriverStatus.Active,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static VehicleDto CreateVehicleDto() => new()
    {
        VehicleMake = "Toyota",
        VehicleModel = "Corolla",
        VehiclePlate = "ABC123",
        VehicleTypeCode = "CAR",
        LicenseNumber = "LIC-001",
        LicenseExpiry = new DateOnly(2030, 5, 15)
    };

    private sealed class FakeDriverProfileService : IDriverProfileService
    {
        public DriverProfile? AddProfileResult { get; set; }
        public VehicleDto? VehicleResult { get; set; }
        public DriverProfile? UpdateVehicleResult { get; set; }
        public string? LastAddedSub { get; private set; }
        public string? LastDriverSub { get; private set; }
        public string? LastUsrSub { get; private set; }
        public bool LastIsAdmin { get; private set; }

        public Task<DriverProfile> AddProfile(string sub, CreateDriverProfileRequestDto request)
        {
            LastAddedSub = sub;
            return Task.FromResult(AddProfileResult!);
        }

        public Task<VehicleDto?> GetVehicleById(string sub)
        {
            return Task.FromResult(VehicleResult);
        }

        public Task<DriverProfile?> UpdateVehicle(string driverSub, string usrSub, UpdateVehicleDto request, bool isAdmin)
        {
            LastDriverSub = driverSub;
            LastUsrSub = usrSub;
            LastIsAdmin = isAdmin;
            return Task.FromResult(UpdateVehicleResult);
        }
    }

    private sealed class FakeUserDirectoryService : IUserDirectoryService
    {
        public string JsonResponse { get; set; } = "{}";
        public string? LastUserId { get; private set; }

        public Task<string> GetUserByIdAsync(string userId)
        {
            LastUserId = userId;
            return Task.FromResult(JsonResponse);
        }
    }

    private sealed class FakeProfileService : IProfileService
    {
        public string ProfileJson { get; set; } = "{}";
        public string? LastAccessToken { get; private set; }
        public ProfileUpdateRequest? LastRequest { get; private set; }

        public Task<string> GetProfileAsync(string accessToken)
        {
            LastAccessToken = accessToken;
            return Task.FromResult(ProfileJson);
        }

        public Task UpdateProfileAsync(string accessToken, ProfileUpdateRequest req)
        {
            LastAccessToken = accessToken;
            LastRequest = req;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAsgardeoRoleService : IAsgardeoRoleService
    {
        public string? LastAsgardeoUserId { get; private set; }
        public string? LastDisplayName { get; private set; }
        public string? LastRoleId { get; private set; }

        public Task AssignRoleAsync(string asgardeoUserId, string displayName, string roleId)
        {
            LastAsgardeoUserId = asgardeoUserId;
            LastDisplayName = displayName;
            LastRoleId = roleId;
            return Task.CompletedTask;
        }
    }

    private sealed class StubAuthenticationService(string? accessToken, ClaimsPrincipal principal) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            var properties = new AuthenticationProperties();
            if (accessToken is not null)
            {
                properties.StoreTokens(
                [
                    new AuthenticationToken { Name = "access_token", Value = accessToken }
                ]);
            }

            var ticket = new AuthenticationTicket(principal, properties, scheme ?? "TestAuth");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
