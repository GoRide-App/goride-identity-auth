using SRC.Services.Impl;
using SRC.Services.Interfaces;

namespace SRC;

public static class ServiceExtentions
{
    public static IServiceCollection AddApplicationServices(IServiceCollection services)
    {
        // One cached client-credentials token per scope, shared by every SCIM2 management call.
        services.AddHttpClient(AsgardeoManagementTokenProvider.HttpClientName);
        services.AddSingleton<IAsgardeoManagementTokenProvider, AsgardeoManagementTokenProvider>();

        services.AddHttpClient<IAsgardeoRoleService, AsgardeoRoleServiceImpl>();
        services.AddHttpClient<IProfileService, ProfileServiceImpl>();
        services.AddHttpClient<IUserDirectoryService, UserDirectoryServiceImpl>();
        services.AddHttpClient<IIdentityAccountService, AsgardeoAccountServiceImpl>();
        services.AddHttpClient<IActiveTripChecker, HttpActiveTripChecker>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5); // a slow trip service must not hang the request
        });

        services.AddScoped<IDriverProfileService, DriverProfileServiceImpl>();
        services.AddScoped<IAccountDeactivationService, AccountDeactivationService>();
        return services;
    }
}
