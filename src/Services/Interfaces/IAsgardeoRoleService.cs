namespace SRC.Services.Interfaces;

public interface IAsgardeoRoleService
{
    Task AssignRoleAsync(string asgardeoUserId, string displayName, string roleId);
}