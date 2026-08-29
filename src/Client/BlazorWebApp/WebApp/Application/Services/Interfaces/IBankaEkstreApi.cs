using WebApp.Shared.Dto.BankaEkstre;

namespace WebApp.Application.Services.Interfaces
{
    /// <summary>Banka ekstresi işleme modülü istemcisi.</summary>
    public interface IBankaEkstreApi
    {
        /// <summary>
        /// Kapsamsız okuma: sunucu tüm firmaların kayıtlarını döner ve her satırda firma
        /// adı gelir. <b>Yalnız listelerde</b> kullanılır; yazma çağrılarına verilirse
        /// sunucu 400 döner (KARARLAR §99).
        /// </summary>
        public const int TumFirmalar = 0;

        /// <summary>
        /// Firma başına banka sayaçları (<c>catalog.Firmalar.Id</c> başına bir satır).
        ///
        /// Firma seçim ekranı için yazılmıştı; o ekran kaldırıldı (KARARLAR §99). Uç
        /// nokta duruyor: anasayfanın "onay bekleyen ekstre satırı" kartı aynı sayıları
        /// sunucu tarafında kullanıyor ve çok firmalı bir görünüm için doğru şekil bu.
        /// </summary>
        Task<List<FirmaBankaOzetiDto>> FirmaOzetleriAsync(IEnumerable<int> firmaIdler, CancellationToken ct = default);

        // ---- Veri temizliği (Tanımlar) ----

        /// <summary>Seçili firmada silinecek kayıt sayıları; onay diyaloğu bunu gösterir.</summary>
        Task<BankaTemizlikOzetiDto> TemizlikOzetiAsync(int firmaId, CancellationToken ct = default);

        /// <summary>Seçili firmanın banka otomasyon verisini siler.</summary>
        Task<(BankaTemizlikOzetiDto? Veri, string? Hata)> TemizleAsync(int firmaId, CancellationToken ct = default);

        /// <summary>
        /// Hiçbir firmaya bağlı olmayan eski kayıtların sayısı (tenant düzeninden kalanlar).
        /// Hiçbir firmanın ekranında görünmedikleri için başka türlü silinemezler.
        /// </summary>
        Task<BankaTemizlikOzetiDto> SahipsizOzetiAsync(int firmaId, CancellationToken ct = default);

        Task<(BankaTemizlikOzetiDto? Veri, string? Hata)> SahipsizTemizleAsync(int firmaId, CancellationToken ct = default);

        // Banka hesapları
        Task<List<BankaHesabiDto>> GetHesaplarAsync(int firmaId, bool pasifDahil = false, CancellationToken ct = default);

        /// <summary>
        /// Firmada kullanılan banka adları (+ hesap sayıları). Banka adı alanı serbest metin
        /// değil açılır listedir; kaynağı burasıdır.
        /// </summary>
        Task<List<BankaAdiDto>> BankaAdlariAsync(int firmaId, CancellationToken ct = default);

        /// <summary>
        /// Aynı bankanın farklı yazımlarını tek ada indirir; yanıtta kaç hesabın etkilendiği
        /// ve güncel ad listesi döner.
        /// </summary>
        Task<(BankaAdiBirlestirSonucDto? Veri, string? Hata)> BankaAdiBirlestirAsync(int firmaId, 
            BankaAdiBirlestirDto dto, CancellationToken ct = default);
        Task<List<ParserSecenekDto>> GetParserlerAsync(int firmaId, CancellationToken ct = default);

        /// <summary>Hesap adından eşleştirme anahtarı önerisi (yeni hesap formunu doldurur).</summary>
        Task<string?> AnahtarOnerisiAsync(int firmaId, string? hesapAdi, string? bankaAdi, CancellationToken ct = default);

        /// <summary>Firmanın hesap sahibi kimliği (unvan + diğer yazımlar).</summary>
        Task<HesapSahibiKimlikDto> HesapSahibiAsync(int firmaId, CancellationToken ct = default);

        /// <summary>Kimliği firmanın tüm banka hesaplarına yazar.</summary>
        Task<(HesapSahibiKimlikDto? Veri, string? Hata)> HesapSahibiKaydetAsync(int firmaId, HesapSahibiKimlikYazDto dto, CancellationToken ct = default);

        /// <summary>
        /// Hesap sahibinin henüz eklenmemiş yazımları; yüklenmiş ekstrelerden çıkarılır.
        /// "ADAY BAĞIMSIZ DENETİM VE SMMM A.Ş." gibi yazımlar ancak böyle bulunur.
        /// </summary>
        Task<List<HesapSahibiOnerisiDto>> HesapSahibiOnerileriAsync(int firmaId, CancellationToken ct = default);

