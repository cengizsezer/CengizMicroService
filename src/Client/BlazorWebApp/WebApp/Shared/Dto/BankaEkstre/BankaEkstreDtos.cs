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
        Vkn = 3
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
        Kullanici = 7
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
        public HesapTipi HesapTipi { get; set; } = HesapTipi.Vadesiz;
        public string ParaBirimi { get; set; } = "TRY";
        public string? Iban { get; set; }
        public string OrkaHesapKodu { get; set; } = string.Empty;
        public string ParserTipi { get; set; } = string.Empty;
        public bool Aktif { get; set; } = true;
        public bool IbanKatmaniAktif { get; set; }
        public bool VknKatmaniAktif { get; set; }
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
        public SatirDurum Durum { get; set; }

        public string? AnahtarCekirdek { get; set; }
        public string? AyirtEdiciEk { get; set; }

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
        public int KullanimSayisi { get; set; }
        public DateTime SonKullanim { get; set; }
    }

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

    public class HesapPlaniOzetDto
    {
        public int Sayi { get; set; }
        public DateTime? SonIceAktarim { get; set; }
        public int? GunFarki { get; set; }
    }

    /// <summary>Ekrandaki Türkçe karşılıklar ve ortak biçimlendirme.</summary>
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
            KaynakKatman.Kullanici => "kullanıcı",
            _ => "—"
        };

        public static string Yon(Yon y) => y == Dto.BankaEkstre.Yon.Giren ? "Giren" : "Çıkan";

        public static string HesapTipi(HesapTipi t) => t == Dto.BankaEkstre.HesapTipi.Vadesiz ? "Vadesiz" : "Vadeli";

        public static string Tutar(decimal tutar) => tutar.ToString("N2", Kultur);

        public static string Tarih(DateTime tarih) => tarih.ToString(TarihBicimi, Kultur);

        public static string Skor(decimal skor) => skor <= 0m ? "—" : skor.ToString("0.00", Kultur);
    }
}
