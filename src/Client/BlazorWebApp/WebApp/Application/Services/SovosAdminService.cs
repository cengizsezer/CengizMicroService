using System.Net.Http.Json;
using WebApp.Application.Services.Interfaces;
using WebApp.Shared.Dto.Admin;
using WebApp.Shared.Dto.Sovos;

namespace WebApp.Application.Services;

public class SovosAdminService : ISovosAdminService
{
    private readonly HttpClient _http;
    public SovosAdminService(HttpClient http) => _http = http;

    private const string Base = "faturakontrol";

    public Task<PageDto<SovosCompanyListItemDto>> GetCompaniesAsync(
        int pageIndex = 0, int pageSize = 50, string? q = null)
        => _http.GetFromJsonAsync<PageDto<SovosCompanyListItemDto>>(
            $"{Base}/companies?p={pageIndex}&ps={pageSize}&q={Uri.EscapeDataString(q ?? "")}");

    public Task<SovosCompanyEditDto?> GetCompanyByIdAsync(int id)
        => _http.GetFromJsonAsync<SovosCompanyEditDto>($"{Base}/companies/{id}");

    public async Task<(bool ok, string? err)> CreateCompanyAsync(NewSovosCompanyDto dto)
    {
        var resp = await _http.PostAsJsonAsync($"{Base}/companies", dto);
        return resp.IsSuccessStatusCode
            ? (true, null)
            : (false, await resp.Content.ReadAsStringAsync());
    }

    public async Task<(bool ok, string? err)> UpdateCompanyAsync(int id, SovosCompanyEditDto dto)
    {
        var resp = await _http.PutAsJsonAsync($"{Base}/companies/{id}", dto);
        if (resp.IsSuccessStatusCode) return (true, null);

        var body = await resp.Content.ReadAsStringAsync();
        var detail = string.IsNullOrWhiteSpace(body)
            ? $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}"
            : $"HTTP {(int)resp.StatusCode}: {body}";
        return (false, detail);
    }

    public async Task<bool> ChangePasswordAsync(int id, SovosCompanyPasswordDto dto)
        => (await _http.PostAsJsonAsync($"{Base}/companies/{id}/password", dto)).IsSuccessStatusCode;

    public async Task<bool> DeleteCompanyAsync(int id)
        => (await _http.DeleteAsync($"{Base}/companies/{id}")).IsSuccessStatusCode;

    public async Task<(bool ok, string? message)> RunNowAsync(int companyId)
    {
        var resp = await _http.PostAsync(
            $"{Base}/companies/{companyId}/run-now",
            null);

        var body = await resp.Content.ReadAsStringAsync();

        if (resp.IsSuccessStatusCode)
            return (true, "Tarama başlatıldı");

        return (false, body);
    }
}
