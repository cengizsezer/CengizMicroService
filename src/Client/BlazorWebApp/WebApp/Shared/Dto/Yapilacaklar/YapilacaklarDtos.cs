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

    /// <summary>İş dialogunun program seçeneği (ortak sistem listesinden).</summary>
    public class IsSistemSecenegiDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public WebApp.Shared.Dto.Sistemler.SistemTuru Tur { get; set; }
        public bool Aktif { get; set; }
        public bool Firmanin { get; set; }
    }

    public enum AliciTipi : byte
    {
        Kime = 1,
        Bilgi = 2
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

        public string? YasalMukellefiyetKodu { get; set; }
        public int? SistemId { get; set; }
        public string? SistemAdi { get; set; }
        public string? MenuYolu { get; set; }
        public string? NasilYapilir { get; set; }
        public int? OnAdimiOlduguIsId { get; set; }
        public string? OnAdimiBaslik { get; set; }
        public List<FirmaIsiAlicisiDto> Alicilar { get; set; } = new();
        public int EkSayisi { get; set; }
    }

    public class FirmaIsiAlicisiDto
    {
        public int Id { get; set; }
        public string AdSoyad { get; set; } = string.Empty;
        public string? Eposta { get; set; }
        public string? Rol { get; set; }
        public AliciTipi AliciTipi { get; set; } = AliciTipi.Kime;
        public int Sira { get; set; }
    }

    public class IsProsedurKaydetDto
    {
        public int? SistemId { get; set; }
        public string? MenuYolu { get; set; }
        public string? NasilYapilir { get; set; }
        public int? OnAdimiOlduguIsId { get; set; }
        public string? OnAdimiYasalKodu { get; set; }
        public IsTekrari? OnAdimiYasalTekrar { get; set; }

        /// <summary><c>null</c> = alıcılara dokunma; liste = bütün alıcılar.</summary>
        public List<FirmaIsiAlicisiDto>? Alicilar { get; set; }
    }

    public class FirmaIsiEkiDto
    {
        public int Id { get; set; }
        public int FileId { get; set; }
        public string DosyaAdi { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Boyut { get; set; }
        public DateTime YuklemeZamani { get; set; }
        public string? YukleyenKullaniciAdi { get; set; }
    }

    public class FirmaIsiEkiOlusturDto
    {
        public int FileId { get; set; }
        public string DosyaAdi { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public long Boyut { get; set; }
    }

    public class IsGecmisSatiriDto
    {
        public string DonemAnahtari { get; set; } = string.Empty;
        public string? DonemEtiketi { get; set; }
        public DateTime SonGun { get; set; }
        public bool Yapildi { get; set; }
        public DateTime? TamamlanmaZamani { get; set; }
        public string? TamamlayanAdi { get; set; }
        public bool Yapilmadi { get; set; }
    }

    public class IsProsedurDto
    {
        public int? IsId { get; set; }
        public int FirmaId { get; set; }
        public IsKaynagi KaynakTip { get; set; }
        public string Baslik { get; set; } = string.Empty;
        public string? MukellefiyetKodu { get; set; }
        public IsTekrari Tekrar { get; set; }
        public string KuralMetni { get; set; } = string.Empty;
        public int? SistemId { get; set; }
        public string? SistemAdi { get; set; }
        public string? MenuYolu { get; set; }
        public string? NasilYapilir { get; set; }
        public List<string> Adimlar { get; set; } = new();
        public int? OnAdimiOlduguIsId { get; set; }
        public string? OnAdimiBaslik { get; set; }
        public List<FirmaIsiAlicisiDto> Alicilar { get; set; } = new();
        public List<FirmaIsiEkiDto> Ekler { get; set; } = new();
        public string? MizanFormati { get; set; }

        /// <summary>İş bir muhasebe programında ama firmanınki başka: "Firmanın muhasebe programı X" notu.</summary>
        public string? FarkliMuhasebeProgrami { get; set; }

        public List<IsGecmisSatiriDto> Gecmis { get; set; } = new();
        public int GecmisToplam { get; set; }

        // İş tarifi (Prompt 14): panel önce tarifi okur.
        public string TarifAnahtari { get; set; } = string.Empty;
        public int? TarifId { get; set; }
        public string? TarifSistemAdi { get; set; }
        public string? TarifMenuYolu { get; set; }
        public List<string> TarifAdimlar { get; set; } = new();
        public bool FirmayaOzel { get; set; }
    }

    public class FirmaIsiKaydetDto : IsProsedurKaydetDto
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
        public List<FirmaIsiDto> YasalProsedurler { get; set; } = new();
        /// <summary>Yeni işte program önerisi (firmanın muhasebe programı); kilit değil.</summary>
        public int? VarsayilanSistemId { get; set; }
        public List<IsSistemSecenegiDto> Sistemler { get; set; } = new();
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

        /// <summary>Elle eklendi ya da düzenlendi: seed dokunmaz (Prompt 7B).</summary>
        public bool ElleDuzenlendi { get; set; }
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

        /// <summary>Kart satırının prosedür tanımı: özelde kendi tanımı, yasalda kod + tekrar ile açılmış satır.</summary>
        public static FirmaIsiDto? Tanim(FirmaIsleriKartDto kart, IsSatiriDto s)
            => s.KaynakTip == IsKaynagi.Ozel
                ? kart.OzelTanimlar.FirstOrDefault(t => t.Id == s.KaynakId)
                : kart.YasalProsedurler.FirstOrDefault(t => t.YasalMukellefiyetKodu == s.MukellefiyetKodu && t.Tekrar == s.Tekrar);

        /// <summary>"Nasıl yapılır" metninin adımları — sunucudaki YapilacaklarKurucu.Adimlar ile aynı kural.</summary>
        public static List<string> Adimlar(string? metin)
            => string.IsNullOrWhiteSpace(metin)
                ? new List<string>()
                : metin.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
    }
}
