using System.Globalization;

namespace WebApp.Shared.Dto.BankaEkstre
{
    // Sunucudaki CatalogService.Api.Features.BankaEkstre.Domain enum'larının aynısı.
    // Sayısal değerler sözleşmenin parçası; sıra değiştirilmemeli.

    public enum HesapTipi : byte { Vadesiz = 1, Vadeli = 2 }

    public enum Yon : byte { Giren = 1, Cikan = 2 }

    public enum YuklemeDurum : byte { Isleniyor = 0, Tamamlandi = 1, Hatali = 2 }

    public enum SatirDurum : byte
    {
        Otomatik = 1,
        OnayBekliyor = 2,
        Onaylandi = 3,
        Cozulemedi = 4,
        DigerBankada = 5
    }

    /// <summary>Öğrenme anahtarının tipi (sunucudaki enum ile aynı).</summary>
    public enum AnahtarTipi : byte
    {
        UnvanCekirdek = 1,
        Iban = 2,
        Vkn = 3,

        /// <summary>Kullanıcının çözdüğü belirsizlik; anahtar, belirsizliği üreten n-gram.</summary>
        Belirsizlik = 4
    }

    public enum KaynakKatman : byte
    {
        Yok = 0,
        Iban = 1,
        Vkn = 2,
        GecmisOnay = 3,
        BankaKayitDefteri = 4,
        SabitKural = 5,
        UnvanBenzerligi = 6,
        Kullanici = 7,

        /// <summary>Benzersiz önek: hesap adı açıklamanın bir token dizisiyle başlayan tek cari.</summary>
        BenzersizOnek = 8,

        /// <summary>Vergi kodu / anahtar kelime eşleme tablosu veya plaka anahtarı.</summary>
        VergiPlaka = 9,

        /// <summary>Kullanıcının tanımladığı kişi yönlendirmesi; tüm katmanlardan önce çalışır.</summary>
        KisiYonlendirme = 10
    }

    /// <summary>Kişi yönlendirmesinin geçerli olduğu para yönü (sunucudaki enum ile aynı).</summary>
    public enum YonlendirmeYonu : byte
    {
        Giren = 1,
        Cikan = 2,
        Farketmez = 3
    }

    /// <summary>
    /// Sabit kuralın deseni hangi metinde aranacak (sunucudaki enum ile aynı).
    /// Açıklama kapsamı ham banka açıklamasında arar ve öğrenme katmanından önce çalışır.
    /// </summary>
    public enum KuralKapsami : byte
    {
        IslemTipi = 1,
        Aciklama = 2
    }

    /// <summary>Şablon/desen/kural tablolarında desenin nasıl eşleşeceği (sunucudaki enum ile aynı).</summary>
    public enum EslesmeTuru : byte
    {
        Tam = 1,
        Icerir = 2,
        Regex = 3
    }

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

        /// <summary>
        /// Hesap sahibinin diğer yazımları, satır satır. Bankalar aynı firmayı çok farklı
        /// yazıyor; tek alan yetmediği için kalan yazımlar elenmiyor ve karşı taraf sanılıyordu.
        /// </summary>
        public string? HesapSahibiTakmaAdlari { get; set; }

        public HesapTipi HesapTipi { get; set; }
        public string ParaBirimi { get; set; } = "TRY";
        public string? Iban { get; set; }
        public string OrkaHesapKodu { get; set; } = string.Empty;
        public string ParserTipi { get; set; } = string.Empty;
        public bool Aktif { get; set; }
        public bool IbanKatmaniAktif { get; set; }
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
    /// Yüklenmiş ekstrelerde geçen, hesap sahibinin tanımlı yazımlarına benzeyen ama henüz
    /// eklenmemiş bir yazım. Tek tıkla takma adlara eklenir.
    /// </summary>
    public class HesapSahibiOnerisiDto
    {
        public string Yazim { get; set; } = string.Empty;
        public int Adet { get; set; }
    }

