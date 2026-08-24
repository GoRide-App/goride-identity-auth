using SRC.Services.Impl;
using SRC.Services.Interfaces;

namespace SRC;

public static class ServiceExtentions
{
    public static IServiceCollection AddApplicationServices(IServiceCollection services)
    {
        services.AddHttpClient<IAsgardeoRoleService, AsgardeoRoleServiceImpl>();
        return services;
    }
}