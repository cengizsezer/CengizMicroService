using CatalogService.Api.Features.BankaEkstre.Domain;

namespace CatalogService.Api.Features.BankaEkstre.Dtos
{
    // ---- Banka hesabı ----

    public class BankaHesabiDto
    {
        public int Id { get; set; }
        public string BankaAdi { get; set; } = string.Empty;

        /// <summary>Hesabın ORKA'daki adı, ör. "VAKIFBANK VADESIZ TL".</summary>
        public string? HesapAdi { get; set; }

        /// <summary>Virgülle ayrılmış ayırt edici anahtarlar, ör. "Otomatik Süpürme, Süpürme".</summary>
        public string? EslestirmeAnahtarlari { get; set; }

        /// <summary>
        /// Hesap sahibinin (firmanın) kendi resmî unvanı. Açıklamada geçtiğinde karşı taraf
        /// sanılmasın diye kullanılır; firma bazlı, tek kez girilir.
        /// </summary>
        public string? HesapSahibiUnvani { get; set; }

        /// <summary>Hesap sahibinin diğer yazımları, satır satır.</summary>
        public string? HesapSahibiTakmaAdlari { get; set; }

        public HesapTipi HesapTipi { get; set; }
        public string ParaBirimi { get; set; } = "TRY";
        public string? Iban { get; set; }
        public string OrkaHesapKodu { get; set; } = string.Empty;
        public string ParserTipi { get; set; } = string.Empty;
        public bool Aktif { get; set; }

        /// <summary>IBAN öğrenme katmanı (varsayılan kapalı).</summary>
        public bool IbanKatmaniAktif { get; set; }

        /// <summary>VKN öğrenme katmanı (varsayılan kapalı; Vakıfbank'ta VKN hesap sahibinin).</summary>
        public bool VknKatmaniAktif { get; set; }
    }

    public class BankaHesabiYazDto
    {
        public string BankaAdi { get; set; } = string.Empty;
        public string? HesapAdi { get; set; }
        public string? EslestirmeAnahtarlari { get; set; }
        public string? HesapSahibiUnvani { get; set; }
        public string? HesapSahibiTakmaAdlari { get; set; }
        public HesapTipi HesapTipi { get; set; } = HesapTipi.Vadesiz;
        public string ParaBirimi { get; set; } = "TRY";
        public string? Iban { get; set; }
        public string OrkaHesapKodu { get; set; } = string.Empty;
        public string ParserTipi { get; set; } = string.Empty;
        public bool Aktif { get; set; } = true;
        public bool IbanKatmaniAktif { get; set; }
        public bool VknKatmaniAktif { get; set; }
    }

    /// <summary>
    /// Toplu banka hesabı içe aktarımının satır bazlı sonucu. Hata bir satırı düşürür,
    /// dosyanın tamamını değil; uyarı satırı düşürmez, yalnız kullanıcıyı uyarır.
    /// Alan adları mevcut <c>{ field, message }</c> hata sözleşmesiyle aynıdır.
    /// </summary>
    public class IceAktarimSatirSorunuDto
    {
        /// <summary>Excel'deki 1 tabanlı satır numarası; kullanıcı dosyada bulabilsin.</summary>
        public int SatirNo { get; set; }

