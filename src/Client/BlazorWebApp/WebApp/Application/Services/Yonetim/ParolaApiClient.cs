using System.Net.Http.Json;
using System.Text.Json;
using WebApp.Shared.Dto.Parolalar;

namespace WebApp.Application.Services.Yonetim
{
    public interface IParolaApiClient
    {
        /// <summary>Üç türün tüm kayıtları, şifresiz.</summary>
        Task<(List<ParolaDto>? Liste, string? Hata)> ListeAsync(CancellationToken ct = default);

        Task<(ParolaDto? Kayit, string? Alan, string? Hata)> EkleAsync(ParolaKaydetDto dto, CancellationToken ct = default);

        Task<(ParolaDto? Kayit, string? Alan, string? Hata)> GuncelleAsync(int id, ParolaKaydetDto dto, CancellationToken ct = default);

        Task<string?> SilAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Tek şifre alanının değeri (<paramref name="islem"/>: "goruntule" | "kopyala"); sunucu erişim kaydı yazar.
        /// Değer çağıranda yalnız gerektiği kadar tutulmalı.
        /// </summary>
        Task<(string? Deger, string? Hata)> AlanAsync(int id, string alan, string islem, CancellationToken ct = default);
    }

    /// <summary>Yönetim › Parolalar uçları (<c>/catalog/parolalar</c>); hata gövdesi <c>{ field, message }</c>.</summary>
    public class ParolaApiClient : IParolaApiClient
    {
        private const string Kok = "/catalog/parolalar";
        private const string SunucuYok = "Sunucuya ulaşılamadı. Bağlantınızı kontrol edip tekrar deneyin.";

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _http;

        public ParolaApiClient(HttpClient http) => _http = http;

        public async Task<(List<ParolaDto>? Liste, string? Hata)> ListeAsync(CancellationToken ct = default)
        {
            try
            {
                using var resp = await _http.GetAsync(Kok, ct);
                if (!resp.IsSuccessStatusCode) return (null, (await HataAsync(resp, ct)).Mesaj ?? "Liste alınamadı.");
                return (await resp.Content.ReadFromJsonAsync<List<ParolaDto>>(Json, ct) ?? new(), null);
            }
            catch (Exception)
            {
                return (null, SunucuYok);
            }
        }

        public Task<(ParolaDto? Kayit, string? Alan, string? Hata)> EkleAsync(ParolaKaydetDto dto, CancellationToken ct = default)
            => KaydetAsync(() => _http.PostAsJsonAsync(Kok, dto, ct), ct);

        public Task<(ParolaDto? Kayit, string? Alan, string? Hata)> GuncelleAsync(int id, ParolaKaydetDto dto, CancellationToken ct = default)
            => KaydetAsync(() => _http.PutAsJsonAsync($"{Kok}/{id}", dto, ct), ct);

        public async Task<string?> SilAsync(int id, CancellationToken ct = default)
        {
            try
            {
                using var resp = await _http.DeleteAsync($"{Kok}/{id}", ct);
                return resp.IsSuccessStatusCode ? null : (await HataAsync(resp, ct)).Mesaj ?? "Silinemedi.";
            }
            catch (Exception)
            {
                return SunucuYok;
            }
        }

        public async Task<(string? Deger, string? Hata)> AlanAsync(int id, string alan, string islem, CancellationToken ct = default)
        {
            try
            {
                using var resp = await _http.GetAsync(
                    $"{Kok}/{id}/alan/{Uri.EscapeDataString(alan)}?islem={Uri.EscapeDataString(islem)}", ct);
                if (!resp.IsSuccessStatusCode) return (null, (await HataAsync(resp, ct)).Mesaj ?? "Değer alınamadı.");
                var dto = await resp.Content.ReadFromJsonAsync<ParolaAlanDegeriDto>(Json, ct);
                return (dto?.Deger ?? string.Empty, null);
            }
            catch (Exception)
            {
                return (null, SunucuYok);
            }
        }

        private async Task<(ParolaDto? Kayit, string? Alan, string? Hata)> KaydetAsync(Func<Task<HttpResponseMessage>> gonder, CancellationToken ct)
        {
            try
            {
                using var resp = await gonder();
                if (resp.IsSuccessStatusCode)
                    return (await resp.Content.ReadFromJsonAsync<ParolaDto>(Json, ct), null, null);
                var (alan, mesaj) = await HataAsync(resp, ct);
                return (null, alan, mesaj ?? "Kaydedilemedi.");
            }
            catch (Exception)
            {
                return (null, null, SunucuYok);
            }
        }

        private static async Task<(string? Alan, string? Mesaj)> HataAsync(HttpResponseMessage resp, CancellationToken ct)
        {
            try
            {
                var govde = await resp.Content.ReadAsStringAsync(ct);
                if (string.IsNullOrWhiteSpace(govde)) return (null, null);
                using var doc = JsonDocument.Parse(govde);
                var kok = doc.RootElement;
                if (kok.ValueKind != JsonValueKind.Object) return (null, null);
                var alan = kok.TryGetProperty("field", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;
                var mesaj = kok.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
                return (alan, mesaj);
            }
            catch (Exception)
            {
                return (null, null);
            }
        }
    }
}
