using WebApp.Shared.Dto.Admin;
using WebApp.Shared.Dto.Scheduling;

namespace WebApp.Application.Services.Interfaces
{
    public interface IUsersService
    {
        Task<PageDto<UserListItemDto>> GetUsersAsync(int pageIndex = 0, int pageSize = 50, string? q = null);
    }
}
