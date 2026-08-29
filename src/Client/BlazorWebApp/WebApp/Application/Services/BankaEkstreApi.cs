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
    ///
    /// FİRMA KAPSAMI HER ÇAĞRIDA PARAMETREDİR (<c>firmaId</c>) ve adrese <c>?firmaId=</c>
    /// olarak eklenir. İstemcinin sakladığı bir "aktif firma" YOKTUR (KARARLAR §99):
    /// kapsamı çağıran verir — listede kullanıcının seçtiği filtre, yazmada kaydın kendi
    /// firması. <see cref="IBankaEkstreApi.TumFirmalar"/> (0) verilirse parametre hiç
    /// gönderilmez ve sunucu tüm firmaları döner; bu yalnız okumada geçerlidir.
    ///
    /// Modül daha önce kapsamı iki kez yanlış yerden aldı: önce token'daki tenant
    /// claim'inden (sekiz firma aynı kovaya yazıldı, KARARLAR §68), sonra oturumda tutulan
    /// "girilen firma"dan (her işlem için firmaya girmek gerekti, §99). Kapsamın çağrı
    /// noktasında görünür olması ikisini de kapatıyor.
    ///
    /// Çok parçalı (dosya) isteklerde de sorgu dizesi kullanılır: sunucudaki filtre form
    /// gövdesini okumaz, 20 MB'lik yüklemeyi model bağlamadan önce tamponlamamak için.
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
        private const string IslemKategorileri = "/catalog/banka-ekstre/islem-kategorileri";

        private const string XlsxTuru = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private const string Temizlik = "/catalog/banka-ekstre/temizlik";

        private readonly HttpClient _http;

        public BankaEkstreApi(HttpClient http) => _http = http;

        /// <summary>
        /// Firma kapsamlı adres. Kapsam <b>çağrıdan</b> gelir, saklanan bir "aktif firma"dan
        /// değil (KARARLAR §99): listelerde kullanıcının seçtiği filtre, yazmada kaydın
        /// firması. <see cref="IBankaEkstreApi.TumFirmalar"/> verilirse <c>firmaId</c> hiç
        /// gönderilmez ve sunucu tüm firmaları döner — bu yalnız okumada geçerlidir,
        /// yazmada sunucu 400 döndürür.
        /// </summary>
        private static string Adres(int firmaId, string yol, params string[] parametreler)
        {
            var hepsi = new List<string>();
            if (firmaId > 0) hepsi.Add($"firmaId={firmaId}");
            hepsi.AddRange(parametreler.Where(p => !string.IsNullOrWhiteSpace(p)));

            if (hepsi.Count == 0) return yol;

            var ayrac = yol.Contains('?') ? "&" : "?";
            return yol + ayrac + string.Join("&", hepsi);
        }

        // ---- Firma seçim ekranı ----

        /// <summary>
        /// Firma seçim ekranının sayaçları. Kapsamsız tek uç nokta: ekran firmaya
        /// <b>girilmeden önce</b> açılıyor ve doğası gereği birçok firmayı soruyor.
        /// </summary>
        public async Task<List<FirmaBankaOzetiDto>> FirmaOzetleriAsync(IEnumerable<int> firmaIdler,
                                                                       CancellationToken ct = default)
        {
            var liste = (firmaIdler ?? Enumerable.Empty<int>()).Where(id => id > 0).Distinct().ToList();
            if (liste.Count == 0) return new List<FirmaBankaOzetiDto>();

            var sorgu = string.Join("&", liste.Select(id => $"firmaIdler={id}"));
            return await GetOrNull<List<FirmaBankaOzetiDto>>($"{Firmalar}/ozet?{sorgu}", ct) ?? new();
        }

        // ---- Veri temizliği ----

        public async Task<BankaTemizlikOzetiDto> TemizlikOzetiAsync(int firmaId, CancellationToken ct = default)
            => await GetOrNull<BankaTemizlikOzetiDto>(Adres(firmaId, $"{Temizlik}/ozet"), ct) ?? new();

        public Task<(BankaTemizlikOzetiDto? Veri, string? Hata)> TemizleAsync(int firmaId, CancellationToken ct = default)
            => GonderAsync<BankaTemizlikOzetiDto>(() => _http.DeleteAsync(Adres(firmaId, Temizlik), ct));

        public async Task<BankaTemizlikOzetiDto> SahipsizOzetiAsync(int firmaId, CancellationToken ct = default)
            => await GetOrNull<BankaTemizlikOzetiDto>(Adres(firmaId, $"{Temizlik}/sahipsiz/ozet"), ct) ?? new();

        public Task<(BankaTemizlikOzetiDto? Veri, string? Hata)> SahipsizTemizleAsync(int firmaId, CancellationToken ct = default)
            => GonderAsync<BankaTemizlikOzetiDto>(() => _http.DeleteAsync(Adres(firmaId, $"{Temizlik}/sahipsiz"), ct));

        // ---- Banka hesapları ----

        public async Task<List<BankaHesabiDto>> GetHesaplarAsync(int firmaId, bool pasifDahil = false, CancellationToken ct = default)
            => await GetOrNull<List<BankaHesabiDto>>(Adres(firmaId, Hesaplar, $"pasifDahil={pasifDahil.ToString().ToLowerInvariant()}"), ct) ?? new();

        public async Task<List<BankaAdiDto>> BankaAdlariAsync(int firmaId, CancellationToken ct = default)
            => await GetOrNull<List<BankaAdiDto>>(Adres(firmaId, $"{Hesaplar}/banka-adlari"), ct) ?? new();

        public Task<(BankaAdiBirlestirSonucDto? Veri, string? Hata)> BankaAdiBirlestirAsync(int firmaId, 
            BankaAdiBirlestirDto dto, CancellationToken ct = default)
            => GonderAsync<BankaAdiBirlestirSonucDto>(
                () => _http.PostAsJsonAsync(Adres(firmaId, $"{Hesaplar}/banka-adi-birlestir"), dto, ct));

        public async Task<List<ParserSecenekDto>> GetParserlerAsync(int firmaId, CancellationToken ct = default)
            => await GetOrNull<List<ParserSecenekDto>>(Adres(firmaId, $"{Hesaplar}/parserler"), ct) ?? new();

        public async Task<string?> AnahtarOnerisiAsync(int firmaId, string? hesapAdi, string? bankaAdi, CancellationToken ct = default)
        {
            var adres = Adres(firmaId, $"{Hesaplar}/anahtar-onerisi",
                              $"hesapAdi={Uri.EscapeDataString(hesapAdi ?? string.Empty)}",
                              $"bankaAdi={Uri.EscapeDataString(bankaAdi ?? string.Empty)}");

            return (await GetOrNull<AnahtarOnerisiDto>(adres, ct))?.EslestirmeAnahtarlari;
        }

        public async Task<HesapSahibiKimlikDto> HesapSahibiAsync(int firmaId, CancellationToken ct = default)
            => await GetOrNull<HesapSahibiKimlikDto>(Adres(firmaId, $"{Hesaplar}/hesap-sahibi"), ct) ?? new();

        public Task<(HesapSahibiKimlikDto? Veri, string? Hata)> HesapSahibiKaydetAsync(int firmaId, HesapSahibiKimlikYazDto dto,
                                                                                       CancellationToken ct = default)
            => GonderAsync<HesapSahibiKimlikDto>(() => _http.PutAsJsonAsync(Adres(firmaId, $"{Hesaplar}/hesap-sahibi"), dto, ct));

        public async Task<List<HesapSahibiOnerisiDto>> HesapSahibiOnerileriAsync(int firmaId, CancellationToken ct = default)
            => await GetOrNull<List<HesapSahibiOnerisiDto>>(Adres(firmaId, $"{Hesaplar}/hesap-sahibi-onerileri"), ct) ?? new();

        public Task<(BankaHesabiDto? Veri, string? Hata)> CreateHesapAsync(int firmaId, BankaHesabiYazDto dto, CancellationToken ct = default)
            => GonderAsync<BankaHesabiDto>(() => _http.PostAsJsonAsync(Adres(firmaId, Hesaplar), dto, ct));

        public Task<(BankaHesabiDto? Veri, string? Hata)> UpdateHesapAsync(int firmaId, int id, BankaHesabiYazDto dto, CancellationToken ct = default)
            => GonderAsync<BankaHesabiDto>(() => _http.PutAsJsonAsync(Adres(firmaId, $"{Hesaplar}/{id}"), dto, ct));

        public async Task<string?> DeleteHesapAsync(int firmaId, int id, CancellationToken ct = default)
        {
            var (_, hata) = await GonderAsync<object>(() => _http.DeleteAsync(Adres(firmaId, $"{Hesaplar}/{id}"), ct));
            return hata;
        }

        public Task<(BankaHesabiIceAktarimSonucDto? Veri, string? Hata)> HesaplariIceAktarAsync(int firmaId, 
            Stream icerik, string dosyaAdi, CancellationToken ct = default)
            => GonderAsync<BankaHesabiIceAktarimSonucDto>(() =>
            {
                var form = new MultipartFormDataContent();
                var dosya = new StreamContent(icerik);
                dosya.Headers.ContentType = new MediaTypeHeaderValue(XlsxTuru);
                form.Add(dosya, "file", dosyaAdi);

                return _http.PostAsync(Adres(firmaId, $"{Hesaplar}/ice-aktar"), form, ct);
            });

        public Task<(string? DosyaAdi, byte[]? Icerik, string? Hata)> HesapSablonuAsync(int firmaId, CancellationToken ct = default)
            => DosyaIndirAsync(() => _http.GetAsync(Adres(firmaId, $"{Hesaplar}/sablon"), ct), "banka-hesaplari-sablon.xlsx",
                               "Şablon indirilemedi.", ct);

        // ---- Ekstre ----

        public async Task<List<EkstreYuklemeDto>> GetYuklemelerAsync(int firmaId, CancellationToken ct = default)
            => await GetOrNull<List<EkstreYuklemeDto>>(Adres(firmaId, Ekstre), ct) ?? new();

        public Task<EkstreYuklemeDto?> GetYuklemeAsync(int firmaId, int id, CancellationToken ct = default)
            => GetOrNull<EkstreYuklemeDto>(Adres(firmaId, $"{Ekstre}/{id}"), ct);

        public Task<(EkstreYuklemeDto? Veri, string? Hata)> YukleAsync(int firmaId, int bankaHesabiId, Stream icerik, string dosyaAdi,
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

                return _http.PostAsync(Adres(firmaId, $"{Ekstre}/yukle"), form, ct);
            });

        public async Task<List<EkstreSatirDto>> GetSatirlarAsync(int firmaId, int ekstreId, SatirDurum? durum = null,
                                                                  int? kategoriId = null, CancellationToken ct = default)
        {
            var url = Adres(firmaId, $"{Ekstre}/{ekstreId}/satirlar",
                            durum is SatirDurum d ? $"durum={(byte)d}" : string.Empty,
                            kategoriId is int k ? $"kategoriId={k}" : string.Empty);

            return await GetOrNull<List<EkstreSatirDto>>(url, ct) ?? new();
        }

        public Task<(EkstreSatirDto? Veri, string? Hata)> OnaylaAsync(int firmaId, int satirId, string hesapKodu,
                                                                      bool kisiYonlendir = false, CancellationToken ct = default)
            => GonderAsync<EkstreSatirDto>(() =>
                _http.PutAsJsonAsync(Adres(firmaId, $"{Ekstre}/satir/{satirId}/onayla"),
                                     new SatirOnaylaDto { HesapKodu = hesapKodu, KisiYonlendir = kisiYonlendir }, ct));

        public Task<(EkstreSatirDto? Veri, string? Hata)> DigerBankadaAsync(int firmaId, int satirId, CancellationToken ct = default)
            => GonderAsync<EkstreSatirDto>(() =>
                _http.PutAsync(Adres(firmaId, $"{Ekstre}/satir/{satirId}/diger-bankada"), content: null, ct));

        public Task<(DisaAktarimSonucDto? Veri, string? Hata)> DisaAktarAsync(int firmaId, int ekstreId, CancellationToken ct = default)
            => GonderAsync<DisaAktarimSonucDto>(() => _http.PostAsync(Adres(firmaId, $"{Ekstre}/{ekstreId}/disa-aktar"), content: null, ct));

        /// <summary>
        /// Düzeltilmiş ekstre dosyası. JSON değil ikili içerik döner; hata gövdesi yine
        /// { field, message } sözleşmesiyle okunur.
        /// </summary>
        public Task<(string? DosyaAdi, byte[]? Icerik, string? Hata)> DuzeltilmisEkstreAsync(int firmaId, 
            int ekstreId, CancellationToken ct = default)
            => DosyaIndirAsync(() => _http.PostAsync(Adres(firmaId, $"{Ekstre}/{ekstreId}/duzeltilmis-ekstre"), content: null, ct),
                               $"ekstre-{ekstreId}-duzeltilmis.xlsx", "Düzeltilmiş ekstre üretilemedi.", ct);

        /// <summary>
        /// Analiz dökümü. "Kod listesi" ve "Düzeltilmiş ekstre"nin aksine eksik satır varken
        /// de üretilir; dosya ORKA'ya yüklenmez, yalnız inceleme içindir.
        /// </summary>
        public Task<(string? DosyaAdi, byte[]? Icerik, string? Hata)> AnalizDokumuAsync(int firmaId, 
            int ekstreId, CancellationToken ct = default)
            => DosyaIndirAsync(() => _http.PostAsync(Adres(firmaId, $"{Ekstre}/{ekstreId}/analiz-dokumu"), content: null, ct),
                               $"ekstre-{ekstreId}-analiz.xlsx", "Analiz dökümü üretilemedi.", ct);

        public async Task<string?> SilAsync(int firmaId, int ekstreId, CancellationToken ct = default)
        {
            var (_, hata) = await GonderAsync<object>(() => _http.DeleteAsync(Adres(firmaId, $"{Ekstre}/{ekstreId}"), ct));
            return hata;
        }

        // ---- Hesap planı ----

        public async Task<List<HesapPlaniKaydiDto>> HesapPlaniAraAsync(int firmaId, string? q, string? anaGrup = null, int enFazla = 20,
                                                                      CancellationToken ct = default)
        {
            var parametreler = new List<string> { $"enFazla={enFazla}" };
            if (!string.IsNullOrWhiteSpace(q)) parametreler.Add($"q={Uri.EscapeDataString(q)}");
            if (!string.IsNullOrWhiteSpace(anaGrup)) parametreler.Add($"anaGrup={Uri.EscapeDataString(anaGrup)}");

            return await GetOrNull<List<HesapPlaniKaydiDto>>(Adres(firmaId, HesapPlani, parametreler.ToArray()), ct) ?? new();
        }

        public async Task<int> HesapPlaniSayisiAsync(int firmaId, CancellationToken ct = default)
            => await GetOrNull<int?>(Adres(firmaId, $"{HesapPlani}/sayi"), ct) ?? 0;

        public async Task<HesapPlaniOzetDto> HesapPlaniOzetAsync(int firmaId, CancellationToken ct = default)
            => await GetOrNull<HesapPlaniOzetDto>(Adres(firmaId, $"{HesapPlani}/ozet"), ct) ?? new();

        public Task<(HesapPlaniIceAktarimSonucDto? Veri, string? Hata)> HesapPlaniIceAktarAsync(int firmaId, 
            Stream icerik, string dosyaAdi, CancellationToken ct = default)
            => GonderAsync<HesapPlaniIceAktarimSonucDto>(() =>
            {
                var form = new MultipartFormDataContent();
                var dosya = new StreamContent(icerik);
                dosya.Headers.ContentType = new MediaTypeHeaderValue(XlsxTuru);
                form.Add(dosya, "file", dosyaAdi);

                return _http.PostAsync(Adres(firmaId, $"{HesapPlani}/ice-aktar"), form, ct);
            });

        // ---- Öğrenilen eşleşmeler ----

        public async Task<List<HesapEslesmesiDto>> EslesmeleriAraAsync(int firmaId, string? q, int enFazla = 100,
                                                                      CancellationToken ct = default)
        {
            var parametreler = new List<string> { $"enFazla={enFazla}" };
            if (!string.IsNullOrWhiteSpace(q)) parametreler.Add($"q={Uri.EscapeDataString(q)}");

            return await GetOrNull<List<HesapEslesmesiDto>>(Adres(firmaId, Eslesmeler, parametreler.ToArray()), ct) ?? new();
        }

        public Task<(HesapEslesmesiDto? Veri, string? Hata)> EslesmeGuncelleAsync(int firmaId, int id, HesapEslesmesiYazDto dto,
                                                                                 CancellationToken ct = default)
            => GonderAsync<HesapEslesmesiDto>(() => _http.PutAsJsonAsync(Adres(firmaId, $"{Eslesmeler}/{id}"), dto, ct));

        public async Task<string?> EslesmeSilAsync(int firmaId, int id, CancellationToken ct = default)
        {
            var (_, hata) = await GonderAsync<object>(() => _http.DeleteAsync(Adres(firmaId, $"{Eslesmeler}/{id}"), ct));
            return hata;
        }

        public Task<(OgrenilenEslesmeIceAktarimSonucDto? Veri, string? Hata)> EslesmeleriIceAktarAsync(int firmaId, 
            Stream icerik, string dosyaAdi, CancellationToken ct = default)
            => GonderAsync<OgrenilenEslesmeIceAktarimSonucDto>(() =>
            {
                var form = new MultipartFormDataContent();
                var dosya = new StreamContent(icerik);
                dosya.Headers.ContentType = new MediaTypeHeaderValue(XlsxTuru);
                form.Add(dosya, "file", dosyaAdi);

                return _http.PostAsync(Adres(firmaId, $"{Eslesmeler}/ice-aktar"), form, ct);
            });

        public Task<(string? DosyaAdi, byte[]? Icerik, string? Hata)> EslesmeSablonuAsync(int firmaId, CancellationToken ct = default)
            => DosyaIndirAsync(() => _http.GetAsync(Adres(firmaId, $"{Eslesmeler}/sablon"), ct),
                               "ogrenilen-eslesmeler-sablon.xlsx", "Şablon indirilemedi.", ct);

        // ---- Vergi kodları ----

        public async Task<List<VergiKoduEslemesiDto>> VergiKodlariAsync(int firmaId, CancellationToken ct = default)
            => await GetOrNull<List<VergiKoduEslemesiDto>>(Adres(firmaId, VergiKodlari), ct) ?? new();

        public Task<(VergiKoduEslemesiDto? Veri, string? Hata)> VergiKoduEkleAsync(int firmaId, VergiKoduEslemesiYazDto dto,
                                                                                  CancellationToken ct = default)
            => GonderAsync<VergiKoduEslemesiDto>(() => _http.PostAsJsonAsync(Adres(firmaId, VergiKodlari), dto, ct));

        public Task<(VergiKoduEslemesiDto? Veri, string? Hata)> VergiKoduGuncelleAsync(int firmaId, int id, VergiKoduEslemesiYazDto dto,
                                                                                      CancellationToken ct = default)
            => GonderAsync<VergiKoduEslemesiDto>(() => _http.PutAsJsonAsync(Adres(firmaId, $"{VergiKodlari}/{id}"), dto, ct));

        public async Task<string?> VergiKoduSilAsync(int firmaId, int id, CancellationToken ct = default)
        {
            var (_, hata) = await GonderAsync<object>(() => _http.DeleteAsync(Adres(firmaId, $"{VergiKodlari}/{id}"), ct));
            return hata;
        }

        // ---- İşlem kategorileri ----

        public async Task<List<IslemKategorisiDto>> IslemKategorileriAsync(int firmaId, CancellationToken ct = default)
            => await GetOrNull<List<IslemKategorisiDto>>(Adres(firmaId, IslemKategorileri), ct) ?? new();

        public async Task<KategoriKapsamOzetiDto> KategoriKapsamiAsync(int firmaId, string? parserTipi, CancellationToken ct = default)
        {
            var url = Adres(firmaId, $"{IslemKategorileri}/kapsam",
                            string.IsNullOrWhiteSpace(parserTipi) ? string.Empty : $"parserTipi={Uri.EscapeDataString(parserTipi)}");

            return await GetOrNull<KategoriKapsamOzetiDto>(url, ct) ?? new();
        }

        public Task<(IslemKategorisiDto? Veri, string? Hata)> IslemKategorisiEkleAsync(int firmaId, IslemKategorisiYazDto dto,
                                                                                       CancellationToken ct = default)
            => GonderAsync<IslemKategorisiDto>(() => _http.PostAsJsonAsync(Adres(firmaId, IslemKategorileri), dto, ct));

        public Task<(IslemKategorisiDto? Veri, string? Hata)> IslemKategorisiGuncelleAsync(int firmaId, int id, IslemKategorisiYazDto dto,
                                                                                           CancellationToken ct = default)
            => GonderAsync<IslemKategorisiDto>(() => _http.PutAsJsonAsync(Adres(firmaId, $"{IslemKategorileri}/{id}"), dto, ct));

        public async Task<string?> IslemKategorisiSilAsync(int firmaId, int id, CancellationToken ct = default)
        {
            var (_, hata) = await GonderAsync<object>(() => _http.DeleteAsync(Adres(firmaId, $"{IslemKategorileri}/{id}"), ct));
            return hata;
        }

        // ---- Kişi yönlendirmeleri ----

        public async Task<List<KisiYonlendirmeDto>> KisiYonlendirmeleriAsync(int firmaId, CancellationToken ct = default)
            => await GetOrNull<List<KisiYonlendirmeDto>>(Adres(firmaId, KisiYonlendirmeleri), ct) ?? new();

        public Task<(KisiYonlendirmeDto? Veri, string? Hata)> KisiYonlendirmeEkleAsync(int firmaId, KisiYonlendirmeYazDto dto,
                                                                                       CancellationToken ct = default)
            => GonderAsync<KisiYonlendirmeDto>(() => _http.PostAsJsonAsync(Adres(firmaId, KisiYonlendirmeleri), dto, ct));

        public Task<(KisiYonlendirmeDto? Veri, string? Hata)> KisiYonlendirmeGuncelleAsync(int firmaId, int id, KisiYonlendirmeYazDto dto,
                                                                                           CancellationToken ct = default)
            => GonderAsync<KisiYonlendirmeDto>(() => _http.PutAsJsonAsync(Adres(firmaId, $"{KisiYonlendirmeleri}/{id}"), dto, ct));

        public async Task<string?> KisiYonlendirmeSilAsync(int firmaId, int id, CancellationToken ct = default)
        {
            var (_, hata) = await GonderAsync<object>(() => _http.DeleteAsync(Adres(firmaId, $"{KisiYonlendirmeleri}/{id}"), ct));
            return hata;
        }

        // ---- Sabit kurallar ----

        public async Task<List<SabitKuralDto>> SabitKurallarAsync(int firmaId, CancellationToken ct = default)
            => await GetOrNull<List<SabitKuralDto>>(Adres(firmaId, SabitKurallar), ct) ?? new();

        public Task<(SabitKuralDto? Veri, string? Hata)> SabitKuralEkleAsync(int firmaId, SabitKuralYazDto dto,
                                                                            CancellationToken ct = default)
            => GonderAsync<SabitKuralDto>(() => _http.PostAsJsonAsync(Adres(firmaId, SabitKurallar), dto, ct));

        public Task<(SabitKuralDto? Veri, string? Hata)> SabitKuralGuncelleAsync(int firmaId, int id, SabitKuralYazDto dto,
                                                                                CancellationToken ct = default)
            => GonderAsync<SabitKuralDto>(() => _http.PutAsJsonAsync(Adres(firmaId, $"{SabitKurallar}/{id}"), dto, ct));

        public async Task<string?> SabitKuralSilAsync(int firmaId, int id, CancellationToken ct = default)
        {
            var (_, hata) = await GonderAsync<object>(() => _http.DeleteAsync(Adres(firmaId, $"{SabitKurallar}/{id}"), ct));
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
