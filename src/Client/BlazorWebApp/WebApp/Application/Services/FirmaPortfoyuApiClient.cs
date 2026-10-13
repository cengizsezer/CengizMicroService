using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WebApp.Shared.Dto.FirmaPortfoyu;

namespace WebApp.Application.Services
{
    public interface IFirmaPortfoyuApiClient
    {
        /// <summary>Kaydetmeden değerlendirir (yükleme penceresinin önizlemesi).</summary>
        Task<(AylikMizanOnizlemeDto? Sonuc, string? Hata)> OnizleAsync(AylikMizanKaydetDto dto, CancellationToken ct = default);

        /// <summary>
        /// Kaydeder. Aynı firma + ay zaten varsa ve <c>UzerineYaz</c> false ise sunucu 409 döner:
        /// <c>Cakisma=true</c> ve <c>Sonuc</c> önizleme + mevcut kayıt.
        /// </summary>
        Task<(AylikMizanOnizlemeDto? Sonuc, bool Cakisma, string? Hata)> KaydetAsync(AylikMizanKaydetDto dto, CancellationToken ct = default);

        Task<string?> SilAsync(int firmaId, int yil, int ay, CancellationToken ct = default);

        Task<(MizanDurumuDto? Durum, string? Hata)> DurumAsync(int yil, CancellationToken ct = default);

        /// <summary>Göstergeler (Aşama 2). <paramref name="donem"/> boşsa sunucu mizanı olan en son ayı seçer.</summary>
        Task<(PortfoyDto? Portfoy, string? Hata)> PortfoyAsync(string? donem, string karsilastir, CancellationToken ct = default);
    }

    /// <summary>Firma Portföyü uçları (<c>/catalog/firma-portfoyu</c>); hata gövdesi <c>{ field, message }</c>.</summary>
    public class FirmaPortfoyuApiClient : IFirmaPortfoyuApiClient
    {
        private const string Kok = "/catalog/firma-portfoyu";
        private const string SunucuYok = "Sunucuya ulaşılamadı. Bağlantınızı kontrol edip tekrar deneyin.";

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _http;

        public FirmaPortfoyuApiClient(HttpClient http) => _http = http;

        public async Task<(AylikMizanOnizlemeDto? Sonuc, string? Hata)> OnizleAsync(AylikMizanKaydetDto dto, CancellationToken ct = default)
        {
            var (sonuc, _, hata) = await GonderAsync(() => _http.PostAsJsonAsync($"{Kok}/mizan/onizle", dto, ct), ct);
            return (sonuc, hata);
        }

        public Task<(AylikMizanOnizlemeDto? Sonuc, bool Cakisma, string? Hata)> KaydetAsync(AylikMizanKaydetDto dto, CancellationToken ct = default)
            => GonderAsync(() => _http.PostAsJsonAsync($"{Kok}/mizan", dto, ct), ct);

        public async Task<string?> SilAsync(int firmaId, int yil, int ay, CancellationToken ct = default)
        {
            try
            {
                using var resp = await _http.DeleteAsync($"{Kok}/mizan/{firmaId}/{yil}/{ay}", ct);
                return resp.IsSuccessStatusCode ? null : MesajCoz(await resp.Content.ReadAsStringAsync(ct)) ?? "Silinemedi.";
            }
            catch (Exception)
            {
                return SunucuYok;
            }
        }

        public Task<(MizanDurumuDto? Durum, string? Hata)> DurumAsync(int yil, CancellationToken ct = default)
            => OkuAsync<MizanDurumuDto>($"{Kok}/mizan-durumu?yil={yil}", "Mizan durumu alınamadı.", ct);

        public Task<(PortfoyDto? Portfoy, string? Hata)> PortfoyAsync(string? donem, string karsilastir, CancellationToken ct = default)
            => OkuAsync<PortfoyDto>(
                $"{Kok}?karsilastir={Uri.EscapeDataString(karsilastir)}" + (string.IsNullOrEmpty(donem) ? "" : $"&donem={Uri.EscapeDataString(donem)}"),
                "Portföy alınamadı.", ct);

        private async Task<(T? Deger, string? Hata)> OkuAsync<T>(string yol, string varsayilanHata, CancellationToken ct) where T : class
        {
            try
            {
                using var resp = await _http.GetAsync(yol, ct);
                var govde = await resp.Content.ReadAsStringAsync(ct);
                return resp.IsSuccessStatusCode
                    ? (JsonSerializer.Deserialize<T>(govde, Json), null)
                    : (null, MesajCoz(govde) ?? varsayilanHata);
            }
            catch (Exception)
            {
                return (null, SunucuYok);
            }
        }

        private static async Task<(AylikMizanOnizlemeDto? Sonuc, bool Cakisma, string? Hata)> GonderAsync(
            Func<Task<HttpResponseMessage>> istek, CancellationToken ct)
        {
            try
            {
                using var resp = await istek();
                var govde = await resp.Content.ReadAsStringAsync(ct);
                if (resp.IsSuccessStatusCode)
                    return (JsonSerializer.Deserialize<AylikMizanOnizlemeDto>(govde, Json), false, null);
                if (resp.StatusCode == HttpStatusCode.Conflict)
                    return (JsonSerializer.Deserialize<AylikMizanOnizlemeDto>(govde, Json), true, null);
                return (null, false, MesajCoz(govde) ?? "İşlem yapılamadı.");
            }
            catch (Exception)
            {
                return (null, false, SunucuYok);
            }
        }

        private static string? MesajCoz(string govde)
        {
            try
            {
                using var belge = JsonDocument.Parse(govde);
                return belge.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
