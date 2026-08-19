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
        public HesapTipi HesapTipi { get; set; }
        public string ParaBirimi { get; set; } = "TRY";
        public string? Iban { get; set; }
        public string OrkaHesapKodu { get; set; } = string.Empty;
        public string ParserTipi { get; set; } = string.Empty;
        public bool Aktif { get; set; }
    }

    public class BankaHesabiYazDto
    {
        public string BankaAdi { get; set; } = string.Empty;
        public HesapTipi HesapTipi { get; set; } = HesapTipi.Vadesiz;
        public string ParaBirimi { get; set; } = "TRY";
        public string? Iban { get; set; }
        public string OrkaHesapKodu { get; set; } = string.Empty;
        public string ParserTipi { get; set; } = string.Empty;
        public bool Aktif { get; set; } = true;
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

        public string? OnaylananHesapKodu { get; set; }
        public string? OnaylananHesapAdi { get; set; }
        public SatirDurum Durum { get; set; }
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
        public string HesapKodu { get; set; } = string.Empty;
        public string? HesapAdi { get; set; }
        public string BankaHesapKodu { get; set; } = string.Empty;
    }

    public class DisaAktarimSonucDto
    {
        public int EkstreId { get; set; }
        public string DosyaAdi { get; set; } = string.Empty;
        public int SatirSayisi { get; set; }
        public int DigerBankadaAtlanan { get; set; }
        public List<OrkaSatirDto> Satirlar { get; set; } = new();
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
        public List<string> Uyarilar { get; set; } = new();
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
