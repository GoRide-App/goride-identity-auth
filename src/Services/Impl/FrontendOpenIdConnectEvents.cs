using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace SRC.Services.Impl;

public class FrontendOpenIdConnectEvents : OpenIdConnectEvents
{
    private readonly FrontendUrlResolver _frontendUrlResolver;

    public FrontendOpenIdConnectEvents(FrontendUrlResolver frontendUrlResolver)
    {
        _frontendUrlResolver = frontendUrlResolver;
    }

    public override Task RedirectToIdentityProvider(RedirectContext context)
    {
        var frontend = _frontendUrlResolver.Resolve(context.Request, context.Properties);
        context.ProtocolMessage.RedirectUri = $"{frontend}/signin-oidc";

        // After this event, OpenIdConnectHandler stores this URI in properties under
        // OpenIdConnectDefaults.RedirectUriForCodePropertiesKey, before protecting state
        // and sending PAR (if enabled). Code redemption reads that same stored URI.
        if (context.Properties.Items.TryGetValue("forceFresh", out var forceFresh) && forceFresh == "true")
        {
            context.ProtocolMessage.Prompt = "login";
        }

        return Task.CompletedTask;
    }

    public override Task RedirectToIdentityProviderForSignOut(RedirectContext context)
    {
        var frontend = _frontendUrlResolver.Resolve(context.Request, context.Properties);
        context.ProtocolMessage.PostLogoutRedirectUri = $"{frontend}/signout-callback-oidc";
        return Task.CompletedTask;
    }
}
