using System.Net.Http.Json;
using WebApp.Application.Services.Interfaces;
using WebApp.Shared.Dto.Admin;

namespace WebApp.Application.Services
{
    public class UserAdminService : IUserAdminService
    {
        private readonly HttpClient _http;
        public UserAdminService(HttpClient http) => _http = http;

        private const string Base = "auth/admin";

        // ---- Users ----
        public Task<PageDto<UserListItemDto>> GetUsersAsync(int pageIndex = 0, int pageSize = 50, string? q = null)
            => _http.GetFromJsonAsync<PageDto<UserListItemDto>>(
                $"{Base}/users?p={pageIndex}&ps={pageSize}&q={Uri.EscapeDataString(q ?? "")}");

        public Task<UserEditDto> GetUserByIdAsync(int id)
            => _http.GetFromJsonAsync<UserEditDto>($"{Base}/users/{id}");

        public async Task<(bool ok, string? err)> CreateUserAsync(NewUserDto dto)
        {
            var resp = await _http.PostAsJsonAsync($"{Base}/users", dto);
            return resp.IsSuccessStatusCode
                ? (true, null)
                : (false, await resp.Content.ReadAsStringAsync());
        }
        public async Task AdminChangePasswordAsync(int userId, UserChangePasswordDto dto)
        {
            var resp = await _http.PostAsJsonAsync($"auth/admin/users/{userId}/password", dto);
            resp.EnsureSuccessStatusCode(); // 204 bekliyoruz
        }
        public async Task<bool> UpdateUserAsync(int id, UserEditDto dto)
            => (await _http.PutAsJsonAsync($"{Base}/users/{id}", dto)).IsSuccessStatusCode;

        public async Task<bool> DeleteUserAsync(int id)
            => (await _http.DeleteAsync($"{Base}/users/{id}")).IsSuccessStatusCode;

        // ---- Roles ----
        public Task<List<RoleDto>> GetAllRolesAsync()
            => _http.GetFromJsonAsync<List<RoleDto>>($"{Base}/roles");

        public Task<IList<string>> GetUserRolesAsync(int userId)
            => _http.GetFromJsonAsync<IList<string>>($"{Base}/users/{userId}/roles");

        public async Task<bool> SetUserRolesAsync(int userId, List<string> roles)
            => (await _http.PutAsJsonAsync($"{Base}/users/{userId}/roles", roles)).IsSuccessStatusCode;

        // ---- Firms (Tenants) ----
        public Task<List<FirmDto>> GetFirmsAsync()
            => _http.GetFromJsonAsync<List<FirmDto>>($"{Base}/firms");

        public Task<List<UserFirmDto>> GetUserFirmsAsync(int userId)
            => _http.GetFromJsonAsync<List<UserFirmDto>>($"{Base}/users/{userId}/firms");

        // interface'inle uyumlu: bool değil Task döndürüyoruz
        public async Task SetUserFirmsAsync(int userId, SetUserFirmsRequest req)
        {
            var resp = await _http.PutAsJsonAsync($"{Base}/users/{userId}/firms", req);
            resp.EnsureSuccessStatusCode(); // UI'daki try/catch'ine düşer
        }
    }
}
