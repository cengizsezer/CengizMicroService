using System.Net.Http.Json;
using System.Text.Json;
using CatalogService.Api.Features.Declarations.Dtos;
using WebApp.Application.Services.Interfaces;
using WebApp.Extensions;

namespace WebApp.Application.Services
{
    /// <inheritdoc cref="IBeyannameTuruApiService"/>
    public class BeyannameTuruApiService : IBeyannameTuruApiService
    {
        private const string Prefix = "/catalog/beyanname/turler";

        private readonly HttpClient _http;

        public BeyannameTuruApiService(HttpClient http) => _http = http;

        public async Task<List<BeyannameTuruDto>> GetHepsiAsync(bool pasifDahil = false,
                                                                 CancellationToken ct = default)
            => await _http.GetResponseAsync<List<BeyannameTuruDto>>(
                   $"{Prefix}?pasifDahil={(pasifDahil ? "true" : "false")}") ?? new();

        public async Task<(BeyannameTuruDto? Kayit, string? Hata)> EkleAsync(BeyannameTuruYazDto dto,
                                                                             CancellationToken ct = default)
        {
            var resp = await _http.PostAsJsonAsync(Prefix, dto, ct);
            return await SonucAsync(resp, ct);
        }

        public async Task<(BeyannameTuruDto? Kayit, string? Hata)> GuncelleAsync(int id, BeyannameTuruYazDto dto,
                                                                                 CancellationToken ct = default)
        {
            var resp = await _http.PutAsJsonAsync($"{Prefix}/{id}", dto, ct);
            return await SonucAsync(resp, ct);
        }

        public async Task<(int Eklenen, int Toplam, string? Hata)> VarsayilanlariYukleAsync(
            CancellationToken ct = default)
        {
            var resp = await _http.PostAsync($"{Prefix}/varsayilanlari-yukle", content: null, ct);

            if (!resp.IsSuccessStatusCode)
                return (0, 0, await HataMetniAsync(resp, ct));

            var sonuc = await resp.Content.ReadFromJsonAsync<YuklemeSonucu>(cancellationToken: ct);
            return (sonuc?.Eklenen ?? 0, sonuc?.Toplam ?? 0, null);
        }

        private static async Task<(BeyannameTuruDto?, string?)> SonucAsync(HttpResponseMessage resp,
                                                                           CancellationToken ct)
        {
            if (!resp.IsSuccessStatusCode) return (null, await HataMetniAsync(resp, ct));

            return (await resp.Content.ReadFromJsonAsync<BeyannameTuruDto>(cancellationToken: ct), null);
        }

        /// <summary>
        /// Sunucunun <c>{ field, message }</c> gövdesini okur. Kural ihlallerinde kullanıcı
        /// "işlem başarısız" değil, hangi alanın neden reddedildiğini görsün.
        /// </summary>
        private static async Task<string> HataMetniAsync(HttpResponseMessage resp, CancellationToken ct)
        {
            try
            {
                var govde = await resp.Content.ReadAsStringAsync(ct);
                if (!string.IsNullOrWhiteSpace(govde))
                {
                    using var belge = JsonDocument.Parse(govde);
                    if (belge.RootElement.TryGetProperty("message", out var mesaj))
                        return mesaj.GetString() ?? resp.ReasonPhrase ?? "Bilinmeyen hata.";
                }
            }
            catch (JsonException)
            {
                // Gövde JSON değilse (proxy hatası, HTML sayfası) durum koduna düşülür.
            }

            return $"Sunucu hatası ({(int)resp.StatusCode}).";
        }

        private sealed class YuklemeSonucu
        {
            public int Eklenen { get; set; }
            public int Toplam { get; set; }
            public string? Message { get; set; }
        }
    }
}
