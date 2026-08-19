using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using WebApp.Application.Services.Interfaces;
using WebApp.Shared.Dto.BankaEkstre;

namespace WebApp.Application.Services
{
    /// <summary>
    /// Banka ekstresi işleme modülü istemcisi. Gateway <c>/catalog/{everything}</c> route'u
    /// ile <c>api/catalog/*</c>'a bağlanır, bu yüzden önek <c>/catalog/banka-ekstre</c>'dir.
    /// Hata gövdesi <c>{ field, message }</c> sözleşmesiyle okunur (MuhasebeApi ile aynı).
    /// </summary>
    public class BankaEkstreApi : IBankaEkstreApi
    {
        private const string Hesaplar = "/catalog/banka-ekstre/banka-hesaplari";
        private const string Ekstre = "/catalog/banka-ekstre/ekstre";
        private const string HesapPlani = "/catalog/banka-ekstre/hesap-plani";

        private const string XlsxTuru = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private readonly HttpClient _http;

        public BankaEkstreApi(HttpClient http) => _http = http;

        // ---- Banka hesapları ----

        public async Task<List<BankaHesabiDto>> GetHesaplarAsync(bool pasifDahil = false, CancellationToken ct = default)
            => await GetOrNull<List<BankaHesabiDto>>($"{Hesaplar}?pasifDahil={pasifDahil.ToString().ToLowerInvariant()}", ct) ?? new();

        public async Task<List<ParserSecenekDto>> GetParserlerAsync(CancellationToken ct = default)
            => await GetOrNull<List<ParserSecenekDto>>($"{Hesaplar}/parserler", ct) ?? new();

        public Task<(BankaHesabiDto? Veri, string? Hata)> CreateHesapAsync(BankaHesabiYazDto dto, CancellationToken ct = default)
            => GonderAsync<BankaHesabiDto>(() => _http.PostAsJsonAsync(Hesaplar, dto, ct));

        public Task<(BankaHesabiDto? Veri, string? Hata)> UpdateHesapAsync(int id, BankaHesabiYazDto dto, CancellationToken ct = default)
            => GonderAsync<BankaHesabiDto>(() => _http.PutAsJsonAsync($"{Hesaplar}/{id}", dto, ct));

        public async Task<string?> DeleteHesapAsync(int id, CancellationToken ct = default)
        {
            var (_, hata) = await GonderAsync<object>(() => _http.DeleteAsync($"{Hesaplar}/{id}", ct));
            return hata;
        }

        // ---- Ekstre ----

        public async Task<List<EkstreYuklemeDto>> GetYuklemelerAsync(CancellationToken ct = default)
            => await GetOrNull<List<EkstreYuklemeDto>>(Ekstre, ct) ?? new();

        public Task<EkstreYuklemeDto?> GetYuklemeAsync(int id, CancellationToken ct = default)
            => GetOrNull<EkstreYuklemeDto>($"{Ekstre}/{id}", ct);

        public Task<(EkstreYuklemeDto? Veri, string? Hata)> YukleAsync(int bankaHesabiId, Stream icerik, string dosyaAdi,
                                                                      CancellationToken ct = default)
            => GonderAsync<EkstreYuklemeDto>(() =>
            {
                var form = new MultipartFormDataContent
                {
                    { new StringContent(bankaHesabiId.ToString()), "bankaHesabiId" }
                };

                var dosya = new StreamContent(icerik);
                dosya.Headers.ContentType = new MediaTypeHeaderValue(XlsxTuru);
                form.Add(dosya, "file", dosyaAdi);

                return _http.PostAsync($"{Ekstre}/yukle", form, ct);
            });

        public async Task<List<EkstreSatirDto>> GetSatirlarAsync(int ekstreId, SatirDurum? durum = null, CancellationToken ct = default)
        {
            var url = $"{Ekstre}/{ekstreId}/satirlar";
            if (durum is SatirDurum d) url += $"?durum={(byte)d}";

            return await GetOrNull<List<EkstreSatirDto>>(url, ct) ?? new();
        }

        public Task<(EkstreSatirDto? Veri, string? Hata)> OnaylaAsync(int satirId, string hesapKodu, CancellationToken ct = default)
            => GonderAsync<EkstreSatirDto>(() =>
                _http.PutAsJsonAsync($"{Ekstre}/satir/{satirId}/onayla", new SatirOnaylaDto { HesapKodu = hesapKodu }, ct));

        public Task<(EkstreSatirDto? Veri, string? Hata)> DigerBankadaAsync(int satirId, CancellationToken ct = default)
            => GonderAsync<EkstreSatirDto>(() =>
                _http.PutAsync($"{Ekstre}/satir/{satirId}/diger-bankada", content: null, ct));

        public Task<(DisaAktarimSonucDto? Veri, string? Hata)> DisaAktarAsync(int ekstreId, CancellationToken ct = default)
            => GonderAsync<DisaAktarimSonucDto>(() => _http.PostAsync($"{Ekstre}/{ekstreId}/disa-aktar", content: null, ct));

        public async Task<string?> SilAsync(int ekstreId, CancellationToken ct = default)
        {
            var (_, hata) = await GonderAsync<object>(() => _http.DeleteAsync($"{Ekstre}/{ekstreId}", ct));
            return hata;
        }

        // ---- Hesap planı ----

        public async Task<List<HesapPlaniKaydiDto>> HesapPlaniAraAsync(string? q, string? anaGrup = null, int enFazla = 20,
                                                                      CancellationToken ct = default)
        {
            var parametreler = new List<string> { $"enFazla={enFazla}" };
            if (!string.IsNullOrWhiteSpace(q)) parametreler.Add($"q={Uri.EscapeDataString(q)}");
            if (!string.IsNullOrWhiteSpace(anaGrup)) parametreler.Add($"anaGrup={Uri.EscapeDataString(anaGrup)}");

            return await GetOrNull<List<HesapPlaniKaydiDto>>($"{HesapPlani}?{string.Join("&", parametreler)}", ct) ?? new();
        }

        public async Task<int> HesapPlaniSayisiAsync(CancellationToken ct = default)
            => await GetOrNull<int?>($"{HesapPlani}/sayi", ct) ?? 0;

        public Task<(HesapPlaniIceAktarimSonucDto? Veri, string? Hata)> HesapPlaniIceAktarAsync(
            Stream icerik, string dosyaAdi, CancellationToken ct = default)
            => GonderAsync<HesapPlaniIceAktarimSonucDto>(() =>
            {
                var form = new MultipartFormDataContent();
                var dosya = new StreamContent(icerik);
                dosya.Headers.ContentType = new MediaTypeHeaderValue(XlsxTuru);
                form.Add(dosya, "file", dosyaAdi);

                return _http.PostAsync($"{HesapPlani}/ice-aktar", form, ct);
            });

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
