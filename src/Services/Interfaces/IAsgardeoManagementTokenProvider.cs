namespace SRC.Services.Interfaces;

/// <summary>
/// Obtains client-credentials tokens for the Asgardeo machine-to-machine application.
/// Tokens are cached per scope until shortly before they expire.
/// </summary>
public interface IAsgardeoManagementTokenProvider
{
    Task<string> GetTokenAsync(string scope, CancellationToken cancellationToken = default);
}
