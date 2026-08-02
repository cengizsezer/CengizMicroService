using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WebApp.Application.Services.Interfaces;
using WebApp.Shared.Dto.Muhasebe;

namespace WebApp.Application.Services
{
    /// <summary>
    /// Muhasebe modülü istemcisi. Gateway <c>/catalog/{everything}</c> route'u ile
    /// <c>api/catalog/*</c>'a bağlanır, bu yüzden önek <c>/catalog/muhasebe</c>'dir.
    /// </summary>
    public class MuhasebeApi : IMuhasebeApi
    {
        private const string HesapPlani = "/catalog/muhasebe/hesap-plani";
        private const string Rapor = "/catalog/muhasebe/rapor";
        private const string BankaKodlari = "/catalog/muhasebe/banka-kodlari";
        private const string Fis = "/catalog/muhasebe/fis";
        private const string MasrafMerkezi = "/catalog/muhasebe/masraf-merkezi";

        private readonly HttpClient _http;

        public MuhasebeApi(HttpClient http) => _http = http;

        // ---- Hesap planı ----

        public async Task<List<HesapPlaniDto>> GetHesapPlaniAsync(CancellationToken ct = default)
        {
            try
            {
                return await _http.GetFromJsonAsync<List<HesapPlaniDto>>(HesapPlani, ct) ?? new();
            }
            catch (Exception)
            {
                return new();
            }
        }

        public Task<HesapPlaniDto?> GetHesapAsync(int id, CancellationToken ct = default)
            => GetOrNull<HesapPlaniDto>($"{HesapPlani}/{id}", ct);

        public Task<(SonrakiKodDto? Veri, string? Hata)> GetSonrakiKodAsync(int ustId, CancellationToken ct = default)
            => GonderAsync<SonrakiKodDto>(() => _http.GetAsync($"{HesapPlani}/{ustId}/sonraki-kod", ct));

        public Task<(List<BosKebirDto>? Veri, string? Hata)> GetBosKebirlerAsync(int grupId, CancellationToken ct = default)
            => GonderAsync<List<BosKebirDto>>(() => _http.GetAsync($"{HesapPlani}/{grupId}/bos-kebirler", ct));

        public async Task<List<BankaKoduDto>> GetBankaKodlariAsync(CancellationToken ct = default)
            => await GetOrNull<List<BankaKoduDto>>(BankaKodlari, ct) ?? new();

        public Task<(HesapPlaniDto? Veri, string? Hata)> CreateHesapAsync(HesapPlaniCreateDto dto, CancellationToken ct = default)
            => GonderAsync<HesapPlaniDto>(() => _http.PostAsJsonAsync(HesapPlani, dto, ct));

        public Task<(HesapPlaniDto? Veri, string? Hata)> UpdateHesapAsync(int id, HesapPlaniUpdateDto dto, CancellationToken ct = default)
            => GonderAsync<HesapPlaniDto>(() => _http.PutAsJsonAsync($"{HesapPlani}/{id}", dto, ct));

        public Task<(HesapPlaniDto? Veri, string? Hata)> PasifeAlAsync(int id, CancellationToken ct = default)
            => GonderAsync<HesapPlaniDto>(() => _http.PatchAsync($"{HesapPlani}/{id}/pasif", content: null, ct));

        public async Task<List<HesapPlaniDto>> GetHareketGorenlerAsync(CancellationToken ct = default)
            => await GetOrNull<List<HesapPlaniDto>>($"{HesapPlani}/hareket-gorenler", ct) ?? new();

        // ---- Fiş ----

        public Task<FisDto?> GetFisAsync(int id, CancellationToken ct = default)
            => GetOrNull<FisDto>($"{Fis}/{id}", ct);

        public async Task<List<FisOzetDto>> GetFisListeAsync(DateTime? bas = null, DateTime? bit = null,
                                                             FisDurum? durum = null, int? hesapId = null,
                                                             CancellationToken ct = default)
        {
            var q = new List<string>();
            if (bas is DateTime b) q.Add($"bas={b:yyyy-MM-dd}");
            if (bit is DateTime e) q.Add($"bit={e:yyyy-MM-dd}");
            if (durum is FisDurum d) q.Add($"durum={(byte)d}");
            if (hesapId is int h) q.Add($"hesapId={h}");

            var url = Fis + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty);
            return await GetOrNull<List<FisOzetDto>>(url, ct) ?? new();
        }

        public Task<(FisDto? Veri, string? Hata)> CreateFisAsync(FisYazDto dto, CancellationToken ct = default)
            => GonderAsync<FisDto>(() => _http.PostAsJsonAsync(Fis, dto, ct));

        public Task<(FisDto? Veri, string? Hata)> UpdateFisAsync(int id, FisYazDto dto, CancellationToken ct = default)
            => GonderAsync<FisDto>(() => _http.PutAsJsonAsync($"{Fis}/{id}", dto, ct));

        public Task<(FisDto? Veri, string? Hata)> KesinlestirAsync(int id, CancellationToken ct = default)
            => GonderAsync<FisDto>(() => _http.PatchAsync($"{Fis}/{id}/kesinlestir", content: null, ct));

