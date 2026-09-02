namespace SRC.Services.Interfaces;

public interface IUserDirectoryService
{
    Task<string> GetUserByIdAsync(string userId);
}