        Task<(BankaHesabiDto? Veri, string? Hata)> CreateHesapAsync(int firmaId, BankaHesabiYazDto dto, CancellationToken ct = default);
        Task<(BankaHesabiDto? Veri, string? Hata)> UpdateHesapAsync(int firmaId, int id, BankaHesabiYazDto dto, CancellationToken ct = default);
        Task<string?> DeleteHesapAsync(int firmaId, int id, CancellationToken ct = default);

        /// <summary>Toplu içe aktarım (xlsx); anahtar ORKA hesap kodu + firma.</summary>
        Task<(BankaHesabiIceAktarimSonucDto? Veri, string? Hata)> HesaplariIceAktarAsync(int firmaId, Stream icerik, string dosyaAdi, CancellationToken ct = default);

        /// <summary>Doğru başlıklara sahip boş şablon.</summary>
        Task<(string? DosyaAdi, byte[]? Icerik, string? Hata)> HesapSablonuAsync(int firmaId, CancellationToken ct = default);

        // Ekstre
        Task<List<EkstreYuklemeDto>> GetYuklemelerAsync(int firmaId, CancellationToken ct = default);
        Task<EkstreYuklemeDto?> GetYuklemeAsync(int firmaId, int id, CancellationToken ct = default);
        Task<(EkstreYuklemeDto? Veri, string? Hata)> YukleAsync(int firmaId, int bankaHesabiId, Stream icerik, string dosyaAdi, CancellationToken ct = default);
        /// <param name="kategoriId">
        /// Dolu ise yalnız o işlem kategorisine düşen satırlar döner; onay ekranındaki
        /// kategori filtresi bunu kullanır.
        /// </param>
        Task<List<EkstreSatirDto>> GetSatirlarAsync(int firmaId, int ekstreId, SatirDurum? durum = null, int? kategoriId = null,
                                                    CancellationToken ct = default);
        /// <summary>
        /// Satırı onaylar. <paramref name="kisiYonlendir"/> true ise satırdaki kişi için
        /// kalıcı bir yönlendirme kaydı da oluşturulur.
        /// </summary>
        Task<(EkstreSatirDto? Veri, string? Hata)> OnaylaAsync(int firmaId, int satirId, string hesapKodu,
                                                               bool kisiYonlendir = false, CancellationToken ct = default);
        Task<(EkstreSatirDto? Veri, string? Hata)> DigerBankadaAsync(int firmaId, int satirId, CancellationToken ct = default);
        Task<(DisaAktarimSonucDto? Veri, string? Hata)> DisaAktarAsync(int firmaId, int ekstreId, CancellationToken ct = default);

        /// <summary>Dışa aktarımın birinci parçası: açıklama kolonu değiştirilmiş orijinal dosya.</summary>
        Task<(string? DosyaAdi, byte[]? Icerik, string? Hata)> DuzeltilmisEkstreAsync(int firmaId, int ekstreId, CancellationToken ct = default);

        /// <summary>
        /// Analiz dökümü: tüm satırlar, durumu ne olursa olsun. ORKA'ya yüklenmez; sistemin
        /// ne önerdiğini onaydan önce incelemek için.
        /// </summary>
        Task<(string? DosyaAdi, byte[]? Icerik, string? Hata)> AnalizDokumuAsync(int firmaId, int ekstreId, CancellationToken ct = default);

        Task<string?> SilAsync(int firmaId, int ekstreId, CancellationToken ct = default);

        // Hesap planı
        Task<List<HesapPlaniKaydiDto>> HesapPlaniAraAsync(int firmaId, string? q, string? anaGrup = null, int enFazla = 20, CancellationToken ct = default);
        Task<int> HesapPlaniSayisiAsync(int firmaId, CancellationToken ct = default);
        Task<HesapPlaniOzetDto> HesapPlaniOzetAsync(int firmaId, CancellationToken ct = default);
        Task<(HesapPlaniIceAktarimSonucDto? Veri, string? Hata)> HesapPlaniIceAktarAsync(int firmaId, Stream icerik, string dosyaAdi, CancellationToken ct = default);

        // Öğrenilen eşleşmeler
        Task<List<HesapEslesmesiDto>> EslesmeleriAraAsync(int firmaId, string? q, int enFazla = 100, CancellationToken ct = default);
        Task<(HesapEslesmesiDto? Veri, string? Hata)> EslesmeGuncelleAsync(int firmaId, int id, HesapEslesmesiYazDto dto, CancellationToken ct = default);
        Task<string?> EslesmeSilAsync(int firmaId, int id, CancellationToken ct = default);

        /// <summary>
        /// ORKA yevmiyesinden çıkarılmış doğrulanmış eşleşmelerin toplu içe aktarımı.
        /// Mevcut kayıt korunur; onay ekranından verilen karar önceliklidir.
        /// </summary>
        Task<(OgrenilenEslesmeIceAktarimSonucDto? Veri, string? Hata)> EslesmeleriIceAktarAsync(int firmaId, 
            Stream icerik, string dosyaAdi, CancellationToken ct = default);

