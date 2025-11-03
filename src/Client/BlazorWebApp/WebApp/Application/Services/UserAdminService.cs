namespace WebApp.Application.Services
{
    using System.Net;
    using System.Net.Http.Json;
    using WebApp.Application.Services.Interfaces;
    using WebApp.Shared.Dto.Admin;

    public class UserAdminService : IUserAdminService
    {
        private readonly HttpClient _http;
        public UserAdminService(HttpClient http) => _http = http;

        const string Base = "auth/admin"; // => api/admin/*

        public Task<PageDto<UserListItemDto>> GetUsersAsync(int pageIndex = 0, int pageSize = 50, string? q = null)
            => _http.GetFromJsonAsync<PageDto<UserListItemDto>>(
                $"{Base}/users?p={pageIndex}&ps={pageSize}&q={Uri.EscapeDataString(q ?? "")}");

        public Task<UserEditDto> GetUserByIdAsync(int id)
            => _http.GetFromJsonAsync<UserEditDto>($"{Base}/users/{id}");

        public async Task<(bool ok, string? err)> CreateUserAsync(UserEditDto dto)
        {
            var resp = await _http.PostAsJsonAsync($"{Base}/users", dto);
            if (resp.IsSuccessStatusCode) return (true, null);

            var err = await resp.Content.ReadAsStringAsync();
            return (false, err);
        }

        public async Task<bool> UpdateUserAsync(int id, UserEditDto dto)
            => (await _http.PutAsJsonAsync($"{Base}/users/{id}", dto)).IsSuccessStatusCode;

        public async Task<bool> DeleteUserAsync(int id)
            => (await _http.DeleteAsync($"{Base}/users/{id}")).IsSuccessStatusCode;

        public Task<List<RoleDto>> GetAllRolesAsync()
            => _http.GetFromJsonAsync<List<RoleDto>>($"{Base}/roles");

        public Task<IList<string>> GetUserRolesAsync(int userId)
            => _http.GetFromJsonAsync<IList<string>>($"{Base}/users/{userId}/roles");

        public async Task<bool> SetUserRolesAsync(int userId, List<string> roles)
            => (await _http.PutAsJsonAsync($"{Base}/users/{userId}/roles", roles)).IsSuccessStatusCode;

        public Task<List<FirmDto>> GetFirmsAsync()
            => _http.GetFromJsonAsync<List<FirmDto>>($"{Base}/firms");

        public async Task<int?> GetUserFirmIdAsync(int userId)
        {
            var resp = await _http.GetAsync($"auth/admin/users/{userId}/firm");
            if (!resp.IsSuccessStatusCode)
                return null;

            // 204 (No Content) kontrolü
            if (resp.StatusCode == HttpStatusCode.NoContent)
                return null;

            // boş body kontrolü
            if (resp.Content.Headers.ContentLength == 0)
                return null;

            return await resp.Content.ReadFromJsonAsync<int?>();
        }

        public async Task<bool> SetUserFirmAsync(int userId, int? firmId)
            => (await _http.PutAsJsonAsync($"{Base}/users/{userId}/firm", new { firmId })).IsSuccessStatusCode;
    }

}
