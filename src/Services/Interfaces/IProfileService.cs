namespace SRC.Services.Interfaces;

public interface IProfileService
{
    Task<string> GetProfileAsync(string accessToken);
    Task UpdateProfileAsync(string accessToken, ProfileUpdateRequest req);
}

public record ProfileUpdateRequest(string? GivenName, string? FamilyName, string? PhoneNumber);