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
        private const string Eslesmeler = "/catalog/banka-ekstre/eslesmeler";
        private const string VergiKodlari = "/catalog/banka-ekstre/vergi-kodlari";
        private const string KisiYonlendirmeleri = "/catalog/banka-ekstre/kisi-yonlendirmeleri";
        private const string SabitKurallar = "/catalog/banka-ekstre/sabit-kurallar";
        private const string AciklamaSablonlari = "/catalog/banka-ekstre/aciklama-sablonlari";
        private const string UnvanDesenleri = "/catalog/banka-ekstre/unvan-desenleri";
        private const string Firmalar = "/catalog/banka-ekstre/firmalar";

        private const string XlsxTuru = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private readonly HttpClient _http;

        public BankaEkstreApi(HttpClient http) => _http = http;

        // ---- Firma seçim ekranı ----

        public async Task<List<FirmaBankaOzetiDto>> FirmaOzetleriAsync(IEnumerable<string> tenantlar,
                                                                       CancellationToken ct = default)
        {
            var liste = (tenantlar ?? Enumerable.Empty<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            if (liste.Count == 0) return new List<FirmaBankaOzetiDto>();

            var sorgu = string.Join("&", liste.Select(t => $"tenantlar={Uri.EscapeDataString(t)}"));
            return await GetOrNull<List<FirmaBankaOzetiDto>>($"{Firmalar}/ozet?{sorgu}", ct) ?? new();
        }

        // ---- Banka hesapları ----

        public async Task<List<BankaHesabiDto>> GetHesaplarAsync(bool pasifDahil = false, CancellationToken ct = default)
            => await GetOrNull<List<BankaHesabiDto>>($"{Hesaplar}?pasifDahil={pasifDahil.ToString().ToLowerInvariant()}", ct) ?? new();

        public async Task<List<ParserSecenekDto>> GetParserlerAsync(CancellationToken ct = default)
            => await GetOrNull<List<ParserSecenekDto>>($"{Hesaplar}/parserler", ct) ?? new();

        public async Task<string?> AnahtarOnerisiAsync(string? hesapAdi, string? bankaAdi, CancellationToken ct = default)
        {
            var adres = $"{Hesaplar}/anahtar-onerisi?hesapAdi={Uri.EscapeDataString(hesapAdi ?? string.Empty)}" +
                        $"&bankaAdi={Uri.EscapeDataString(bankaAdi ?? string.Empty)}";

            return (await GetOrNull<AnahtarOnerisiDto>(adres, ct))?.EslestirmeAnahtarlari;
        }

        public async Task<HesapSahibiKimlikDto> HesapSahibiAsync(CancellationToken ct = default)
            => await GetOrNull<HesapSahibiKimlikDto>($"{Hesaplar}/hesap-sahibi", ct) ?? new();

        public Task<(HesapSahibiKimlikDto? Veri, string? Hata)> HesapSahibiKaydetAsync(HesapSahibiKimlikYazDto dto,
                                                                                       CancellationToken ct = default)
            => GonderAsync<HesapSahibiKimlikDto>(() => _http.PutAsJsonAsync($"{Hesaplar}/hesap-sahibi", dto, ct));

        public async Task<List<HesapSahibiOnerisiDto>> HesapSahibiOnerileriAsync(CancellationToken ct = default)
            => await GetOrNull<List<HesapSahibiOnerisiDto>>($"{Hesaplar}/hesap-sahibi-onerileri", ct) ?? new();

        public Task<(BankaHesabiDto? Veri, string? Hata)> CreateHesapAsync(BankaHesabiYazDto dto, CancellationToken ct = default)
            => GonderAsync<BankaHesabiDto>(() => _http.PostAsJsonAsync(Hesaplar, dto, ct));

        public Task<(BankaHesabiDto? Veri, string? Hata)> UpdateHesapAsync(int id, BankaHesabiYazDto dto, CancellationToken ct = default)
            => GonderAsync<BankaHesabiDto>(() => _http.PutAsJsonAsync($"{Hesaplar}/{id}", dto, ct));

        public async Task<string?> DeleteHesapAsync(int id, CancellationToken ct = default)
        {
            var (_, hata) = await GonderAsync<object>(() => _http.DeleteAsync($"{Hesaplar}/{id}", ct));
            return hata;
        }

        public Task<(BankaHesabiIceAktarimSonucDto? Veri, string? Hata)> HesaplariIceAktarAsync(
            Stream icerik, string dosyaAdi, CancellationToken ct = default)
            => GonderAsync<BankaHesabiIceAktarimSonucDto>(() =>
            {
                var form = new MultipartFormDataContent();
                var dosya = new StreamContent(icerik);
                dosya.Headers.ContentType = new MediaTypeHeaderValue(XlsxTuru);
                form.Add(dosya, "file", dosyaAdi);

                return _http.PostAsync($"{Hesaplar}/ice-aktar", form, ct);
            });

        public Task<(string? DosyaAdi, byte[]? Icerik, string? Hata)> HesapSablonuAsync(CancellationToken ct = default)
            => DosyaIndirAsync(() => _http.GetAsync($"{Hesaplar}/sablon", ct), "banka-hesaplari-sablon.xlsx",
                               "Şablon indirilemedi.", ct);

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

        public Task<(EkstreSatirDto? Veri, string? Hata)> OnaylaAsync(int satirId, string hesapKodu,
                                                                      bool kisiYonlendir = false, CancellationToken ct = default)
            => GonderAsync<EkstreSatirDto>(() =>
                _http.PutAsJsonAsync($"{Ekstre}/satir/{satirId}/onayla",
                                     new SatirOnaylaDto { HesapKodu = hesapKodu, KisiYonlendir = kisiYonlendir }, ct));

        public Task<(EkstreSatirDto? Veri, string? Hata)> DigerBankadaAsync(int satirId, CancellationToken ct = default)
            => GonderAsync<EkstreSatirDto>(() =>
                _http.PutAsync($"{Ekstre}/satir/{satirId}/diger-bankada", content: null, ct));

        public Task<(DisaAktarimSonucDto? Veri, string? Hata)> DisaAktarAsync(int ekstreId, CancellationToken ct = default)
            => GonderAsync<DisaAktarimSonucDto>(() => _http.PostAsync($"{Ekstre}/{ekstreId}/disa-aktar", content: null, ct));

        /// <summary>
        /// Düzeltilmiş ekstre dosyası. JSON değil ikili içerik döner; hata gövdesi yine
        /// { field, message } sözleşmesiyle okunur.
        /// </summary>
        public Task<(string? DosyaAdi, byte[]? Icerik, string? Hata)> DuzeltilmisEkstreAsync(
            int ekstreId, CancellationToken ct = default)
            => DosyaIndirAsync(() => _http.PostAsync($"{Ekstre}/{ekstreId}/duzeltilmis-ekstre", content: null, ct),
                               $"ekstre-{ekstreId}-duzeltilmis.xlsx", "Düzeltilmiş ekstre üretilemedi.", ct);

        /// <summary>
        /// Analiz dökümü. "Kod listesi" ve "Düzeltilmiş ekstre"nin aksine eksik satır varken
        /// de üretilir; dosya ORKA'ya yüklenmez, yalnız inceleme içindir.
        /// </summary>
        public Task<(string? DosyaAdi, byte[]? Icerik, string? Hata)> AnalizDokumuAsync(
            int ekstreId, CancellationToken ct = default)
            => DosyaIndirAsync(() => _http.PostAsync($"{Ekstre}/{ekstreId}/analiz-dokumu", content: null, ct),
                               $"ekstre-{ekstreId}-analiz.xlsx", "Analiz dökümü üretilemedi.", ct);

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

        public async Task<HesapPlaniOzetDto> HesapPlaniOzetAsync(CancellationToken ct = default)
            => await GetOrNull<HesapPlaniOzetDto>($"{HesapPlani}/ozet", ct) ?? new();

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

        // ---- Öğrenilen eşleşmeler ----

        public async Task<List<HesapEslesmesiDto>> EslesmeleriAraAsync(string? q, int enFazla = 100,
                                                                      CancellationToken ct = default)
        {
            var url = $"{Eslesmeler}?enFazla={enFazla}";
            if (!string.IsNullOrWhiteSpace(q)) url += $"&q={Uri.EscapeDataString(q)}";

            return await GetOrNull<List<HesapEslesmesiDto>>(url, ct) ?? new();
        }

        public Task<(HesapEslesmesiDto? Veri, string? Hata)> EslesmeGuncelleAsync(int id, HesapEslesmesiYazDto dto,
                                                                                 CancellationToken ct = default)
            => GonderAsync<HesapEslesmesiDto>(() => _http.PutAsJsonAsync($"{Eslesmeler}/{id}", dto, ct));

        public async Task<string?> EslesmeSilAsync(int id, CancellationToken ct = default)
        {
            var (_, hata) = await GonderAsync<object>(() => _http.DeleteAsync($"{Eslesmeler}/{id}", ct));
            return hata;
        }

        // ---- Vergi kodları ----

        public async Task<List<VergiKoduEslemesiDto>> VergiKodlariAsync(CancellationToken ct = default)
            => await GetOrNull<List<VergiKoduEslemesiDto>>(VergiKodlari, ct) ?? new();

        public Task<(VergiKoduEslemesiDto? Veri, string? Hata)> VergiKoduEkleAsync(VergiKoduEslemesiYazDto dto,
                                                                                  CancellationToken ct = default)
            => GonderAsync<VergiKoduEslemesiDto>(() => _http.PostAsJsonAsync(VergiKodlari, dto, ct));

        public Task<(VergiKoduEslemesiDto? Veri, string? Hata)> VergiKoduGuncelleAsync(int id, VergiKoduEslemesiYazDto dto,
                                                                                      CancellationToken ct = default)
            => GonderAsync<VergiKoduEslemesiDto>(() => _http.PutAsJsonAsync($"{VergiKodlari}/{id}", dto, ct));

        public async Task<string?> VergiKoduSilAsync(int id, CancellationToken ct = default)
        {
            var (_, hata) = await GonderAsync<object>(() => _http.DeleteAsync($"{VergiKodlari}/{id}", ct));
            return hata;
        }

        // ---- Kişi yönlendirmeleri ----

        public async Task<List<KisiYonlendirmeDto>> KisiYonlendirmeleriAsync(CancellationToken ct = default)
            => await GetOrNull<List<KisiYonlendirmeDto>>(KisiYonlendirmeleri, ct) ?? new();

        public Task<(KisiYonlendirmeDto? Veri, string? Hata)> KisiYonlendirmeEkleAsync(KisiYonlendirmeYazDto dto,
                                                                                       CancellationToken ct = default)
            => GonderAsync<KisiYonlendirmeDto>(() => _http.PostAsJsonAsync(KisiYonlendirmeleri, dto, ct));

        public Task<(KisiYonlendirmeDto? Veri, string? Hata)> KisiYonlendirmeGuncelleAsync(int id, KisiYonlendirmeYazDto dto,
                                                                                           CancellationToken ct = default)
            => GonderAsync<KisiYonlendirmeDto>(() => _http.PutAsJsonAsync($"{KisiYonlendirmeleri}/{id}", dto, ct));

        public async Task<string?> KisiYonlendirmeSilAsync(int id, CancellationToken ct = default)
        {
            var (_, hata) = await GonderAsync<object>(() => _http.DeleteAsync($"{KisiYonlendirmeleri}/{id}", ct));
            return hata;
        }

        // ---- Sabit kurallar ----

        public async Task<List<SabitKuralDto>> SabitKurallarAsync(CancellationToken ct = default)
            => await GetOrNull<List<SabitKuralDto>>(SabitKurallar, ct) ?? new();

        public Task<(SabitKuralDto? Veri, string? Hata)> SabitKuralEkleAsync(SabitKuralYazDto dto,
                                                                            CancellationToken ct = default)
            => GonderAsync<SabitKuralDto>(() => _http.PostAsJsonAsync(SabitKurallar, dto, ct));

        public Task<(SabitKuralDto? Veri, string? Hata)> SabitKuralGuncelleAsync(int id, SabitKuralYazDto dto,
                                                                                CancellationToken ct = default)
            => GonderAsync<SabitKuralDto>(() => _http.PutAsJsonAsync($"{SabitKurallar}/{id}", dto, ct));

        public async Task<string?> SabitKuralSilAsync(int id, CancellationToken ct = default)
        {
            var (_, hata) = await GonderAsync<object>(() => _http.DeleteAsync($"{SabitKurallar}/{id}", ct));
            return hata;
        }

        // ---- Açıklama şablonları ----

        public async Task<List<AciklamaSablonuDto>> AciklamaSablonlariAsync(CancellationToken ct = default)
            => await GetOrNull<List<AciklamaSablonuDto>>(AciklamaSablonlari, ct) ?? new();

        public async Task<List<YerTutucuDto>> YerTutucularAsync(CancellationToken ct = default)
            => await GetOrNull<List<YerTutucuDto>>($"{AciklamaSablonlari}/yer-tutucular", ct) ?? new();

        public Task<(AciklamaSablonuDto? Veri, string? Hata)> AciklamaSablonuEkleAsync(AciklamaSablonuYazDto dto,
                                                                                      CancellationToken ct = default)
            => GonderAsync<AciklamaSablonuDto>(() => _http.PostAsJsonAsync(AciklamaSablonlari, dto, ct));

        public Task<(AciklamaSablonuDto? Veri, string? Hata)> AciklamaSablonuGuncelleAsync(int id, AciklamaSablonuYazDto dto,
                                                                                          CancellationToken ct = default)
            => GonderAsync<AciklamaSablonuDto>(() => _http.PutAsJsonAsync($"{AciklamaSablonlari}/{id}", dto, ct));

        public async Task<string?> AciklamaSablonuSilAsync(int id, CancellationToken ct = default)
        {
            var (_, hata) = await GonderAsync<object>(() => _http.DeleteAsync($"{AciklamaSablonlari}/{id}", ct));
            return hata;
        }

        // ---- Unvan çıkarma desenleri ----

        public async Task<List<UnvanDeseniDto>> UnvanDesenleriAsync(CancellationToken ct = default)
            => await GetOrNull<List<UnvanDeseniDto>>(UnvanDesenleri, ct) ?? new();

        /// <summary>
        /// Deneme geçersiz regex'te de 200 döner (sonucun içinde bildirilir); yalnız ağ/yetki
        /// hatasında null döner ve ekran sessiz kalır.
        /// </summary>
        public async Task<DesenDenemeSonucDto?> UnvanDeseniDeneAsync(DesenDenemeIstegiDto istek,
                                                                     CancellationToken ct = default)
        {
            var (veri, _) = await GonderAsync<DesenDenemeSonucDto>(
                () => _http.PostAsJsonAsync($"{UnvanDesenleri}/dene", istek, ct));

            return veri;
        }

        public Task<(UnvanDeseniDto? Veri, string? Hata)> UnvanDeseniEkleAsync(UnvanDeseniYazDto dto,
                                                                               CancellationToken ct = default)
            => GonderAsync<UnvanDeseniDto>(() => _http.PostAsJsonAsync(UnvanDesenleri, dto, ct));

        public Task<(UnvanDeseniDto? Veri, string? Hata)> UnvanDeseniGuncelleAsync(int id, UnvanDeseniYazDto dto,
                                                                                   CancellationToken ct = default)
            => GonderAsync<UnvanDeseniDto>(() => _http.PutAsJsonAsync($"{UnvanDesenleri}/{id}", dto, ct));

        public async Task<string?> UnvanDeseniSilAsync(int id, CancellationToken ct = default)
        {
            var (_, hata) = await GonderAsync<object>(() => _http.DeleteAsync($"{UnvanDesenleri}/{id}", ct));
            return hata;
        }

        // ---- Yardımcılar ----

        /// <summary>
        /// İkili içerik indirir (JSON değil). Hata gövdesi yine { field, message }
        /// sözleşmesiyle okunur; dosya adı Content-Disposition'dan, yoksa varsayılandan gelir.
        /// </summary>
        private static async Task<(string? DosyaAdi, byte[]? Icerik, string? Hata)> DosyaIndirAsync(
            Func<Task<HttpResponseMessage>> istek, string varsayilanAd, string hataMesaji, CancellationToken ct)
        {
            try
            {
                using var resp = await istek();

                if (!resp.IsSuccessStatusCode)
                {
                    var govde = await resp.Content.ReadAsStringAsync(ct);
                    return (null, null, MesajCoz(govde) ?? hataMesaji);
                }

                var ad = resp.Content.Headers.ContentDisposition?.FileNameStar
                         ?? resp.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                         ?? varsayilanAd;

                return (ad, await resp.Content.ReadAsByteArrayAsync(ct), null);
            }
            catch (Exception)
            {
                return (null, null, "Sunucuya ulaşılamadı. Bağlantınızı kontrol edip tekrar deneyin.");
            }
        }

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
