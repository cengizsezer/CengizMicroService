using System.Net;
using System.Net.Http.Json;
using WebApp.Application.Services.Yonetim; // ApiErrorParser
using WebApp.Shared.Dto.FirmaKontrol;

namespace WebApp.Application.Services.FirmaKontrol
{
    /// <summary>
    /// Kurumlar vergisi beyannamesi uçları. Mevcut <see cref="FirmaKontrolApiClient"/>
    /// ile aynı desen: gateway <c>/catalog/*</c> önekiyle bağlanır, hataları
    /// <c>ApiErrorParser</c> üzerinden Türkçe mesaja çevirir.
    /// </summary>
    public class VergiBeyannameApiClient : IVergiBeyannameApiClient
    {
        private const string Base = "/catalog/firma-kontrol/vergi";

        private readonly HttpClient _httpClient;

        public VergiBeyannameApiClient(HttpClient httpClient) => _httpClient = httpClient;

        // ── Kalem katalogu ──

        public async Task<List<VergiKalemiDto>> GetKalemlerAsync(bool pasifDahil = false, CancellationToken ct = default)
        {
            var url = $"{Base}/kalemler" + (pasifDahil ? "?pasifDahil=true" : string.Empty);
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return await response.Content.ReadFromJsonAsync<List<VergiKalemiDto>>(cancellationToken: ct) ?? new();
        }

        public async Task<VergiKalemiDto> KalemEkleAsync(VergiKalemiYazDto dto, CancellationToken ct = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/kalemler", dto, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return (await response.Content.ReadFromJsonAsync<VergiKalemiDto>(cancellationToken: ct))!;
        }

        public async Task<VergiKalemiDto> KalemGuncelleAsync(int id, VergiKalemiYazDto dto, CancellationToken ct = default)
        {
            var response = await _httpClient.PutAsJsonAsync($"{Base}/kalemler/{id}", dto, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return (await response.Content.ReadFromJsonAsync<VergiKalemiDto>(cancellationToken: ct))!;
        }

        public async Task<VergiKalemiDto> KalemPasifeAlAsync(int id, CancellationToken ct = default)
        {
            var response = await _httpClient.PatchAsync($"{Base}/kalemler/{id}/pasif", content: null, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return (await response.Content.ReadFromJsonAsync<VergiKalemiDto>(cancellationToken: ct))!;
        }

        public async Task KalemSilAsync(int id, CancellationToken ct = default)
        {
            var response = await _httpClient.DeleteAsync($"{Base}/kalemler/{id}", ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);
        }

        public async Task SiralamayiKaydetAsync(List<VergiKalemSiraDto> sira, CancellationToken ct = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/kalemler/sirala", sira, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);
        }

        // ── Beyanname ──

        public async Task<VergiBeyannameDto?> GetBeyannameAsync(int firmaId, short donemYil, CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync($"{Base}/{firmaId}/{donemYil}", ct);

            // Kayıt yoksa sunucu 204 döner; bu bir hata değil.
            if (response.StatusCode == HttpStatusCode.NoContent)
                return null;

            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return await response.Content.ReadFromJsonAsync<VergiBeyannameDto>(cancellationToken: ct);
        }

        public async Task<VergiSonucDto> OnizleAsync(VergiBeyannameYazDto dto, CancellationToken ct = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/onizle", dto, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return (await response.Content.ReadFromJsonAsync<VergiSonucDto>(cancellationToken: ct))!;
        }

        public async Task<VergiBeyannameDto> KaydetAsync(int firmaId, VergiBeyannameYazDto dto, CancellationToken ct = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/{firmaId}", dto, ct);
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            return (await response.Content.ReadFromJsonAsync<VergiBeyannameDto>(cancellationToken: ct))!;
        }

        public async Task<(byte[] Icerik, string DosyaAdi)?> ExcelAsync(int firmaId, short donemYil, CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync($"{Base}/{firmaId}/{donemYil}/excel", ct);

            // Henüz kaydedilmemiş beyanname: hata değil, kullanıcıya "önce kaydedin" denir.
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);

            var icerik = await response.Content.ReadAsByteArrayAsync(ct);
            var dosyaAdi = response.Content.Headers.ContentDisposition?.FileNameStar
                           ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                           ?? $"kurumlar-vergisi-{donemYil}.xlsx";

            return (icerik, dosyaAdi);
        }
    }
}