        public async Task<(bool Basarili, string? Hata)> DeleteFisAsync(int id, CancellationToken ct = default)
        {
            var (_, hata) = await GonderAsync<object>(() => _http.DeleteAsync($"{Fis}/{id}", ct));
            return (hata is null, hata);
        }

        public Task<(FisDto? Veri, string? Hata)> TersKayitAsync(int id, TersKayitDto dto, CancellationToken ct = default)
            => GonderAsync<FisDto>(() => _http.PostAsJsonAsync($"{Fis}/{id}/ters-kayit", dto, ct));

        // ---- Masraf merkezi ----

        public async Task<List<MasrafMerkeziSecenekDto>> GetMasrafMerkezleriAsync(bool pasifDahil = false,
                                                                                  CancellationToken ct = default)
        {
            var url = MasrafMerkezi + (pasifDahil ? "?pasifDahil=true" : string.Empty);
            return await GetOrNull<List<MasrafMerkeziSecenekDto>>(url, ct) ?? new();
        }

        public Task<(MasrafMerkeziSecenekDto? Veri, string? Hata)> CreateMasrafMerkeziAsync(
            MasrafMerkeziYazDto dto, CancellationToken ct = default)
            => GonderAsync<MasrafMerkeziSecenekDto>(() => _http.PostAsJsonAsync(MasrafMerkezi, dto, ct));

        public Task<(MasrafMerkeziSecenekDto? Veri, string? Hata)> MasrafMerkeziPasifeAlAsync(
            int id, CancellationToken ct = default)
            => GonderAsync<MasrafMerkeziSecenekDto>(() => _http.PatchAsync($"{MasrafMerkezi}/{id}/pasif", content: null, ct));

        // ---- Rapor ----

        public Task<MizanDto?> GetMizanAsync(DateTime? bas = null, DateTime? bit = null, byte? seviye = null,
                                             CancellationToken ct = default)
        {
            var url = $"{Rapor}/mizan" + Sorgu(bas, bit, seviye is byte s ? $"seviye={s}" : null);
            return GetOrNull<MizanDto>(url, ct);
        }

        public Task<EkstreDto?> GetEkstreAsync(int hesapId, DateTime? bas = null, DateTime? bit = null,
                                               CancellationToken ct = default)
            => GetOrNull<EkstreDto>($"{Rapor}/ekstre/{hesapId}" + Sorgu(bas, bit), ct);

        public Task<MasrafMerkeziRaporDto?> GetMasrafMerkeziRaporAsync(DateTime? bas = null, DateTime? bit = null,
                                                                       CancellationToken ct = default)
            => GetOrNull<MasrafMerkeziRaporDto>($"{Rapor}/masraf-merkezi" + Sorgu(bas, bit), ct);

        /// <summary>Rapor uçlarının ortak <c>?bas=&amp;bit=</c> sorgu dizesi.</summary>
        private static string Sorgu(DateTime? bas, DateTime? bit, string? ek = null)
        {
            var q = new List<string>();
            if (bas is DateTime b) q.Add($"bas={b:yyyy-MM-dd}");
            if (bit is DateTime e) q.Add($"bit={e:yyyy-MM-dd}");
            if (ek is not null) q.Add(ek);

            return q.Count > 0 ? "?" + string.Join("&", q) : string.Empty;
        }

        // ---- Yardımcılar ----

        private async Task<T?> GetOrNull<T>(string url, CancellationToken ct)
        {
            try
            {
                using var resp = await _http.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) return default;
                return await resp.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
            }
            catch (Exception)
            {
                return default;
            }
        }

        /// <summary>
        /// İsteği gönderir; başarısızsa sunucunun <c>{ field, message }</c> gövdesindeki
        /// Türkçe mesajı çıkarır. Ham exception/JSON ekrana basılmaz.
        /// </summary>
        private static async Task<(T? Veri, string? Hata)> GonderAsync<T>(Func<Task<HttpResponseMessage>> istek)
        {
            try
            {
                using var resp = await istek();

                if (resp.IsSuccessStatusCode)
                {
                    if (resp.StatusCode == HttpStatusCode.NoContent) return (default, null);
                    return (await resp.Content.ReadFromJsonAsync<T>(), null);
                }

                if (resp.StatusCode == HttpStatusCode.NotFound)
                    return (default, "Kayıt bulunamadı. Sayfayı yenileyip tekrar deneyin.");

                var govde = await resp.Content.ReadAsStringAsync();
                return (default, MesajCoz(govde) ?? "İşlem tamamlanamadı. Lütfen tekrar deneyin.");
            }
            catch (Exception)
            {
                return (default, "Sunucuya ulaşılamadı. Bağlantınızı kontrol edip tekrar deneyin.");
            }
        }

        /// <summary>Sunucunun döndüğü <c>{ "field": "...", "message": "..." }</c> gövdesinden mesajı alır.</summary>
        private static string? MesajCoz(string? govde)
        {
            if (string.IsNullOrWhiteSpace(govde)) return null;

            try
            {
                using var doc = JsonDocument.Parse(govde);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

                foreach (var alan in new[] { "message", "detail", "title" })
                    if (doc.RootElement.TryGetProperty(alan, out var v) && v.ValueKind == JsonValueKind.String)
                        return v.GetString();

                return null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
