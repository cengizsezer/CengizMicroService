namespace WebApp.Shared.Dto.Yapilacaklar
{
    // Sunucudaki CatalogService.Api.Features.Yapilacaklar.Dtos.DonemPanosuDtos'un aynası.

    /// <summary>Üçü ayrı görünür: eksik ile "bu firmada yok" aynı DEĞİL.</summary>
    public enum DonemHucreDurumu : byte
    {
        Yok = 0,
        Eksik = 1,
        Yapildi = 2
    }

    public class DonemPanosuKolonDto
    {
        public string Anahtar { get; set; } = string.Empty;
        public string Baslik { get; set; } = string.Empty;
        public IsKaynagi KaynakTip { get; set; }
        public IsTekrari Tekrar { get; set; }
        public string? MukellefiyetKodu { get; set; }
    }

    public class DonemPanosuHucreDto
    {
        public DonemHucreDurumu Durum { get; set; }
        public IsKaynagi KaynakTip { get; set; }
        public int KaynakId { get; set; }
        public string DonemAnahtari { get; set; } = string.Empty;
        public int EkSayisi { get; set; }
        public bool NotVar { get; set; }
    }

    public class DonemPanosuSatirDto
    {
        public int FirmaId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;
        public List<DonemPanosuHucreDto> Hucreler { get; set; } = new();
        public int Gecerli { get; set; }
        public int Yapilan { get; set; }
    }

    public class DonemEksikOzetiDto
    {
        public string Baslik { get; set; } = string.Empty;
        public int FirmaSayisi { get; set; }
    }

    public class DonemPanosuDto
    {
        public int Yil { get; set; }
        public int Ay { get; set; }
        public string Etiket { get; set; } = string.Empty;
        public List<DonemPanosuKolonDto> Kolonlar { get; set; } = new();
        public List<DonemPanosuSatirDto> Satirlar { get; set; } = new();
        public int Toplam { get; set; }
        public int Yapilan { get; set; }
        public List<DonemEksikOzetiDto> Eksikler { get; set; } = new();

        /// <summary>Firma filtresi; doluysa sayılar yalnız bu firmayı sayar.</summary>
        public int? FirmaId { get; set; }

        public List<DonemIsSatiriDto> Isler { get; set; } = new();
    }

    /// <summary>Dönem sheet satırı: firma × iş (matrisin aynı hücresi).</summary>
    public class DonemIsSatiriDto
    {
        public int FirmaId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;
        public string KolonAnahtari { get; set; } = string.Empty;
        public string Baslik { get; set; } = string.Empty;
        public IsKaynagi KaynakTip { get; set; }
        public int KaynakId { get; set; }
        public string DonemAnahtari { get; set; } = string.Empty;
        public string? MukellefiyetKodu { get; set; }
        public IsTekrari Tekrar { get; set; }
        public DonemHucreDurumu Durum { get; set; }
        public int EkSayisi { get; set; }
        public bool NotVar { get; set; }
        public DateTime? SonGun { get; set; }
        public int? SistemId { get; set; }
        public string? SistemAdi { get; set; }
        public List<string> Kime { get; set; } = new();
        public List<string> Bilgi { get; set; } = new();

        /// <summary>Özel işin kayıtlı başlangıç ayı (Prompt 17); boşsa "…'dan beri" notu çıkmaz.</summary>
        public string? IlkDonem { get; set; }

        public IsIsaretDto Isaret() => new() { KaynakTip = KaynakTip, KaynakId = KaynakId, FirmaId = FirmaId, DonemAnahtari = DonemAnahtari };
    }

    // ---- Takip panosu: sekme şeridi ve Özet (Prompt 14) ----

    /// <summary>Eksik rengi; eşik sunucuda tek yerde. İstemci eşik tanımlamaz, yalnız boyar.</summary>
    public enum EksikTonu : byte
    {
        Yok = 0,
        Az = 1,
        Cok = 2
    }

    public class DonemSekmesiDto
    {
        public int Yil { get; set; }
        public int Ay { get; set; }
        public string Anahtar { get; set; } = string.Empty;
        public string Etiket { get; set; } = string.Empty;
        public int Toplam { get; set; }
        public int Yapilan { get; set; }
        public int Eksik { get; set; }
        public int EksikFirmaSayisi { get; set; }
        public EksikTonu Ton { get; set; }
        public bool IcindeBulunulan { get; set; }
        public List<DonemEksikOzetiDto> Eksikler { get; set; } = new();
    }

    public class DonemOzetHucreDto
    {
        public int Gecerli { get; set; }
        public int Yapilan { get; set; }
        public EksikTonu Ton { get; set; }
    }

    public class DonemOzetSatirDto
    {
        public int FirmaId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;
        public int? SorumluKullaniciId { get; set; }
        public string? SorumluKullaniciAdi { get; set; }
        public List<DonemOzetHucreDto> Hucreler { get; set; } = new();
    }

    public class DonemOzetiDto
    {
        public List<DonemSekmesiDto> Donemler { get; set; } = new();
        public List<DonemOzetSatirDto> Satirlar { get; set; } = new();
        public string VarsayilanAnahtar { get; set; } = string.Empty;
        public int KirmiziEsik { get; set; }
    }

    public class GecenDonemDto
    {
        public string DonemAnahtari { get; set; } = string.Empty;
        public string? DonemEtiketi { get; set; }
        public bool Yapildi { get; set; }
        public DateTime? TamamlanmaZamani { get; set; }
        public string? TamamlayanAdi { get; set; }
        public string? Not { get; set; }
        public List<FirmaIsiEkiDto> Ekler { get; set; } = new();
    }

    public class DonemHucresiDto
    {
        public int FirmaId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;
        public string Baslik { get; set; } = string.Empty;
        public IsKaynagi KaynakTip { get; set; }
        public int KaynakId { get; set; }
        public string DonemAnahtari { get; set; } = string.Empty;
        public string? DonemEtiketi { get; set; }
        public string? MukellefiyetKodu { get; set; }
        public IsTekrari Tekrar { get; set; }

        /// <summary>Dönem kaydı var (yapılmış ya da yalnız not/kanıt) — "Bu dönem kaydını sil" için.</summary>
        public bool KayitVar { get; set; }

        public bool Yapildi { get; set; }
        public DateTime? TamamlanmaZamani { get; set; }
        public string? TamamlayanAdi { get; set; }
        public string? Not { get; set; }
        public List<FirmaIsiEkiDto> Ekler { get; set; } = new();
        public GecenDonemDto? GecenDonem { get; set; }

        /// <summary>"Nasıl yapılır" sekmesinde bu işin tarifi: ?sekme=nasil&amp;is=…</summary>
        public string TarifAnahtari { get; set; } = string.Empty;

        public IsIsaretDto Isaret() => new()
        {
            KaynakTip = KaynakTip, KaynakId = KaynakId, FirmaId = FirmaId, DonemAnahtari = DonemAnahtari
        };
    }

    public class DonemNotuDto : IsIsaretDto
    {
        public string? Not { get; set; }
    }

    public class DonemEkiOlusturDto : IsIsaretDto
    {
        public int FileId { get; set; }
        public string DosyaAdi { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public long Boyut { get; set; }
    }
}
