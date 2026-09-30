namespace WebApp.Shared.Dto.Yapilacaklar
{
    // Sunucudaki CatalogService.Api.Features.Yapilacaklar.Dtos'un aynası; sayısal enum
    // değerleri sözleşmenin parçası.

    public enum IsTekrari : byte
    {
        TekSefer = 1,
        Aylik = 2,
        UcAylik = 3,
        Yillik = 4
    }

    public enum IsGunKurali : byte
    {
        AyinGunu = 1,
        AySonu = 2,
        DonemSonu = 3
    }

    public enum IsKaynagi : byte
    {
        Yasal = 1,
        Ozel = 2
    }

    public enum IsDurumu : byte
    {
        Gecikti = 1,
        BuHafta = 2,
        BuAy = 3,
        Sonra = 4,
        Tamam = 5
    }

    public enum IsFiltresi : byte
    {
        Tumu = 0,
        Bende = 1,
        Yasal = 2,
        Ozel = 3
    }

    public class IsSatiriDto
    {
        public IsKaynagi KaynakTip { get; set; }
        public int KaynakId { get; set; }
        public int FirmaId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;
        public string Baslik { get; set; } = string.Empty;
        public string? Aciklama { get; set; }
        public string? MukellefiyetKodu { get; set; }
        public IsTekrari Tekrar { get; set; }
        public string DonemAnahtari { get; set; } = string.Empty;
        public string? DonemEtiketi { get; set; }
        public DateTime SonGun { get; set; }
        public IsDurumu Durum { get; set; }
        public int Gun { get; set; }
        public bool Tamamlandi { get; set; }
        public DateTime? TamamlanmaZamani { get; set; }
        public string? TamamlayanAdi { get; set; }
        public DateTime? SonYapilma { get; set; }
        public int? SorumluKullaniciId { get; set; }
        public string? SorumluKullaniciAdi { get; set; }
    }

    public class FirmaIsiDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public string Baslik { get; set; } = string.Empty;
        public string? Aciklama { get; set; }
        public IsTekrari Tekrar { get; set; }
        public IsGunKurali GunKurali { get; set; }
        public int? AyinGunu { get; set; }
        public DateTime? TekSeferTarih { get; set; }
        public int? SorumluKullaniciId { get; set; }
        public string? SorumluKullaniciAdi { get; set; }
        public bool Aktif { get; set; }
        public string KuralMetni { get; set; } = string.Empty;
    }

    public class FirmaIsiKaydetDto
    {
        public string Baslik { get; set; } = string.Empty;
        public string? Aciklama { get; set; }
        public IsTekrari Tekrar { get; set; } = IsTekrari.Aylik;
        public IsGunKurali GunKurali { get; set; } = IsGunKurali.AyinGunu;
        public int? AyinGunu { get; set; } = 1;
        public DateTime? TekSeferTarih { get; set; }
        public int? SorumluKullaniciId { get; set; }
        public string? SorumluKullaniciAdi { get; set; }
        public bool Aktif { get; set; } = true;
    }

    public class FirmaIsleriKartDto
    {
        public int FirmaId { get; set; }
        public List<IsSatiriDto> Yasal { get; set; } = new();
        public List<IsSatiriDto> Ozel { get; set; } = new();
        public List<FirmaIsiDto> OzelTanimlar { get; set; } = new();
        public int GeciktiSayisi { get; set; }
        public int BuAySayisi { get; set; }
        public List<string> MukellefiyetKodlari { get; set; } = new();
        public List<string> TakvimdeOlmayanKodlar { get; set; } = new();
    }

    public class IsGrubuDto
    {
        public string Anahtar { get; set; } = string.Empty;
        public IsKaynagi KaynakTip { get; set; }
        public string Baslik { get; set; } = string.Empty;
        public IsTekrari Tekrar { get; set; }
        public string? DonemEtiketi { get; set; }
        public DateTime SonGun { get; set; }
        public IsDurumu Durum { get; set; }
        public int Gun { get; set; }
        public int FirmaSayisi { get; set; }
        public int TamamlananSayisi { get; set; }
        public List<IsSatiriDto> Firmalar { get; set; } = new();
    }

    public class YapilacaklarDto
    {
        public DateTime Bugun { get; set; }
        public IsFiltresi Filtre { get; set; }
        public List<IsGrubuDto> Gruplar { get; set; } = new();
    }

    public class YapilacaklarOzetDto
    {
        public int Gecikti { get; set; }
        public int BuHafta { get; set; }
    }

    public class IsIsaretDto
    {
        public IsKaynagi KaynakTip { get; set; }
        public int KaynakId { get; set; }
        public int FirmaId { get; set; }
        public string DonemAnahtari { get; set; } = string.Empty;

        public static IsIsaretDto Satirdan(IsSatiriDto s) => new()
        {
            KaynakTip = s.KaynakTip,
            KaynakId = s.KaynakId,
            FirmaId = s.FirmaId,
            DonemAnahtari = s.DonemAnahtari
        };
    }

    public class IsIsaretleDto
    {
        public bool Yapildi { get; set; }
        public List<IsIsaretDto> Isler { get; set; } = new();
    }

    public class VergiTakvimiDto
    {
        public int Id { get; set; }
        public string MukellefiyetKodu { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public IsTekrari Tekrar { get; set; }
        public int Yil { get; set; }
        public int DonemNo { get; set; }
        public DateTime DonemBas { get; set; }
        public DateTime DonemBit { get; set; }
        public DateTime SonGun { get; set; }
        public bool Aktif { get; set; }
        public int TamamlamaSayisi { get; set; }
    }

    public class VergiTakvimiKaydetDto
    {
        public string MukellefiyetKodu { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public IsTekrari Tekrar { get; set; } = IsTekrari.Aylik;
        public int Yil { get; set; }
        public int DonemNo { get; set; } = 1;
        public DateTime SonGun { get; set; }
        public bool Aktif { get; set; } = true;
    }

    /// <summary>Rozet ve etiket metinleri: renk tek başına anlam taşımasın, her rozette metin yazar.</summary>
    public static class IsMetin
    {
        public static string Rozet(IsDurumu durum, int gun) => durum switch
        {
            IsDurumu.Gecikti => $"Gecikti · {gun} gün",
            IsDurumu.BuHafta => gun == 0 ? "Bugün son gün" : $"Bu hafta · {gun} gün var",
            IsDurumu.BuAy => "Bu ay",
            IsDurumu.Sonra => "Sonra",
            _ => "Bu dönem yapıldı"
        };

        public static string RozetSinifi(IsDurumu durum) => durum switch
        {
            IsDurumu.Gecikti => "ys-rozet ys-rozet-gecikti",
            IsDurumu.BuHafta => "ys-rozet ys-rozet-hafta",
            IsDurumu.BuAy => "ys-rozet ys-rozet-ay",
            IsDurumu.Sonra => "ys-rozet ys-rozet-sonra",
            _ => "ys-rozet ys-rozet-tamam"
        };

        public static string Tekrar(IsTekrari t) => t switch
        {
            IsTekrari.Aylik => "her ay",
            IsTekrari.UcAylik => "üç ayda bir",
            IsTekrari.Yillik => "her yıl",
            _ => "tek sefer"
        };

        public static string Kaynak(IsKaynagi k) => k == IsKaynagi.Yasal ? "Yasal" : "Firmaya özel";
    }
}