    /// <summary>Hesap adından üretilen eşleştirme anahtarı önerisi.</summary>
    public class AnahtarOnerisiDto
    {
        public string? EslestirmeAnahtarlari { get; set; }
    }

    /// <summary>
    /// Banka adı alanının biçim denetimi. <b>Engellemez</b>, yalnız uyarır: kullanıcı
    /// buraya tam hesap adını yazma eğiliminde ("Vakıfbank, Vadeli Tl - Otomatik Süpürme
    /// Hesabı") ama o metin hiçbir ekstre açıklamasında geçmediği için eşleşme hiç olmuyor.
    /// </summary>
    public static class BankaAdiDenetimi
    {
        /// <summary>Bu uzunluğu aşan ad, banka adı değil hesap adı olma ihtimali yüksek.</summary>
        public const int EnFazlaUzunluk = 25;

        /// <summary>Uyarı metni; sorun yoksa null.</summary>
        public static string? Uyari(string? bankaAdi)
        {
            if (string.IsNullOrWhiteSpace(bankaAdi)) return null;

            var ad = bankaAdi.Trim();
            var uzun = ad.Length > EnFazlaUzunluk;
            var ayrac = ad.Contains(',') || ad.Contains('-');

            if (!uzun && !ayrac) return null;

            var sebep = uzun && ayrac ? "uzun ve virgül/tire içeriyor"
                      : uzun ? $"{EnFazlaUzunluk} karakterden uzun"
                      : "virgül/tire içeriyor";

            return $"Banka adı {sebep}. Buraya kısa banka adı yazın (Vakıfbank, Ziraat, TEB); " +
                   "hesabın tam adı Hesap adı alanına, ayırt edici ifadeler Eşleştirme anahtarlarına girilir.";
        }

        /// <summary>
        /// Girilen ad mevcut hesaplardan hiçbiriyle eşleşmiyorsa uyarı; eşleşiyorsa null.
        ///
        /// Gerekçe biçimsel değil işlevsel: "aynı banka önceliği" kuralı <c>BankaAdi</c>
        /// üzerinden çalışır. Aynı banka iki farklı yazımla girilirse sistem onları ayrı
        /// bankalar sayar, sekme sayısı şişer ve bankalar arası eşleştirme bozulur.
        ///
        /// KARŞILAŞTIRMA, sekme şeridinin gruplamasıyla <b>birebir aynıdır</b>
        /// (<c>OrdinalIgnoreCase</c> + kırpma). Uyarı böylece tam olarak "yeni bir sekme
        /// açılacak mı?" sorusunu yanıtlar. Türkçe sonucu: "ZIRAAT" ile "Ziraat" aynı
        /// sayılır, ama "İŞ BANKASI" ile "İş Bankası" AYRI sayılır — ordinal karşılaştırma
        /// 'ı' ile 'I' harflerini eşlemez ve sekme şeridi de tam bu yüzden ikiye bölünür.
        /// Uyarı doğru: kullanıcının düzeltmesi gereken şey de zaten bu.
        /// </summary>
        public static string? YeniBankaUyarisi(string? bankaAdi, IEnumerable<string?>? mevcutAdlar)
        {
            if (string.IsNullOrWhiteSpace(bankaAdi)) return null;

            var ad = bankaAdi.Trim();

            var eslesenVar = (mevcutAdlar ?? Enumerable.Empty<string?>())
                .Any(m => !string.IsNullOrWhiteSpace(m)
                          && string.Equals(m!.Trim(), ad, StringComparison.OrdinalIgnoreCase));

            if (eslesenVar) return null;

            return $"\"{ad}\" mevcut hiçbir hesapla eşleşmiyor, yeni bir banka sekmesi açılacak. " +
                   "Var olan bir bankayı kastediyorsanız listeden aynı yazımı seçin; " +
                   "farklı yazımlar ayrı banka sayılır ve bankalar arası eşleştirmeyi bozar.";
        }
    }

