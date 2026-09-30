using System.Net.Http.Json;
using System.Text.Json;
using WebApp.Extensions;
using WebApp.Shared.Dto.Yapilacaklar;

namespace WebApp.Application.Services
{
    /// <summary>
    /// Firma işleri kartı, Yapılacaklar ekranı, anasayfa şeridi ve Yönetim → Vergi Takvimi.
    /// Yazma metotları başarıda <c>null</c>, hatada kullanıcıya gösterilecek mesajı döner.
    /// </summary>
    public interface IYapilacaklarApiClient
    {
        Task<FirmaIsleriKartDto?> FirmaIsleriAsync(int firmaId, CancellationToken ct = default);
        Task<string?> IsEkleAsync(int firmaId, FirmaIsiKaydetDto dto, CancellationToken ct = default);
        Task<string?> IsGuncelleAsync(int firmaId, int isId, FirmaIsiKaydetDto dto, CancellationToken ct = default);
        Task<string?> IsSilAsync(int firmaId, int isId, CancellationToken ct = default);

        Task<YapilacaklarDto?> ListeAsync(IsFiltresi filtre, CancellationToken ct = default);
        Task<YapilacaklarOzetDto?> OzetAsync(CancellationToken ct = default);
        Task<string?> IsaretleAsync(IsIsaretleDto dto, CancellationToken ct = default);

        Task<List<VergiTakvimiDto>?> TakvimAsync(int? yil, CancellationToken ct = default);
        Task<string?> TakvimEkleAsync(VergiTakvimiKaydetDto dto, CancellationToken ct = default);
        Task<string?> TakvimGuncelleAsync(int id, VergiTakvimiKaydetDto dto, CancellationToken ct = default);
        Task<string?> TakvimSilAsync(int id, CancellationToken ct = default);
        Task<(int? Eklenen, string? Hata)> TakvimVarsayilanlarAsync(CancellationToken ct = default);
    }

    /// <inheritdoc cref="IYapilacaklarApiClient"/>
    public class YapilacaklarApiClient : IYapilacaklarApiClient
    {
        private const string Anasayfa = "/catalog/anasayfa";
        private const string Yapilacaklar = "/catalog/yapilacaklar";
        private const string Takvim = "/catalog/vergi-takvimi";
        private const string SunucuYok = "Sunucuya ulaşılamadı. Bağlantınızı kontrol edip tekrar deneyin.";

        private readonly HttpClient _http;

        public YapilacaklarApiClient(HttpClient http) => _http = http;

        public Task<FirmaIsleriKartDto?> FirmaIsleriAsync(int firmaId, CancellationToken ct = default)
            => OkuAsync<FirmaIsleriKartDto>($"{Anasayfa}/firmalar/{firmaId}/isler");

        public Task<string?> IsEkleAsync(int firmaId, FirmaIsiKaydetDto dto, CancellationToken ct = default)
            => YazAsync(() => _http.PostAsJsonAsync($"{Anasayfa}/firmalar/{firmaId}/isler", dto, ct), ct);

        public Task<string?> IsGuncelleAsync(int firmaId, int isId, FirmaIsiKaydetDto dto, CancellationToken ct = default)
            => YazAsync(() => _http.PutAsJsonAsync($"{Anasayfa}/firmalar/{firmaId}/isler/{isId}", dto, ct), ct);

        public Task<string?> IsSilAsync(int firmaId, int isId, CancellationToken ct = default)
            => YazAsync(() => _http.DeleteAsync($"{Anasayfa}/firmalar/{firmaId}/isler/{isId}", ct), ct);

        public Task<YapilacaklarDto?> ListeAsync(IsFiltresi filtre, CancellationToken ct = default)
            => OkuAsync<YapilacaklarDto>($"{Yapilacaklar}?filtre={(int)filtre}");

        public Task<YapilacaklarOzetDto?> OzetAsync(CancellationToken ct = default)
            => OkuAsync<YapilacaklarOzetDto>($"{Yapilacaklar}/ozet");

        public Task<string?> IsaretleAsync(IsIsaretleDto dto, CancellationToken ct = default)
            => YazAsync(() => _http.PutAsJsonAsync($"{Yapilacaklar}/isaretle", dto, ct), ct);

        public Task<List<VergiTakvimiDto>?> TakvimAsync(int? yil, CancellationToken ct = default)
            => OkuAsync<List<VergiTakvimiDto>>(yil is null ? Takvim : $"{Takvim}?yil={yil}");

        public Task<string?> TakvimEkleAsync(VergiTakvimiKaydetDto dto, CancellationToken ct = default)
            => YazAsync(() => _http.PostAsJsonAsync(Takvim, dto, ct), ct);

        public Task<string?> TakvimGuncelleAsync(int id, VergiTakvimiKaydetDto dto, CancellationToken ct = default)
            => YazAsync(() => _http.PutAsJsonAsync($"{Takvim}/{id}", dto, ct), ct);

        public Task<string?> TakvimSilAsync(int id, CancellationToken ct = default)
            => YazAsync(() => _http.DeleteAsync($"{Takvim}/{id}", ct), ct);

        public async Task<(int? Eklenen, string? Hata)> TakvimVarsayilanlarAsync(CancellationToken ct = default)
        {
            try
            {
                using var resp = await _http.PostAsync($"{Takvim}/varsayilanlar", null, ct);
                var govde = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode) return (null, MesajCoz(govde) ?? "Yüklenemedi.");

                using var belge = JsonDocument.Parse(govde);
                return (belge.RootElement.GetProperty("eklenen").GetInt32(), null);
            }
            catch (Exception)
            {
                return (null, SunucuYok);
            }
        }

        private async Task<T?> OkuAsync<T>(string yol) where T : class
        {
            try
            {
                return await _http.GetResponseAsync<T?>(yol);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static async Task<string?> YazAsync(Func<Task<HttpResponseMessage>> istek, CancellationToken ct)
        {
            try
            {
                using var resp = await istek();
                if (resp.IsSuccessStatusCode) return null;

                return MesajCoz(await resp.Content.ReadAsStringAsync(ct)) ?? "Kayıt yapılamadı.";
            }
            catch (Exception)
            {
                return SunucuYok;
            }
        }

        /// <summary>Sunucunun <c>{ field, message }</c> hata gövdesinden mesajı çıkarır.</summary>
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