        Task<(string? DosyaAdi, byte[]? Icerik, string? Hata)> EslesmeSablonuAsync(int firmaId, CancellationToken ct = default);

        // Vergi kodu eşlemeleri
        Task<List<VergiKoduEslemesiDto>> VergiKodlariAsync(int firmaId, CancellationToken ct = default);
        Task<(VergiKoduEslemesiDto? Veri, string? Hata)> VergiKoduEkleAsync(int firmaId, VergiKoduEslemesiYazDto dto, CancellationToken ct = default);
        Task<(VergiKoduEslemesiDto? Veri, string? Hata)> VergiKoduGuncelleAsync(int firmaId, int id, VergiKoduEslemesiYazDto dto, CancellationToken ct = default);
        Task<string?> VergiKoduSilAsync(int firmaId, int id, CancellationToken ct = default);

        // Sabit kurallar (işlem tipi / açıklama → hesap kodu)
        Task<List<SabitKuralDto>> SabitKurallarAsync(int firmaId, CancellationToken ct = default);
        Task<(SabitKuralDto? Veri, string? Hata)> SabitKuralEkleAsync(int firmaId, SabitKuralYazDto dto, CancellationToken ct = default);
        Task<(SabitKuralDto? Veri, string? Hata)> SabitKuralGuncelleAsync(int firmaId, int id, SabitKuralYazDto dto, CancellationToken ct = default);
        Task<string?> SabitKuralSilAsync(int firmaId, int id, CancellationToken ct = default);

        // Açıklama şablonları
        Task<List<AciklamaSablonuDto>> AciklamaSablonlariAsync(CancellationToken ct = default);

        /// <summary>Şablonda kullanılabilecek yer tutucular; ekranda liste olarak gösterilir.</summary>
        Task<List<YerTutucuDto>> YerTutucularAsync(CancellationToken ct = default);

        Task<(AciklamaSablonuDto? Veri, string? Hata)> AciklamaSablonuEkleAsync(AciklamaSablonuYazDto dto, CancellationToken ct = default);
        Task<(AciklamaSablonuDto? Veri, string? Hata)> AciklamaSablonuGuncelleAsync(int id, AciklamaSablonuYazDto dto, CancellationToken ct = default);
        Task<string?> AciklamaSablonuSilAsync(int id, CancellationToken ct = default);

        // Unvan çıkarma desenleri
        Task<List<UnvanDeseniDto>> UnvanDesenleriAsync(CancellationToken ct = default);

        /// <summary>Deseni kaydetmeden dener: verilen metinde ne yakalıyor?</summary>
        Task<DesenDenemeSonucDto?> UnvanDeseniDeneAsync(DesenDenemeIstegiDto istek, CancellationToken ct = default);

        Task<(UnvanDeseniDto? Veri, string? Hata)> UnvanDeseniEkleAsync(UnvanDeseniYazDto dto, CancellationToken ct = default);
        Task<(UnvanDeseniDto? Veri, string? Hata)> UnvanDeseniGuncelleAsync(int id, UnvanDeseniYazDto dto, CancellationToken ct = default);
        Task<string?> UnvanDeseniSilAsync(int id, CancellationToken ct = default);

        // İşlem kategorileri (kuralların muhasebe sınıflandırması)
        Task<List<IslemKategorisiDto>> IslemKategorileriAsync(int firmaId, CancellationToken ct = default);

        /// <summary>Kategoriler görünümü: bankanın kuralları kategorilere dağıtılmış hâlde.</summary>
        Task<KategoriKapsamOzetiDto> KategoriKapsamiAsync(int firmaId, string? parserTipi, CancellationToken ct = default);

        Task<(IslemKategorisiDto? Veri, string? Hata)> IslemKategorisiEkleAsync(int firmaId, IslemKategorisiYazDto dto, CancellationToken ct = default);
        Task<(IslemKategorisiDto? Veri, string? Hata)> IslemKategorisiGuncelleAsync(int firmaId, int id, IslemKategorisiYazDto dto, CancellationToken ct = default);
        Task<string?> IslemKategorisiSilAsync(int firmaId, int id, CancellationToken ct = default);

        // Kişi yönlendirmeleri
        Task<List<KisiYonlendirmeDto>> KisiYonlendirmeleriAsync(int firmaId, CancellationToken ct = default);
        Task<(KisiYonlendirmeDto? Veri, string? Hata)> KisiYonlendirmeEkleAsync(int firmaId, KisiYonlendirmeYazDto dto, CancellationToken ct = default);
        Task<(KisiYonlendirmeDto? Veri, string? Hata)> KisiYonlendirmeGuncelleAsync(int firmaId, int id, KisiYonlendirmeYazDto dto, CancellationToken ct = default);
        Task<string?> KisiYonlendirmeSilAsync(int firmaId, int id, CancellationToken ct = default);
    }
}