    /// <summary>
    /// Toplu banka hesabı içe aktarımında bir satırın sorunu. Hata satırı düşürür,
    /// uyarı düşürmez. Alan adları sunucunun <c>{ field, message }</c> sözleşmesiyle aynı.
    /// </summary>
    public class IceAktarimSatirSorunuDto
    {
        public int SatirNo { get; set; }
        public string Field { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class BankaHesabiIceAktarimSonucDto
    {
        public int Okunan { get; set; }
        public int Eklenen { get; set; }
        public int Guncellenen { get; set; }
        public int Atlanan { get; set; }
        public List<IceAktarimSatirSorunuDto> Hatalar { get; set; } = new();
        public List<IceAktarimSatirSorunuDto> Uyarilar { get; set; } = new();
    }

    /// <summary>
    /// Hesap sahibinin (firmanın) kimliği. Değer hesap satırlarında durur ama firma
    /// bazlıdır; Firma Tanımları ekranı tek kayıt olarak yönetir ve tüm hesaplara yazar.
    /// </summary>
    public class HesapSahibiKimlikDto
    {
        public string? Unvan { get; set; }
        public string? TakmaAdlar { get; set; }

        /// <summary>Değerin yazılacağı hesap sayısı; ekranda gösterilir.</summary>
        public int HesapSayisi { get; set; }
    }

    public class HesapSahibiKimlikYazDto
    {
        public string? Unvan { get; set; }
        public string? TakmaAdlar { get; set; }
    }

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

        /// <summary>Dışa aktarıma engel olan satır sayısı.</summary>
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

        /// <summary>Kaynak dosya saklandı mı — düzeltilmiş ekstre indirilebilir mi?</summary>
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

        /// <summary>
        /// Satırın işlem kategorisi; onaylanan (yoksa önerilen) hesap kodunun ana grubundan
        /// sunucuda okunur. Onay ekranında küçük bir etiket olarak görünür.
        /// </summary>
        public int? IslemKategorisiId { get; set; }

        public string? IslemKategorisiAdi { get; set; }

        public SatirDurum Durum { get; set; }

        public string? AnahtarCekirdek { get; set; }
        public string? AyirtEdiciEk { get; set; }

        /// <summary>
        /// Satır çoklu adayla onaya düştüyse belirsizliği üreten n-gram. Doluysa onay
        /// ekranı "bu seçim öğrenilecek, aynı belirsizlik bir daha sorulmayacak" der.
        /// </summary>
        public string? BelirsizlikAnahtari { get; set; }

        /// <summary>Onay sonrası uyarı (ör. kod planda yok → öğrenilmedi). Hata değil.</summary>
        public string? Uyari { get; set; }

        /// <summary>
        /// Onay ekranının göstereceği aday listesi: aile doluysa o, değilse önerilen +
        /// yakın ikinci aday. Alt+N seçimi bu sırayı kullanır.
        /// </summary>
        public List<AdayDto> SecilebilirAdaylar()
        {
            if (Adaylar.Count > 0) return Adaylar;

            var liste = new List<AdayDto>();
            if (!string.IsNullOrWhiteSpace(OnerilenHesapKodu))
                liste.Add(new AdayDto { Kod = OnerilenHesapKodu, Ad = OnerilenHesapAdi ?? string.Empty, Skor = GuvenSkoru });

            if (!string.IsNullOrWhiteSpace(IkinciAdayKodu))
                liste.Add(new AdayDto { Kod = IkinciAdayKodu, Ad = IkinciAdayAdi ?? string.Empty, Skor = IkinciAdaySkoru ?? 0m });

            return liste.Count > 1 ? liste : new List<AdayDto>();
        }
    }

    public class SatirOnaylaDto
    {
        public string HesapKodu { get; set; } = string.Empty;

        /// <summary>
        /// "Bu kişiyi hep bu hesaba yönlendir": işaretlenirse satırdaki kişi için kalıcı
        /// bir yönlendirme kaydı oluşur ve aynı kişi bir daha sorulmaz.
        /// </summary>
        public bool KisiYonlendir { get; set; }
    }