        public string Field { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class BankaHesabiIceAktarimSonucDto
    {
        public int Okunan { get; set; }
        public int Eklenen { get; set; }
        public int Guncellenen { get; set; }

        /// <summary>Hatalı olduğu için işlenmeyen satır sayısı.</summary>
        public int Atlanan { get; set; }

        public List<IceAktarimSatirSorunuDto> Hatalar { get; set; } = new();

        /// <summary>Satırı düşürmeyen sorunlar (ör. 102 ile başlamayan kod, boş ayrıştırıcı).</summary>
        public List<IceAktarimSatirSorunuDto> Uyarilar { get; set; } = new();
    }

    /// <summary>Hesap adından üretilen eşleştirme anahtarı önerisi.</summary>
    public class AnahtarOnerisiDto
    {
        public string? EslestirmeAnahtarlari { get; set; }
    }

    /// <summary>
    /// Yüklenmiş ekstrelerin açıklamalarında geçen, hesap sahibinin tanımlı yazımlarına
    /// benzeyen ama henüz eklenmemiş yazımlar. "ADAY BAĞIMSIZ DENETİM VE SMMM A.Ş." gibi
    /// yazımlar ancak böyle bulunur; kullanıcı tek tıkla takma adlara ekler.
    /// </summary>
    public class HesapSahibiOnerisiDto
    {
        public string Yazim { get; set; } = string.Empty;

        /// <summary>Yüklenmiş ekstrelerde kaç satırda geçtiği; sık geçen yazım önce gelir.</summary>
        public int Adet { get; set; }
    }

    /// <summary>
    /// Hesap sahibinin (firmanın) kimliği. Alan <see cref="BankaHesabi"/> satırlarında
    /// duruyor ama anlamı <b>firma bazlı</b>: aynı unvan her banka hesabına ayrı ayrı
    /// yazılmaz. Firma Tanımları ekranı bu tek kayıt üzerinden yönetir; yazma işlemi
    /// değeri firmanın tüm hesaplarına kopyalar.
    /// </summary>
    public class HesapSahibiKimlikDto
    {
        public string? Unvan { get; set; }

        /// <summary>Diğer yazımlar, satır satır.</summary>
        public string? TakmaAdlar { get; set; }

        /// <summary>Değerin yazıldığı hesap sayısı; ekranda "N hesaba uygulanır" denir.</summary>
        public int HesapSayisi { get; set; }
    }

    public class HesapSahibiKimlikYazDto
    {
        public string? Unvan { get; set; }
        public string? TakmaAdlar { get; set; }
    }

    /// <summary>Kullanıcıya seçtirilecek ayrıştırıcılar (şimdilik yalnız Vakıfbank vadesiz).</summary>
    public class ParserSecenekDto
    {
        public string Tip { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
    }

    // ---- Ekstre ----

    public class EkstreSayaclariDto
    {
        public int Toplam { get; set; }
        public int Otomatik { get; set; }
        public int OnayBekleyen { get; set; }
        public int Onaylanan { get; set; }
        public int Cozulemeyen { get; set; }
        public int DigerBankada { get; set; }

        /// <summary>Dışa aktarıma engel olan satır sayısı (onay bekleyen + çözülemeyen).</summary>
        public int Eksik => OnayBekleyen + Cozulemeyen;
    }

    public class EkstreYuklemeDto
    {
        public int Id { get; set; }
        public int BankaHesabiId { get; set; }
        public string BankaAdi { get; set; } = string.Empty;
        public string DosyaAdi { get; set; } = string.Empty;
        public DateTime YuklemeTarihi { get; set; }
        public DateTime? DonemBaslangic { get; set; }
        public DateTime? DonemBitis { get; set; }
        public int SatirSayisi { get; set; }
        public YuklemeDurum Durum { get; set; }
        public string? Uyarilar { get; set; }
        public EkstreSayaclariDto Sayaclar { get; set; } = new();

        /// <summary>Kaynak dosya saklandı mı — düzeltilmiş ekstre dosyası üretilebilir mi?</summary>
        public bool KaynakDosyaVar { get; set; }
    }

    /// <summary>Onay ekranında seçenek olarak listelenen karşı hesap adayı.</summary>
    public class AdayDto
    {
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public decimal Skor { get; set; }
    }

    public class EkstreSatirDto
    {
        public int Id { get; set; }
        public int SiraNo { get; set; }
        public DateTime Tarih { get; set; }
        public Yon Yon { get; set; }
        public decimal Tutar { get; set; }
        public string IslemTipi { get; set; } = string.Empty;
        public string HamAciklama { get; set; } = string.Empty;
        public string? KarsiIban { get; set; }
        public string? KarsiVkn { get; set; }
        public string? Kanal { get; set; }

        public string? UretilenAciklama { get; set; }
        public string? CikarilanUnvan { get; set; }

        public string? OnerilenHesapKodu { get; set; }
        public string? OnerilenHesapAdi { get; set; }
        public decimal GuvenSkoru { get; set; }
        public KaynakKatman KaynakKatman { get; set; }

        public string? IkinciAdayKodu { get; set; }
        public string? IkinciAdayAdi { get; set; }
        public decimal? IkinciAdaySkoru { get; set; }

        /// <summary>Aynı unvan ailesinden tüm adaylar; iki adayla sınırlı değil.</summary>
        public List<AdayDto> Adaylar { get; set; } = new();

        public string? OnaylananHesapKodu { get; set; }
        public string? OnaylananHesapAdi { get; set; }
        public SatirDurum Durum { get; set; }

        /// <summary>Öğrenme anahtarının çekirdeği; onay ekranında hangi anahtarın öğrenileceğini gösterir.</summary>
        public string? AnahtarCekirdek { get; set; }
        public string? AyirtEdiciEk { get; set; }

        /// <summary>
        /// Satır çoklu adayla onaya düştüyse belirsizliği üreten n-gram. Onay ekranı
        /// "bu seçim öğrenilecek, aynı belirsizlik bir daha sorulmayacak" bilgisini
        /// buradan gösterir.
        /// </summary>
        public string? BelirsizlikAnahtari { get; set; }

        /// <summary>
        /// Onay sonrası kullanıcıya gösterilecek uyarı (ör. kod hesap planında yok,
        /// bu yüzden öğrenme kaydı yazılmadı). Hata değil; işlem tamamlanmıştır.
        /// </summary>
        public string? Uyari { get; set; }
    }

    public class SatirOnaylaDto
    {
        /// <summary>Boşluklu ORKA kodu, ör. "120 D22".</summary>
        public string HesapKodu { get; set; } = string.Empty;

        /// <summary>
        /// "Bu kişiyi hep bu hesaba yönlendir" kısayolu. İşaretlenirse satırdaki kişi için
        /// kalıcı bir kişi yönlendirmesi oluşturulur; yön, satırın yönünden gelir.
        /// Kişi adı okunamamış satırlarda kayıt yazılmaz, uyarı döner.
        /// </summary>
        public bool KisiYonlendir { get; set; }
    }

    /// <summary>ORKA'ya aktarılacak tek satır.</summary>
    public class OrkaSatirDto
    {
        public int SiraNo { get; set; }
        public DateTime Tarih { get; set; }
        public string Aciklama { get; set; } = string.Empty;
        public Yon Yon { get; set; }
        public decimal Tutar { get; set; }

        /// <summary>
        /// Karşı hesap (cari/gider/banka). PkfRobot'un <c>GridDoldur</c> adımı bu listeyi
        /// tüketiyor: her satır için { SiraNo, Aciklama, KarsiHesapKodu }.
        /// </summary>
        public string KarsiHesapKodu { get; set; } = string.Empty;
        public string? HesapAdi { get; set; }

        /// <summary>Ekstresi işlenen banka hesabının ORKA kodu (kaydın diğer bacağı).</summary>
        public string BankaHesapKodu { get; set; } = string.Empty;
    }

    public class DisaAktarimSonucDto
    {
        public int EkstreId { get; set; }
        public string DosyaAdi { get; set; } = string.Empty;
        public int SatirSayisi { get; set; }

        /// <summary>Karşı bacağı başka bankada işlendiği için dışarıda bırakılan satır sayısı.</summary>
        public int DigerBankadaAtlanan { get; set; }

        /// <summary>
        /// Düzeltilmiş ekstre dosyası (birinci parça) indirilebilir mi? Kaynak dosya
        /// saklanmamışsa (eski yüklemeler) yalnız kod listesi üretilir.
        /// </summary>
        public bool DuzeltilmisEkstreHazir { get; set; }

        public List<OrkaSatirDto> Satirlar { get; set; } = new();
    }

    // ---- Öğrenilen eşleşmeler ----

    public class HesapEslesmesiDto
    {
        public int Id { get; set; }
        public string AnahtarCekirdek { get; set; } = string.Empty;
        public string? AyirtEdiciEk { get; set; }
        public string TamAnahtar { get; set; } = string.Empty;
        public AnahtarTipi AnahtarTipi { get; set; }
        public string HesapKodu { get; set; } = string.Empty;
        public string? HesapAdi { get; set; }
        public Yon Yon { get; set; }

        /// <summary>Belirsizlik kayıtlarında aday kümesinin özeti; küme değişirse karar uygulanmaz.</summary>
        public string? AdayKumesiOzeti { get; set; }

        public int KullanimSayisi { get; set; }
        public DateTime SonKullanim { get; set; }
    }

    /// <summary>Öğrenilen eşleşme düzenleme; anahtar değil, gittiği hesap düzeltilir.</summary>
    public class HesapEslesmesiYazDto
    {
        public string HesapKodu { get; set; } = string.Empty;
        public Yon Yon { get; set; }
        public string? AyirtEdiciEk { get; set; }
    }

    // ---- Hesap planı ----

    public class HesapPlaniKaydiDto
    {
        public int Id { get; set; }
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public string AnaGrup { get; set; } = string.Empty;
        public bool Aktif { get; set; }
    }

    public class HesapPlaniIceAktarimSonucDto
    {
        public int Okunan { get; set; }
        public int Eklenen { get; set; }
        public int Guncellenen { get; set; }
        public int Atlanan { get; set; }

        /// <summary>ORKA dosyasında olmayıp planda duran kayıtlar; silinmez, pasife çekilir.</summary>
        public int Pasiflenen { get; set; }

        public List<string> Uyarilar { get; set; } = new();
    }

    // ---- Vergi kodu eşlemeleri ----

    public class VergiKoduEslemesiDto
    {
        public int Id { get; set; }
        public string? VergiKodu { get; set; }
        public string? AnahtarKelime { get; set; }
        public string HesapKodu { get; set; } = string.Empty;
        public string? HesapAdi { get; set; }
        public int Sira { get; set; }
        public bool Aktif { get; set; }
    }

    public class VergiKoduEslemesiYazDto
    {
        public string? VergiKodu { get; set; }
        public string? AnahtarKelime { get; set; }
        public string HesapKodu { get; set; } = string.Empty;
        public string? HesapAdi { get; set; }
        public int Sira { get; set; }
        public bool Aktif { get; set; } = true;
    }

    // ---- Kişi yönlendirmeleri ----

    public class KisiYonlendirmeDto
    {
        public int Id { get; set; }

        /// <summary>Kullanıcının girdiği yazım.</summary>
        public string Isim { get; set; } = string.Empty;

        /// <summary>Eşleştirmenin kullandığı normalize çekirdek; ekranda ipucu olarak gösterilir.</summary>
        public string IsimCekirdegi { get; set; } = string.Empty;

        public YonlendirmeYonu Yon { get; set; }
        public string HesapKodu { get; set; } = string.Empty;
        public string? HesapAdi { get; set; }
        public string? Aciklama { get; set; }
        public bool Aktif { get; set; }
    }

    public class KisiYonlendirmeYazDto
    {
        public string Isim { get; set; } = string.Empty;
        public YonlendirmeYonu Yon { get; set; } = YonlendirmeYonu.Farketmez;
        public string HesapKodu { get; set; } = string.Empty;
        public string? Aciklama { get; set; }
        public bool Aktif { get; set; } = true;
    }


    // ---- Yapılandırma tabloları (sabit kural / şablon / desen) ----

    /// <summary>
    /// Üç yapılandırma tablosunun ortak alanı: kaydın hangi bankada geçerli olduğu.
    /// Boş <c>ParserTipi</c> "tüm bankalar" demektir; Vakıfbank'a özel bir desen
    /// Ziraat ekstresinde çalışmasın diye doldurulur.
    /// </summary>
    public class SabitKuralDto
    {
        public int Id { get; set; }

        /// <summary>Boş ise kural tüm bankalarda geçerli.</summary>
        public string ParserTipi { get; set; } = string.Empty;

        /// <summary>Ekranda gösterilecek ayrıştırıcı adı; boş ParserTipi için "Tüm bankalar".</summary>
        public string ParserAdi { get; set; } = string.Empty;

        public string IslemTipiDeseni { get; set; } = string.Empty;
        public KuralKapsami Kapsam { get; set; }
        public EslesmeTuru EslesmeTuru { get; set; }

        /// <summary>Dolu ise kural yalnız bu yöndeki satırlara uygulanır.</summary>
        public Yon? Yon { get; set; }

        public string HesapKodu { get; set; } = string.Empty;
        public string? HesapAdi { get; set; }
        public bool UnvanCikarilsin { get; set; }
        public bool AltHesapGerekli { get; set; }
        public int Sira { get; set; }
        public bool Aktif { get; set; }
    }

    public class SabitKuralYazDto
    {
        /// <summary>Boş bırakılırsa kural tüm bankalarda geçerli olur.</summary>
        public string? ParserTipi { get; set; }

        public string IslemTipiDeseni { get; set; } = string.Empty;
        public KuralKapsami Kapsam { get; set; } = KuralKapsami.IslemTipi;
        public EslesmeTuru EslesmeTuru { get; set; } = EslesmeTuru.Tam;
        public Yon? Yon { get; set; }
        public string HesapKodu { get; set; } = string.Empty;
        public string? HesapAdi { get; set; }
        public bool UnvanCikarilsin { get; set; } = true;
        public bool AltHesapGerekli { get; set; }
        public int Sira { get; set; }
        public bool Aktif { get; set; } = true;
    }

    // ---- Açıklama şablonları ----

    public class AciklamaSablonuDto
    {
        public int Id { get; set; }
        public string ParserTipi { get; set; } = string.Empty;
        public string ParserAdi { get; set; } = string.Empty;
        public string IslemTipiDeseni { get; set; } = string.Empty;
        public EslesmeTuru EslesmeTuru { get; set; }
        public string Sablon { get; set; } = string.Empty;

        /// <summary>Bankalar arası hareket mi (virman, süpürme); karşı taraf yerine banka adı kullanılır.</summary>
        public bool BankalarArasi { get; set; }

        public int Sira { get; set; }
        public bool Aktif { get; set; }
    }

    public class AciklamaSablonuYazDto
    {
        public string? ParserTipi { get; set; }
        public string IslemTipiDeseni { get; set; } = string.Empty;
        public EslesmeTuru EslesmeTuru { get; set; } = EslesmeTuru.Tam;
        public string Sablon { get; set; } = string.Empty;
        public bool BankalarArasi { get; set; }
        public int Sira { get; set; }
        public bool Aktif { get; set; } = true;
    }

    /// <summary>Şablonda kullanılabilecek yer tutucu; liste sunucudan gelir, ekranda gösterilir.</summary>
    public class YerTutucuDto
    {
        public string Ad { get; set; } = string.Empty;
        public string Aciklama { get; set; } = string.Empty;
    }

    // ---- Unvan çıkarma desenleri ----

    public class UnvanDeseniDto
    {
        public int Id { get; set; }
        public string ParserTipi { get; set; } = string.Empty;
        public string ParserAdi { get; set; } = string.Empty;
        public string Desen { get; set; } = string.Empty;
        public int GrupNo { get; set; }
        public string? Aciklama { get; set; }
        public int Sira { get; set; }
        public bool Aktif { get; set; }
    }

    public class UnvanDeseniYazDto
    {
        public string? ParserTipi { get; set; }
        public string Desen { get; set; } = string.Empty;
        public int GrupNo { get; set; } = 1;
        public string? Aciklama { get; set; }
        public int Sira { get; set; }
        public bool Aktif { get; set; } = true;
    }

    /// <summary>Deneme kutusunun isteği: desen bu metinde ne yakalıyor?</summary>
    public class DesenDenemeIstegiDto
    {
        public string Desen { get; set; } = string.Empty;
        public int GrupNo { get; set; } = 1;
        public string OrnekMetin { get; set; } = string.Empty;
    }

    /// <summary>
    /// Deneme sonucu. Ham yakalama ile <see cref="Unvan"/> ayrı: desen tutsa bile
    /// çıkarıcı yakalamayı eleyebilir (çok kısa, IBAN künyesi, hesap sahibinin
    /// kendi unvan alanı). Kullanıcı farkı ekranda görmeli.
    /// </summary>
    public class DesenDenemeSonucDto
    {
        public bool Gecerli { get; set; }
        public string? Hata { get; set; }
        public bool Eslesti { get; set; }
        public string? HamYakalanan { get; set; }
        public string? Unvan { get; set; }
        public string? Not { get; set; }
    }

    /// <summary>Tanımlar ekranındaki hesap planı özeti.</summary>
    public class HesapPlaniOzetDto
    {
        public int Sayi { get; set; }
        public DateTime? SonIceAktarim { get; set; }

        /// <summary>Son içe aktarımın üzerinden geçen gün; 30'u aşarsa ekranda hatırlatma çıkar.</summary>
        public int? GunFarki { get; set; }
    }

    /// <summary>
    /// Banka Otomasyon firma seçim ekranının bir satırı: firmanın kurulum durumu.
    /// Tenant filtresi baypas edilerek okunur (bkz. <c>FirmaOzetService</c>), çünkü
    /// ekran <b>girilmemiş</b> firmaların sayaçlarını da gösterir.
    /// </summary>
    public class FirmaBankaOzetiDto
    {
        public string TenantNo { get; set; } = string.Empty;

        /// <summary>Aktif hesap planı kaydı sayısı; 0 ise firma "kurulum gerekli".</summary>
        public int HesapPlaniSayisi { get; set; }

        /// <summary>Tanımlı aktif banka hesabı sayısı.</summary>
        public int BankaHesabiSayisi { get; set; }

        /// <summary>
        /// Dışa aktarıma engel satır sayısı (onay bekleyen + çözülemeyen), firmanın
        /// <b>tüm</b> bankaları ve <b>tüm</b> dönemleri toplanarak. Aktar ekranındaki
        /// banka rozeti yalnız seçili dönemi sayar; bu sütun kasten daha geniştir.
        /// </summary>
        public int OnayBekleyen { get; set; }
    }
}
