using System.Net.Http.Json;
using WebApp.Application.Services.Interfaces;
using WebApp.Shared.Dto.Admin;
using WebApp.Shared.Dto.Scheduling;

namespace WebApp.Application.Services
{
    public class UsersService : IUsersService
    {
        private readonly HttpClient _http;
        public UsersService(HttpClient http) => _http = http;
        private const string Base = "auth";
        public Task<PageDto<UserListItemDto>> GetUsersAsync(int pageIndex = 0, int pageSize = 50, string? q = null)
            => _http.GetFromJsonAsync<PageDto<UserListItemDto>>(
                $"{Base}/users?p={pageIndex}&ps={pageSize}&q={Uri.EscapeDataString(q ?? "")}");
    }
}