    public class OrkaSatirDto
    {
        public int SiraNo { get; set; }
        public DateTime Tarih { get; set; }
        public string Aciklama { get; set; } = string.Empty;
        public Yon Yon { get; set; }
        public decimal Tutar { get; set; }
        /// <summary>PkfRobot'un GridDoldur adımının tükettiği karşı hesap kodu.</summary>
        public string KarsiHesapKodu { get; set; } = string.Empty;
        public string? HesapAdi { get; set; }
        public string BankaHesapKodu { get; set; } = string.Empty;
    }

    public class DisaAktarimSonucDto
    {
        public int EkstreId { get; set; }
        public string DosyaAdi { get; set; } = string.Empty;
        public int SatirSayisi { get; set; }
        public int DigerBankadaAtlanan { get; set; }

        /// <summary>Düzeltilmiş ekstre dosyası (birinci parça) indirilebilir mi?</summary>
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

    public class HesapEslesmesiYazDto
    {
        public string HesapKodu { get; set; } = string.Empty;
        public Yon Yon { get; set; }
        public string? AyirtEdiciEk { get; set; }
    }

    /// <summary>
    /// Öğrenilen eşleşmelerin toplu içe aktarım sonucu. Banka hesabı sonucundan ayrı bir
    /// tip: burada "güncellenen" yok (mevcut kayıt korunur, üzerine yazılmaz) ve
    /// <see cref="Atlanan"/> ile <see cref="Hatali"/> ayrı sayılır — biri kullanıcının
    /// kararının korunduğu satır, diğeri reddedilen satır.
    /// </summary>
    public class OgrenilenEslesmeIceAktarimSonucDto
    {
        /// <summary>İşlenen (boş olmayan) satır sayısı.</summary>
        public int Okunan { get; set; }

        /// <summary>En az bir kayıt yazılan satır sayısı.</summary>
        public int Eklenen { get; set; }

        /// <summary>Anahtar zaten kayıtlı olduğu için dokunulmayan satır sayısı.</summary>
        public int Atlanan { get; set; }

        /// <summary>Doğrulamadan geçemeyen satır sayısı.</summary>
        public int Hatali { get; set; }

        /// <summary>
        /// Yazılan kayıt sayısı. Satır sayısından fazla olabilir: <c>Farketmez</c> satırı
        /// iki yön için de kayıt yazar (<c>HesapEslesmesi.Yon</c> yalnız Giren/Çıkan tutar).
        /// </summary>
        public int EklenenKayit { get; set; }

        public List<IceAktarimSatirSorunuDto> Hatalar { get; set; } = new();

        /// <summary>Satırı düşürmeyen sorunlar (mevcut kayıt korundu, ayırt edici ekli kayıt var…).</summary>
        public List<IceAktarimSatirSorunuDto> Uyarilar { get; set; } = new();
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
        /// <summary>Muhasebe kategorisi; yalnız etiket ve görünüm, eşleştirmeye girmez.</summary>
        public int? IslemKategorisiId { get; set; }

        public string? IslemKategorisiAdi { get; set; }
        public int Sira { get; set; }
        public bool Aktif { get; set; }
    }

    public class VergiKoduEslemesiYazDto
    {
        public string? VergiKodu { get; set; }
        public string? AnahtarKelime { get; set; }
        public string HesapKodu { get; set; } = string.Empty;
        public string? HesapAdi { get; set; }
        /// <summary>Muhasebe kategorisi; boş bırakılabilir.</summary>
        public int? IslemKategorisiId { get; set; }
        public int Sira { get; set; }
        public bool Aktif { get; set; } = true;
    }

    public class HesapPlaniOzetDto
    {
        public int Sayi { get; set; }
        public DateTime? SonIceAktarim { get; set; }
        public int? GunFarki { get; set; }
    }

    /// <summary>Ekrandaki Türkçe karşılıklar ve ortak biçimlendirme.</summary>
    // ---- Kişi yönlendirmeleri ----

    public class KisiYonlendirmeDto
    {
        public int Id { get; set; }
        public string Isim { get; set; } = string.Empty;

