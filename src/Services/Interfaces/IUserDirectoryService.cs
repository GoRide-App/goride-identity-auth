namespace SRC.Services.Impl;

public interface IUserDirectoryService
{
    Task<string> GetUserByIdAsync(string userId);
}