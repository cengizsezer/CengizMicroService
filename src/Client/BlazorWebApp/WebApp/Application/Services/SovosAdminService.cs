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

    public async Task<(SovosBatchRunResultDto? result, string? error)> RunBatchAsync(
        SovosBatchRunRequestDto request,
        CancellationToken ct = default)
    {
        // Backend uzun sürebilir (5+ firma × ~30sn = 2.5dk+). Default HttpClient timeout
        // (100sn) yetmez; per-call linked CTS ile 25dk timeout uyguluyoruz.
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(25));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            var resp = await _http.PostAsJsonAsync(
                $"{Base}/companies/run-batch", request, linkedCts.Token);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(linkedCts.Token);
                var detail = string.IsNullOrWhiteSpace(body)
                    ? $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}"
                    : $"HTTP {(int)resp.StatusCode}: {body}";
                return (null, detail);
            }

            var result = await resp.Content.ReadFromJsonAsync<SovosBatchRunResultDto>(
                cancellationToken: linkedCts.Token);

            return result is null
                ? (null, "Sunucudan boş yanıt alındı.")
                : (result, null);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            return (null, "Tarama zaman aşımına uğradı (25 dakika). Bazı firmalar tamamlanmamış olabilir.");
        }
        catch (HttpRequestException ex)
        {
            return (null, $"Bağlantı hatası: {ex.Message}");
        }
    }

    // ── Firmalarım köprüsü (firma bazlı) ──────────────────────────────────
    // GET hesap yoksa da 200 + HasAccount=false döner.
    public Task<SovosFirmaCredentialDto?> GetFirmaCredentialAsync(int firmaId)
        => _http.GetFromJsonAsync<SovosFirmaCredentialDto>($"{Base}/firmalar/{firmaId}/sovos");

    // Upsert: kayıt yoksa oluşturur (Password zorunlu), varsa günceller
    // (Password boşsa şifreye dokunulmaz).
    public async Task<(bool ok, string? err)> UpsertFirmaCredentialAsync(
        int firmaId, SovosFirmaCredentialUpsertDto dto)
    {
        var resp = await _http.PutAsJsonAsync($"{Base}/firmalar/{firmaId}/sovos", dto);
        if (resp.IsSuccessStatusCode) return (true, null);

        var body = await resp.Content.ReadAsStringAsync();
        var detail = string.IsNullOrWhiteSpace(body)
            ? $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}"
            : $"HTTP {(int)resp.StatusCode}: {body}";
        return (false, detail);
    }
}