        /// <summary>Eşleştirmenin kullandığı normalize çekirdek; ekranda ipucu olarak gösterilir.</summary>
        public string IsimCekirdegi { get; set; } = string.Empty;

        public YonlendirmeYonu Yon { get; set; }
        public string HesapKodu { get; set; } = string.Empty;
        public string? HesapAdi { get; set; }
        public string? Aciklama { get; set; }
        /// <summary>Muhasebe kategorisi; yalnız etiket ve görünüm, eşleştirmeye girmez.</summary>
        public int? IslemKategorisiId { get; set; }

        public string? IslemKategorisiAdi { get; set; }
        public bool Aktif { get; set; }
    }

    public class KisiYonlendirmeYazDto
    {
        public string Isim { get; set; } = string.Empty;
        public YonlendirmeYonu Yon { get; set; } = YonlendirmeYonu.Farketmez;
        public string HesapKodu { get; set; } = string.Empty;
        public string? Aciklama { get; set; }
        /// <summary>Muhasebe kategorisi; boş bırakılabilir.</summary>
        public int? IslemKategorisiId { get; set; }
        public bool Aktif { get; set; } = true;
    }

    // ---- Sabit kurallar ----

    /// <summary>
    /// Üç yapılandırma tablosunun (kural / şablon / desen) ortak alanı: boş
    /// <see cref="ParserTipi"/> "tüm bankalar" demektir, dolusu yalnız o bankada geçerli.
    /// </summary>
    public class SabitKuralDto
    {
        public int Id { get; set; }
        public string ParserTipi { get; set; } = string.Empty;

        /// <summary>Listede gösterilen ad; boş ParserTipi için "Tüm bankalar".</summary>
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
        /// <summary>Muhasebe kategorisi; yalnız etiket ve görünüm, eşleştirmeye girmez.</summary>
        public int? IslemKategorisiId { get; set; }

        public string? IslemKategorisiAdi { get; set; }
        public int Sira { get; set; }
        public bool Aktif { get; set; }
    }

    public class SabitKuralYazDto
    {
        public string? ParserTipi { get; set; }
        public string IslemTipiDeseni { get; set; } = string.Empty;
        public KuralKapsami Kapsam { get; set; } = KuralKapsami.IslemTipi;
        public EslesmeTuru EslesmeTuru { get; set; } = EslesmeTuru.Tam;
        public Yon? Yon { get; set; }
        public string HesapKodu { get; set; } = string.Empty;
        public string? HesapAdi { get; set; }
        public bool UnvanCikarilsin { get; set; } = true;
        public bool AltHesapGerekli { get; set; }
        /// <summary>Muhasebe kategorisi; boş bırakılabilir.</summary>
        public int? IslemKategorisiId { get; set; }
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

        /// <summary>Bankalar arası hareket mi; karşı taraf yerine banka adı kullanılır.</summary>
        public bool BankalarArasi { get; set; }
        /// <summary>Muhasebe kategorisi; yalnız etiket ve görünüm, eşleştirmeye girmez.</summary>
        public int? IslemKategorisiId { get; set; }

        public string? IslemKategorisiAdi { get; set; }
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
        /// <summary>Muhasebe kategorisi; boş bırakılabilir.</summary>
        public int? IslemKategorisiId { get; set; }
        public int Sira { get; set; }
        public bool Aktif { get; set; } = true;
    }

    /// <summary>Şablonda kullanılabilecek yer tutucu; liste sunucudan gelir.</summary>
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

    public class DesenDenemeIstegiDto
    {
        public string Desen { get; set; } = string.Empty;
        public int GrupNo { get; set; } = 1;
        public string OrnekMetin { get; set; } = string.Empty;
    }

    /// <summary>
    /// Deneme sonucu. Ham yakalama ile <see cref="Unvan"/> ayrı: desen tutsa bile
    /// çıkarıcı yakalamayı eleyebilir (çok kısa, IBAN künyesi, hesap sahibinin kendi alanı).
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

    public static class BankaEkstreEtiket
    {
        public static readonly CultureInfo Kultur = new("tr-TR");
        public const string TarihBicimi = "dd.MM.yyyy";

