using System.Net.Http.Headers;
using System.Net.Http.Json;
using WebApp.Domain.Models.KdvBeyanname;

namespace WebApp.Application.Services.KdvBeyanname
{
    // Gateway upstream prefix: /catalog/... → CatalogService /api/catalog/...
    // Bu yüzden tüm path'ler "catalog/kdv-beyanname/..." ile başlar.
    public class KdvBeyannameApiService : IKdvBeyannameApiService
    {
        private const string Base = "catalog/kdv-beyanname";
        private readonly HttpClient _http;

        public KdvBeyannameApiService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<KdvFirmaCardDto>> GetFirmalarAsync(CancellationToken ct = default)
            => await _http.GetFromJsonAsync<List<KdvFirmaCardDto>>($"{Base}/firmalar", ct)
               ?? new();

        public async Task<bool> TaraTetikleAsync(
            int firmaId, TaraTetikleRequest req, CancellationToken ct = default)
        {
            var resp = await _http.PostAsJsonAsync($"{Base}/{firmaId}/tara", req, ct);
            return resp.IsSuccessStatusCode;
        }

        public async Task<List<KdvTarama>> GetTaramalarAsync(
            int firmaId, int take = 20, CancellationToken ct = default)
            => await _http.GetFromJsonAsync<List<KdvTarama>>(
                   $"{Base}/{firmaId}/taramalar?take={take}", ct)
               ?? new();

        public async Task<List<KdvGelenFatura>> GetGelenFaturalarAsync(
            int firmaId, DateTime? baslangic = null, DateTime? bitis = null,
            CancellationToken ct = default)
        {
            var qs = new List<string>();
            if (baslangic.HasValue) qs.Add($"baslangic={baslangic:yyyy-MM-dd}");
            if (bitis.HasValue) qs.Add($"bitis={bitis:yyyy-MM-dd}");
            var query = qs.Count > 0 ? "?" + string.Join("&", qs) : "";
            return await _http.GetFromJsonAsync<List<KdvGelenFatura>>(
                       $"{Base}/{firmaId}/gelen-faturalar{query}", ct)
                   ?? new();
        }

        public async Task<List<KdvMizanSatir>> GetMizanAsync(
            int firmaId, string donem, CancellationToken ct = default)
            => await _http.GetFromJsonAsync<List<KdvMizanSatir>>(
                   $"{Base}/{firmaId}/mizan?donem={donem}", ct)
               ?? new();

        public async Task<List<KdvYevmiyeSatir>> GetYevmiyeAsync(
            int firmaId, string donem, CancellationToken ct = default)
            => await _http.GetFromJsonAsync<List<KdvYevmiyeSatir>>(
                   $"{Base}/{firmaId}/yevmiye?donem={donem}", ct)
               ?? new();

        public async Task<MizanUploadResult> UploadMizanAsync(
            int firmaId, string donem, Stream content, string fileName, CancellationToken ct = default)
        {
            using var form = new MultipartFormDataContent();
            var streamContent = new StreamContent(content);
            streamContent.Headers.ContentType =
                new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            form.Add(streamContent, "file", fileName);

            var resp = await _http.PostAsync(
                $"{Base}/{firmaId}/mizan/upload?donem={donem}", form, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<MizanUploadResult>(cancellationToken: ct)
                   ?? new();
        }

        public async Task<YevmiyeUploadResult> UploadYevmiyeAsync(
            int firmaId, string donem, Stream content, string fileName, CancellationToken ct = default)
        {
            using var form = new MultipartFormDataContent();
            var streamContent = new StreamContent(content);
            streamContent.Headers.ContentType =
                new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            form.Add(streamContent, "file", fileName);

            var resp = await _http.PostAsync(
                $"{Base}/{firmaId}/yevmiye/upload?donem={donem}", form, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<YevmiyeUploadResult>(cancellationToken: ct)
                   ?? new();
        }

        public async Task<KdvKarsilastirmaSonucu> GetKarsilastirmaAsync(
            int firmaId, string donem, CancellationToken ct = default)
            => await _http.GetFromJsonAsync<KdvKarsilastirmaSonucu>(
                   $"{Base}/{firmaId}/karsilastirma?donem={donem}", ct)
               ?? new();

        public async Task<KdvSonuc> GetSonucAsync(
            int firmaId, string donem, CancellationToken ct = default)
            => await _http.GetFromJsonAsync<KdvSonuc>(
                   $"{Base}/{firmaId}/sonuc?donem={donem}", ct)
               ?? new();

        public async Task<(byte[] Content, string FileName)?> IndirXmlAsync(
            int firmaId, string donem, CancellationToken ct = default)
        {
            var resp = await _http.GetAsync($"{Base}/{firmaId}/xml?donem={donem}", ct);
            if (!resp.IsSuccessStatusCode) return null;

            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            var fileName = resp.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                ?? $"KDV1_44_{donem}.xml";
            return (bytes, fileName);
        }

        // ── Düzenleyen CRUD ────────────────────────────────────────────────

        public async Task<List<KdvDuzenleyen>> ListDuzenleyenlerAsync(
            bool includeInactive = false, CancellationToken ct = default)
        {
            var qs = includeInactive ? "?includeInactive=true" : string.Empty;
            return await _http.GetFromJsonAsync<List<KdvDuzenleyen>>(
                       $"{Base}/duzenleyenler{qs}", ct)
                   ?? new();
        }

        public async Task<KdvDuzenleyen?> GetDuzenleyenByIdAsync(int id, CancellationToken ct = default)
        {
            var resp = await _http.GetAsync($"{Base}/duzenleyenler/{id}", ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<KdvDuzenleyen>(cancellationToken: ct);
        }

        public async Task<KdvDuzenleyen> CreateDuzenleyenAsync(
            KdvDuzenleyenUpsert dto, CancellationToken ct = default)
        {
            var resp = await _http.PostAsJsonAsync($"{Base}/duzenleyenler", dto, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<KdvDuzenleyen>(cancellationToken: ct)
                   ?? new();
        }

        public async Task<KdvDuzenleyen?> UpdateDuzenleyenAsync(
            int id, KdvDuzenleyenUpsert dto, CancellationToken ct = default)
        {
            var resp = await _http.PutAsJsonAsync($"{Base}/duzenleyenler/{id}", dto, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<KdvDuzenleyen>(cancellationToken: ct);
        }

        public async Task<bool> DeleteDuzenleyenAsync(int id, CancellationToken ct = default)
        {
            var resp = await _http.DeleteAsync($"{Base}/duzenleyenler/{id}", ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
            resp.EnsureSuccessStatusCode();
            return true;
        }
    }
}
