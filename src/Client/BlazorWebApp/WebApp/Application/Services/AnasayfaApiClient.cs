using System.Net.Http.Json;
using System.Text.Json;
using Blazored.LocalStorage;
using WebApp.Extensions;
using WebApp.Shared.Dto.Anasayfa;

namespace WebApp.Application.Services
{
    public interface IAnasayfaApiClient
    {
        Task<AnasayfaOzetDto?> OzetAsync(int? yil = null, int? ay = null, CancellationToken ct = default);

        /// <summary>
        /// Firma paneli: tüm firmaların satırları + seçili firmanın ayrıntısı, tek çağrı.
        /// <paramref name="firmaId"/> boşsa sunucu ilk firmayı seçer.
        /// </summary>
        Task<FirmaPaneliDto?> FirmaPaneliAsync(int? firmaId = null, CancellationToken ct = default);

        /// <summary>Sınıflandırma kartının formu. Hata varsa sunucunun mesajı döner, yoksa <c>null</c>.</summary>
        Task<string?> SiniflandirmaKaydetAsync(int firmaId, FirmaSiniflandirmaKaydetDto dto, CancellationToken ct = default);

        /// <summary>Seçili mizan formatının boş şablonu (xlsx).</summary>
        Task<(byte[]? Icerik, string? Hata)> MizanSablonuAsync(string format, CancellationToken ct = default);

        // ---- Takip kartı ----

        /// <summary><c>KullaniciId = null</c> sorumluyu kaldırır. Hata mesajı ya da <c>null</c>.</summary>
        Task<string?> SorumluAtaAsync(int firmaId, FirmaSorumluAtaDto dto, CancellationToken ct = default);

        Task<List<FirmaNotuDto>?> NotlarAsync(int firmaId, CancellationToken ct = default);
        Task<string?> NotEkleAsync(int firmaId, string metin, CancellationToken ct = default);
        Task<string?> NotSilAsync(int firmaId, long notId, CancellationToken ct = default);

        Task<List<FirmaOlayDto>?> OlaylarAsync(int firmaId, CancellationToken ct = default);
    }

    /// <inheritdoc cref="IAnasayfaApiClient"/>
    public class AnasayfaApiClient : IAnasayfaApiClient
    {
        private const string Prefix = "/catalog/anasayfa";

        private readonly HttpClient _http;

        public AnasayfaApiClient(HttpClient http) => _http = http;

        public Task<AnasayfaOzetDto?> OzetAsync(int? yil = null, int? ay = null, CancellationToken ct = default)
        {
            var sorgu = new List<string>();
            if (yil.HasValue) sorgu.Add($"yil={yil.Value}");
            if (ay.HasValue) sorgu.Add($"ay={ay.Value}");

            var yol = sorgu.Count == 0 ? $"{Prefix}/ozet" : $"{Prefix}/ozet?{string.Join("&", sorgu)}";
            return _http.GetResponseAsync<AnasayfaOzetDto?>(yol);
        }

        /// <inheritdoc />
        public Task<FirmaPaneliDto?> FirmaPaneliAsync(int? firmaId = null, CancellationToken ct = default)
        {
            // Firma kapsamı Banka Otomasyon'daki gibi ?firmaId= ile gidiyor; parametre
            // yoksa sunucu ilk firmayı seçiyor, istemci varsayılan uydurmuyor.
            var yol = firmaId is > 0
                ? $"{Prefix}/firma-paneli?firmaId={firmaId}"
                : $"{Prefix}/firma-paneli";

            return _http.GetResponseAsync<FirmaPaneliDto?>(yol);
        }

        /// <inheritdoc />
        public Task<string?> SiniflandirmaKaydetAsync(int firmaId, FirmaSiniflandirmaKaydetDto dto, CancellationToken ct = default)
            => YazAsync(() => _http.PutAsJsonAsync($"{Prefix}/firmalar/{firmaId}/siniflandirma", dto, ct), ct);

