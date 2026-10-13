namespace WebApp.Shared.Dto.FirmaPortfoyu
{
    // Sunucudaki CatalogService.Api.Features.FirmaPortfoyu.Dtos'un aynası (Firma Portföyü Aşama 1).

    public enum GiderYontemi : byte
    {
        Belirsiz = 0,
        YediB = 1,
        YediA = 2,
        Ikisi = 3
    }

    public class AylikMizanSatiriDto
    {
        public string HesapKodu { get; set; } = string.Empty;
        public string? HesapAdi { get; set; }
        public decimal BorcBakiye { get; set; }
        public decimal AlacakBakiye { get; set; }
    }

    public class AylikMizanKaydetDto
    {
        public int FirmaId { get; set; }
        public int Yil { get; set; }
        public int Ay { get; set; }
        public string? DosyaAdi { get; set; }
        public bool UzerineYaz { get; set; }
        public List<AylikMizanSatiriDto> Satirlar { get; set; } = new();
    }

    public class MevcutMizanDto
    {
        public string DosyaAdi { get; set; } = string.Empty;
        public DateTime YuklemeTarihi { get; set; }
        public string? YukleyenKullanici { get; set; }
        public bool Dengeli { get; set; }
    }

    public class AylikMizanOnizlemeDto
    {
        public int FirmaId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;
        public int Yil { get; set; }
        public int Ay { get; set; }
        public int SatirSayisi { get; set; }
        public decimal BorcToplami { get; set; }
        public decimal AlacakToplami { get; set; }
        public decimal DengeFarki { get; set; }
        public bool Dengeli { get; set; }
        public bool KapanisSonrasi { get; set; }
        public GiderYontemi GiderYontemi { get; set; }
        public bool GelirTablosuVar { get; set; }
        public decimal? NetSatisKumulatif { get; set; }
        public decimal? SmmKumulatif { get; set; }
        public decimal? FaaliyetGiderleriKumulatif { get; set; }
        public List<string> Uyarilar { get; set; } = new();
        public MevcutMizanDto? Mevcut { get; set; }
        public bool Kaydedildi { get; set; }
    }

    public class MizanDurumHucresiDto
    {
        public int Ay { get; set; }
        public bool Yuklu { get; set; }
        public bool Dengeli { get; set; }
        public bool KapanisSonrasi { get; set; }
        public string? DosyaAdi { get; set; }
        public DateTime? YuklemeTarihi { get; set; }
        public string? YukleyenKullanici { get; set; }
        public int SatirSayisi { get; set; }
        public decimal DengeFarki { get; set; }
        public List<string> Uyarilar { get; set; } = new();
    }

    public class MizanDurumFirmasiDto
    {
        public int FirmaId { get; set; }
        public string KisaAd { get; set; } = string.Empty;
        public string Unvan { get; set; } = string.Empty;
        public string? Sorumlu { get; set; }
        public List<MizanDurumHucresiDto> Aylar { get; set; } = new();
        public int YukluAySayisi { get; set; }
    }

    public class MizanDurumuDto
    {
        public int Yil { get; set; }
        public List<MizanDurumFirmasiDto> Firmalar { get; set; } = new();
    }

    // ---- Aşama 2: portföy göstergeleri (firmalar arası toplam yok) ----

    public enum PortfoyMizanDurumu : byte
    {
        Tamam = 0,
        Eksik = 1,
        Dengesiz = 2
    }

    public class GostergeDto
    {
        public decimal? Deger { get; set; }
        public decimal? Karsilastirma { get; set; }
        public string? Not { get; set; }
    }

    public class PortfoyGostergeleriDto
    {
        public GostergeDto NetSatis { get; set; } = new();
        public GostergeDto BrutKar { get; set; } = new();
        public GostergeDto FaaliyetGiderleri { get; set; } = new();
        public GostergeDto FaaliyetKari { get; set; } = new();
        public GostergeDto DonemKari { get; set; } = new();
        public GostergeDto DonemKariYilBasindan { get; set; } = new();
        public GostergeDto HazirDegerler { get; set; } = new();
        public GostergeDto TicariAlacaklar { get; set; } = new();
        public GostergeDto TicariBorclar { get; set; } = new();
        public GostergeDto DevredenKdv { get; set; } = new();
    }

    public class PortfoyAyDto
    {
        public int Yil { get; set; }
        public int Ay { get; set; }
        public bool Yuklu { get; set; }
        public bool Dengeli { get; set; }
        public decimal? NetSatis { get; set; }
        public decimal? Smm { get; set; }
        public decimal? FaaliyetGiderleri { get; set; }
        public decimal? DonemKari { get; set; }
        public decimal? HazirDegerler { get; set; }
        public string? Not { get; set; }
    }

    public class PortfoyFirmasiDto
    {
        public int FirmaId { get; set; }
        public string KisaAd { get; set; } = string.Empty;
        public string Unvan { get; set; } = string.Empty;
        public string? Sorumlu { get; set; }
        public PortfoyMizanDurumu Durum { get; set; }
        public int? EksikAySayisi { get; set; }
        public string? SonMizanDonemi { get; set; }
        public List<string> Uyarilar { get; set; } = new();
        public PortfoyGostergeleriDto Gostergeler { get; set; } = new();
        /// <summary>Seçili dönemin yılının Ocak–Aralık'ı (12 eleman); mizanı olmayan ve gelecek ay boş, Not nedeni.</summary>
        public List<PortfoyAyDto> Seri { get; set; } = new();
    }

    public class PortfoyDto
    {
        public string Donem { get; set; } = string.Empty;
        public int Yil { get; set; }
        public int Ay { get; set; }
        public string Karsilastirma { get; set; } = "ay";
        public string KarsilastirmaDonemi { get; set; } = string.Empty;
        public int AktifFirmaSayisi { get; set; }
        public int MizaniYuklenenFirmaSayisi { get; set; }
        public List<PortfoyFirmasiDto> Firmalar { get; set; } = new();
    }
}