        public static string Durum(SatirDurum d) => d switch
        {
            SatirDurum.Otomatik => "Otomatik",
            SatirDurum.OnayBekliyor => "Onay bekliyor",
            SatirDurum.Onaylandi => "Onaylandı",
            SatirDurum.Cozulemedi => "Çözülemedi",
            SatirDurum.DigerBankada => "Diğer bankada",
            _ => "—"
        };

        /// <summary>Onay ekranındaki küçük etiket: hangi katmanın yanıldığı buradan görülür.</summary>
        public static string Katman(KaynakKatman k) => k switch
        {
            KaynakKatman.Iban => "IBAN",
            // "geçmiş" = öğrenilmiş eşleşme; yanılırsa Tanımlar > Öğrenilen Eşleşmeler'den düzeltilir.
            KaynakKatman.Vkn => "VKN",
            KaynakKatman.GecmisOnay => "geçmiş",
            KaynakKatman.BankaKayitDefteri => "banka",
            KaynakKatman.SabitKural => "kural",
            KaynakKatman.UnvanBenzerligi => "benzerlik",
            KaynakKatman.BenzersizOnek => "önek",
            KaynakKatman.VergiPlaka => "vergi/plaka",
            KaynakKatman.KisiYonlendirme => "kişi",
            KaynakKatman.Kullanici => "kullanıcı",
            _ => "—"
        };

        public static string Yon(Yon y) => y == Dto.BankaEkstre.Yon.Giren ? "Giren" : "Çıkan";

        public static string YonlendirmeYonu(YonlendirmeYonu y) => y switch
        {
            Dto.BankaEkstre.YonlendirmeYonu.Giren => "Giren",
            Dto.BankaEkstre.YonlendirmeYonu.Cikan => "Çıkan",
            _ => "Farketmez"
        };

        /// <summary>Sabit kuralın deseni nerede aradığı.</summary>
        public static string Kapsam(KuralKapsami k)
            => k == KuralKapsami.Aciklama ? "Açıklama" : "İşlem tipi";

        public static string Eslesme(EslesmeTuru e) => e switch
        {
            EslesmeTuru.Icerir => "İçerir",
            EslesmeTuru.Regex => "Regex",
            _ => "Tam"
        };

        /// <summary>Kuralın yön kısıtı; boş bırakılmışsa her iki yönde de geçerli.</summary>
        public static string KuralYonu(Yon? y) => y is null ? "Farketmez" : Yon(y.Value);

        public static string HesapTipi(HesapTipi t) => t == Dto.BankaEkstre.HesapTipi.Vadesiz ? "Vadesiz" : "Vadeli";

        public static string Tutar(decimal tutar) => tutar.ToString("N2", Kultur);

        public static string Tarih(DateTime tarih) => tarih.ToString(TarihBicimi, Kultur);

        public static string Skor(decimal skor) => skor <= 0m ? "—" : skor.ToString("0.00", Kultur);
    }

    /// <summary>
    /// Banka Otomasyon firma seçim ekranının bir satırının sayaçları. Sunucudaki
    /// <c>FirmaBankaOzetiDto</c> ile aynı sözleşme.
    /// </summary>
    public class FirmaBankaOzetiDto
    {
        /// <summary>catalog.Firmalar.Id — Raporlar ekranıyla aynı anahtar.</summary>
        public int FirmaId { get; set; }

        /// <summary>Aktif hesap planı kaydı sayısı; 0 ise firma "kurulum gerekli".</summary>
        public int HesapPlaniSayisi { get; set; }

        public int BankaHesabiSayisi { get; set; }

        /// <summary>Tüm bankalar ve tüm dönemler toplamı (onay bekleyen + çözülemeyen).</summary>
        public int OnayBekleyen { get; set; }
    }

