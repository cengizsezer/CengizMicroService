using System.Net.Http.Json;
using WebApp.Application.Services.Interfaces;
using WebApp.Shared.Dto.Scheduling;

namespace WebApp.Application.Services
{
    public class UsersService : IUsersService
    {
        private readonly HttpClient _http;
        public UsersService(HttpClient http) => _http = http;

        public async Task<List<UserMiniDto>> SearchAsync(string? search = null, int page = 0, int pageSize = 50, CancellationToken ct = default)
        {
            var qs = new List<string> { $"page={page}", $"pageSize={pageSize}" };
            if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
            var url = "/api/identity/users" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
            var res = await _http.GetFromJsonAsync<PagedResult<UserMiniDto>>(url, ct);
            return res?.Items ?? new();
        }

        public class PagedResult<T>
        {
            public int Total { get; set; }
            public List<T> Items { get; set; } = new();
        }
    }
}
