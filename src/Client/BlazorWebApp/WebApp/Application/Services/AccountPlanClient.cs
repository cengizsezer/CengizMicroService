using System.Net.Http.Json;
using WebApp.Application.Services.Interfaces;
using WebApp.Shared.Dto.AccountNodePlan;

namespace WebApp.Application.Services
{
    public class AccountPlanClient: IAccountPlanClient
    {
        private readonly HttpClient _http;
        private const string Base = "/catalog/accountplan";
        public AccountPlanClient(HttpClient http) => _http = http;

        public Task<List<AccountNodeDto>> GetTreeAsync(CancellationToken ct = default)
            => _http.GetFromJsonAsync<List<AccountNodeDto>>($"{Base}/tree", ct)!;

        public Task<AccountNodeDto?> GetAsync(int id, CancellationToken ct = default)
            => _http.GetFromJsonAsync<AccountNodeDto>($"{Base}/{id}", ct);

        public Task UpdateNotesAsync(int id, string? notes, CancellationToken ct = default)
            => _http.PutAsJsonAsync($"{Base}/{id}/notes", new { Notes = notes }, ct);
    }
}