    /// <summary>
    /// "Bu firmanın banka otomasyon verisini temizle" işleminin kapsamı: hangi tablodan
    /// kaç kayıt silinecek (onaydan önce) veya silindi (onaydan sonra).
    ///
    /// Global tablolar (açıklama şablonları, unvan desenleri, sabit kurallar, vergi
    /// kodları, kimlik kayıtları) bu listede YOK — onlar bankanın yazım kalıbına ait,
    /// firmaya değil ve silinmiyor.
    /// </summary>
    public class BankaTemizlikOzetiDto
    {
        /// <summary>catalog.Firmalar.Id; 0 ise sahipsiz (eski tenant düzeninden kalan) kayıtlar.</summary>
        public int FirmaId { get; set; }

        public int HesapPlaniKaydi { get; set; }
        public int BankaHesabi { get; set; }
        public int EkstreYukleme { get; set; }
        public int EkstreSatiri { get; set; }
        public int HesapEslesmesi { get; set; }
        public int KisiYonlendirme { get; set; }

        public int Toplam => HesapPlaniKaydi + BankaHesabi + EkstreYukleme + EkstreSatiri
                             + HesapEslesmesi + KisiYonlendirme;

        public bool Bos => Toplam == 0;
    }

    // ---- Banka adları ----

    /// <summary>Firmada kullanılan bir banka adı ve kaç hesapta geçtiği.</summary>
    public class BankaAdiDto
    {
        public string Ad { get; set; } = string.Empty;
        public int HesapSayisi { get; set; }
    }

    public class BankaAdiBirlestirDto
    {
        public List<string> Kaynaklar { get; set; } = new();
        public string Hedef { get; set; } = string.Empty;
    }

    public class BankaAdiBirlestirSonucDto
    {
        public string Hedef { get; set; } = string.Empty;
        public int EtkilenenHesap { get; set; }
        public List<BankaAdiDto> BankaAdlari { get; set; } = new();
    }

    // ---- İşlem kategorileri ----

    public class IslemKategorisiDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;

        /// <summary>Varsayılan ana hesap grubu ("195"); ekstre satırı bununla etiketlenir.</summary>
        public string? VarsayilanAnaGrup { get; set; }

        public int Sira { get; set; }
        public bool Aktif { get; set; }
    }

    public class IslemKategorisiYazDto
    {
        public string Ad { get; set; } = string.Empty;
        public string? VarsayilanAnaGrup { get; set; }
        public int Sira { get; set; }
        public bool Aktif { get; set; } = true;
    }

    /// <summary>Kategori accordion'unda listelenen kural; mekanizması küçük bir etiket.</summary>
    public class KategoriKuralDto
    {
        public int Id { get; set; }

        /// <summary>"sabit kural" | "şablon" | "vergi kodu" | "kişi".</summary>
        public string Mekanizma { get; set; } = string.Empty;

        public string Ad { get; set; } = string.Empty;
        public string? HesapKodu { get; set; }
        public string? HesapAdi { get; set; }
        public bool Aktif { get; set; }
    }

    public class KategoriKapsamDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string? VarsayilanAnaGrup { get; set; }
        public int Sira { get; set; }
        public bool Aktif { get; set; }

        /// <summary>Kategoriye bağlı kuralların ORKA kodları, tekrarsız.</summary>
        public List<string> HesapKodlari { get; set; } = new();

        public int KuralSayisi { get; set; }
        public List<KategoriKuralDto> Kurallar { get; set; } = new();

        /// <summary>Hiç kuralı yok mu? Liste bu satırları kırmızı gösterir.</summary>
        public bool Bos => KuralSayisi == 0;

        /// <summary>Kod kolonu: birden fazlaysa "195 · 196".</summary>
        public string KodMetni => HesapKodlari.Count == 0 ? "—" : string.Join(" · ", HesapKodlari);
    }

    public class KategoriKapsamOzetiDto
    {
        public string? ParserTipi { get; set; }
        public string ParserAdi { get; set; } = string.Empty;
        public int Toplam { get; set; }
        public int Tanimli { get; set; }
        public int KategorisizKural { get; set; }
        public List<KategoriKapsamDto> Kategoriler { get; set; } = new();
    }
}

