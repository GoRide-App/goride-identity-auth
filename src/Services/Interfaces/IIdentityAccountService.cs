namespace SRC.Services.Interfaces;

/// <summary>Account-level operations against the WSO2 Identity Server (Asgardeo) via SCIM2.</summary>
public interface IIdentityAccountService
{
    /// <summary>
    /// Sets <c>accountDisabled = true</c> on the user, which makes the Identity Server refuse
    /// every subsequent authentication and token refresh for that account.
    /// </summary>
    Task DisableAccountAsync(string userId, CancellationToken cancellationToken = default);
}
