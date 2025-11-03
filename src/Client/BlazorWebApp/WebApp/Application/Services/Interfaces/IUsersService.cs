using WebApp.Shared.Dto.Scheduling;

namespace WebApp.Application.Services.Interfaces
{
    public interface IUsersService
    {
        Task<List<UserMiniDto>> SearchAsync(string? search = null, int page = 0, int pageSize = 50, CancellationToken ct = default);
    }
}