        /// <inheritdoc />
        public async Task<(byte[]? Icerik, string? Hata)> MizanSablonuAsync(string format, CancellationToken ct = default)
        {
            try
            {
                using var resp = await _http.GetAsync($"{Prefix}/mizan-sablonu?format={Uri.EscapeDataString(format)}", ct);
                if (!resp.IsSuccessStatusCode)
                    return (null, MesajCoz(await resp.Content.ReadAsStringAsync(ct)) ?? "Şablon indirilemedi.");

                return (await resp.Content.ReadAsByteArrayAsync(ct), null);
            }
            catch (Exception)
            {
                return (null, SunucuYok);
            }
        }

        /// <inheritdoc />
        public Task<string?> SorumluAtaAsync(int firmaId, FirmaSorumluAtaDto dto, CancellationToken ct = default)
            => YazAsync(() => _http.PutAsJsonAsync($"{Prefix}/firmalar/{firmaId}/sorumlu", dto, ct), ct);

        /// <inheritdoc />
        public Task<List<FirmaNotuDto>?> NotlarAsync(int firmaId, CancellationToken ct = default)
            => _http.GetResponseAsync<List<FirmaNotuDto>?>($"{Prefix}/firmalar/{firmaId}/notlar");

        /// <inheritdoc />
        public Task<string?> NotEkleAsync(int firmaId, string metin, CancellationToken ct = default)
            => YazAsync(() => _http.PostAsJsonAsync($"{Prefix}/firmalar/{firmaId}/notlar",
                                                    new FirmaNotuEkleDto { Metin = metin }, ct), ct);

        /// <inheritdoc />
        public Task<string?> NotSilAsync(int firmaId, long notId, CancellationToken ct = default)
            => YazAsync(() => _http.DeleteAsync($"{Prefix}/firmalar/{firmaId}/notlar/{notId}", ct), ct);

        /// <inheritdoc />
        public Task<List<FirmaOlayDto>?> OlaylarAsync(int firmaId, CancellationToken ct = default)
            => _http.GetResponseAsync<List<FirmaOlayDto>?>($"{Prefix}/firmalar/{firmaId}/olaylar");

        private const string SunucuYok ="Sunucuya ulaşılamadı. Bağlantınızı kontrol edip tekrar deneyin.";

        /// <summary>Yazma isteği: başarıda <c>null</c>, hatada kullanıcıya gösterilecek mesaj.</summary>
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

    /// <summary>
    /// "Son kullanılan firmalar" listesi. Kullanıcının kendi gezinme geçmişi olduğu için
    /// tarayıcıda tutuluyor; sunucuya yazılacak bir veri değil ve cihazlar arası
    /// taşınması da beklenmiyor.
    /// </summary>
    public interface ISonFirmalarStore
    {
        Task<List<SonFirmaDto>> GetAsync();

        /// <summary>Firmayı listenin başına alır; liste <see cref="EnFazla"/> kayıtla sınırlı.</summary>
        Task KaydetAsync(int firmaId, string ad, DateTime zaman);
    }

    /// <inheritdoc cref="ISonFirmalarStore"/>
    public class SonFirmalarStore : ISonFirmalarStore
    {
        public const int EnFazla = 6;

        private const string Anahtar = "SonKullanilanFirmalar";

        private readonly ILocalStorageService _depo;

        public SonFirmalarStore(ILocalStorageService depo) => _depo = depo;

        public async Task<List<SonFirmaDto>> GetAsync()
        {
            try
            {
                var liste = await _depo.GetItemAsync<List<SonFirmaDto>>(Anahtar);
                return liste?.OrderByDescending(f => f.SonKullanim).Take(EnFazla).ToList() ?? new();
            }
            catch
            {
                // Bozuk ya da eski biçimli kayıt yüzünden anasayfa açılmasın diye
                // sessizce boş liste; kullanıcı gezindikçe yeniden dolar.
                return new();
            }
        }

        public async Task KaydetAsync(int firmaId, string ad, DateTime zaman)
        {
            if (firmaId <= 0) return;

            var liste = await GetAsync();

            liste.RemoveAll(f => f.FirmaId == firmaId);
            liste.Insert(0, new SonFirmaDto { FirmaId = firmaId, Ad = ad, SonKullanim = zaman });

            if (liste.Count > EnFazla) liste = liste.Take(EnFazla).ToList();

            try
            {
                await _depo.SetItemAsync(Anahtar, liste);
            }
            catch
            {
                // Depolama kapalı olabilir; hızlı erişim listesi kritik değil.
            }
        }
    }
}
