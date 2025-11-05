using WebApp.Shared.Dto.Admin;

namespace WebApp.Application.Services.Interfaces
{
    public interface IUserAdminService
    {
        // Users
        Task<PageDto<UserListItemDto>> GetUsersAsync(int pageIndex = 0, int pageSize = 50, string? q = null);
        Task<UserEditDto> GetUserByIdAsync(int id);
        Task<(bool ok, string? err)> CreateUserAsync(NewUserDto dto);
        Task<bool> UpdateUserAsync(int id, UserEditDto dto);
        Task<bool> DeleteUserAsync(int id);
        Task AdminChangePasswordAsync(int userId, UserChangePasswordDto dto);
        // Roles
        Task<List<RoleDto>> GetAllRolesAsync();
        Task<IList<string>> GetUserRolesAsync(int userId);
        Task<bool> SetUserRolesAsync(int userId, List<string> roles);

        // Firms (Tenants)
        Task<List<FirmDto>> GetFirmsAsync();
        Task<List<UserFirmDto>> GetUserFirmsAsync(int userId);
        Task SetUserFirmsAsync(int userId, SetUserFirmsRequest req);
    }
}
