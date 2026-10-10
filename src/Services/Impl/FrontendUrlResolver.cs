using Microsoft.AspNetCore.Authentication;

namespace SRC.Services.Impl;

public class FrontendUrlResolver
{
    public const string FrontendPropertyKey = "frontend";
    private static readonly object ResolvedFrontendKey = new();
    private readonly string _baseUrl;
    private readonly HashSet<string> _allowedBaseUrls = new(StringComparer.OrdinalIgnoreCase);

    public FrontendUrlResolver(IConfiguration configuration)
    {
        _baseUrl = GetOrigin(configuration["Frontend:BaseUrl"])
            ?? throw new InvalidOperationException("Frontend:BaseUrl must be an absolute HTTP or HTTPS URL.");
        _allowedBaseUrls.Add(_baseUrl);

        foreach (var entry in configuration.GetSection("Frontend:AllowedBaseUrls").GetChildren())
        {
            var origin = GetOrigin(entry.Value);
            if (origin is not null)
            {
                _allowedBaseUrls.Add(origin);
            }
        }
    }

    public string Resolve(HttpRequest request, AuthenticationProperties? properties = null)
    {
        if (properties is not null && properties.Items.TryGetValue(FrontendPropertyKey, out var savedFrontend)
            && GetAllowedOrigin(savedFrontend) is { } savedOrigin)
        {
            return savedOrigin;
        }

        if (request.HttpContext.Items.TryGetValue(ResolvedFrontendKey, out var resolvedFrontend))
        {
            return (string)resolvedFrontend!;
        }

        var host = FirstValue(request.Headers["X-Forwarded-Host"].ToString());
        var proto = FirstValue(request.Headers["X-Forwarded-Proto"].ToString());
        string? forwardedOrigin = null;
        if (!string.IsNullOrEmpty(host) && host.IndexOfAny(['/', '\\', '?', '#', '@']) < 0
            && (string.Equals(proto, "https", StringComparison.OrdinalIgnoreCase)
                || string.Equals(proto, "http", StringComparison.OrdinalIgnoreCase)))
        {
            forwardedOrigin = GetAllowedOrigin($"{proto}://{host}");
        }

        var frontend = forwardedOrigin
            ?? GetAllowedOrigin(request.Headers.Origin.ToString())
            ?? GetAllowedOrigin(request.Headers.Referer.ToString())
            ?? _baseUrl;

        request.HttpContext.Items[ResolvedFrontendKey] = frontend;
        return frontend;
    }

    public AuthenticationProperties CreateProperties(HttpRequest request, string? returnUrl = null)
    {
        var frontend = Resolve(request);
        var properties = new AuthenticationProperties
        {
            RedirectUri = IsAllowedReturnUrl(returnUrl) ? returnUrl : frontend
        };
        properties.Items[FrontendPropertyKey] = frontend;
        return properties;
    }

    private bool IsAllowedReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || returnUrl != returnUrl.Trim()
            || returnUrl.Any(char.IsControl) || returnUrl.Contains('\\')
            || returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        if (GetAllowedOrigin(returnUrl) is not null)
        {
            return true;
        }

        // A colon in the first segment can be interpreted as a scheme by browsers
        // even when System.Uri treats a malformed URL (such as https:host) as relative.
        var firstSegment = returnUrl.Split(['/', '?', '#'], 2)[0];
        return !firstSegment.Contains(':') && Uri.TryCreate(returnUrl, UriKind.Relative, out _);
    }

    private string? GetAllowedOrigin(string? value)
    {
        var origin = GetOrigin(value);
        return origin is not null && _allowedBaseUrls.Contains(origin) ? origin : null;
    }

    private static string? GetOrigin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl) || value.Contains('\\')
            || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return null;
        }

        return uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant();
    }

    private static string FirstValue(string value) => value.Split(',', 2)[0].Trim();
}
