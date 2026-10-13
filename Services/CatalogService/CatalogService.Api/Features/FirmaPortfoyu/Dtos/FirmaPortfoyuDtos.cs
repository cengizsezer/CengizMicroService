using CatalogService.Api.Features.FirmaPortfoyu.Domain;

namespace CatalogService.Api.Features.FirmaPortfoyu.Dtos
{
    /// <summary>İstemcinin ExcelMizanParser ile okuduğu bir ana hesap satırı (dosya sunucuya gelmez).</summary>
    public class AylikMizanSatiriDto
    {
        public string HesapKodu { get; set; } = string.Empty;
        public string? HesapAdi { get; set; }
        public decimal BorcBakiye { get; set; }
        public decimal AlacakBakiye { get; set; }
    }

    /// <summary>POST mizan/onizle ve POST mizan gövdesi.</summary>
    public class AylikMizanKaydetDto
    {
        public int FirmaId { get; set; }
        public int Yil { get; set; }
        public int Ay { get; set; }
        public string? DosyaAdi { get; set; }

        /// <summary>Aynı firma + ay zaten varsa: true = eskisinin yerine yaz; false = 409.</summary>
        public bool UzerineYaz { get; set; }

        public List<AylikMizanSatiriDto> Satirlar { get; set; } = new();
    }

    /// <summary>Aynı firma + ayın mevcut mizanı (üzerine yazma sorusu için).</summary>
    public class MevcutMizanDto
    {
        public string DosyaAdi { get; set; } = string.Empty;
        public DateTime YuklemeTarihi { get; set; }
        public string? YukleyenKullanici { get; set; }
        public bool Dengeli { get; set; }
    }

    /// <summary>
    /// Önizleme ve kayıt sonucu — ikisi aynı değerlendirmeden (PortfoyHesaplayici.Degerlendir). Net satış, SMM
    /// ve faaliyet giderleri bu aşamada yıl başından KÜMÜLATİF (mizandaki gibi).
    /// </summary>
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

        /// <summary>Bu firma + ayın zaten yüklenmiş mizanı; yoksa null.</summary>
        public MevcutMizanDto? Mevcut { get; set; }

        /// <summary>Kayıt yapıldı mı (önizlemede false).</summary>
        public bool Kaydedildi { get; set; }
    }

    /// <summary>Firma × ay matrisinin bir hücresi (yüklenmemiş ayda Yuklu=false, diğer alanlar boş).</summary>
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

        /// <summary>Uyarılar (sorunlu saymaz): gelir düşük, 6xx yok, kapanış sonrası, iki gider yöntemi.</summary>
        public List<string> Uyarilar { get; set; } = new();
    }

    public class MizanDurumFirmasiDto
    {
        public int FirmaId { get; set; }
        public string KisaAd { get; set; } = string.Empty;
        public string Unvan { get; set; } = string.Empty;
        public string? Sorumlu { get; set; }

        /// <summary>12 hücre, Ocak → Aralık.</summary>
        public List<MizanDurumHucresiDto> Aylar { get; set; } = new();

        public int YukluAySayisi { get; set; }
    }

    /// <summary>GET mizan-durumu?yil= — yalnız AKTİF firmalar; firmalar arası tutar toplamı yok.</summary>
    public class MizanDurumuDto
    {
        public int Yil { get; set; }
        public List<MizanDurumFirmasiDto> Firmalar { get; set; } = new();
    }

    // ================= Aşama 2: portföy göstergeleri =================

    /// <summary>Seçili ayda firmanın mizan durumu. Eksik ve Dengesiz "Mizan sorunlu" sayılır.</summary>
    public enum PortfoyMizanDurumu : byte
    {
        Tamam = 0,

        /// <summary>Seçili ayın mizanı yok.</summary>
        Eksik = 1,

        /// <summary>Seçili ayın mizanı var, borç ≠ alacak.</summary>
        Dengesiz = 2
    }

    /// <summary>
    /// Bir gösterge: seçili ayın değeri ve karşılaştırma döneminin değeri (önceki ay ya da geçen yılın aynı ayı).
    /// Değer yoksa <see cref="Not"/> nedenini söyler ("mizan yüklenmedi", "önceki ay eksik", "kapanış sonrası").
    /// Yüzde değişimi ve rengi istemci hesaplar.
    /// </summary>
    public class GostergeDto
    {
        public decimal? Deger { get; set; }
        public decimal? Karsilastirma { get; set; }
        public string? Not { get; set; }
    }

    /// <summary>Seçili firmanın 9 göstergesi (taslaktaki sırayla; 10. gösterge portföy düzeyinde).</summary>
    public class PortfoyGostergeleriDto
    {
        /// <summary>Aylık.</summary>
        public GostergeDto NetSatis { get; set; } = new();

        /// <summary>Aylık; brüt marj = brüt kâr / net satış (istemci).</summary>
        public GostergeDto BrutKar { get; set; } = new();

        public GostergeDto FaaliyetGiderleri { get; set; } = new();
        public GostergeDto FaaliyetKari { get; set; } = new();

        /// <summary>Dönem kârı (vergi öncesi), AYLIK — firma tablosu için.</summary>
        public GostergeDto DonemKari { get; set; } = new();

        /// <summary>Yıl başından kümülatif. Karşılaştırma yalnız "geçen yıl" modunda (aynı dönem).</summary>
        public GostergeDto DonemKariYilBasindan { get; set; } = new();

        /// <summary>Bilanço — ayın bakiyesi.</summary>
        public GostergeDto HazirDegerler { get; set; } = new();

        public GostergeDto TicariAlacaklar { get; set; } = new();
        public GostergeDto TicariBorclar { get; set; } = new();
        public GostergeDto DevredenKdv { get; set; } = new();
    }

    /// <summary>12 aylık serinin bir ayı (aylık gelir tablosu + ay sonu hazır değerler).</summary>
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

        /// <summary>Durum Eksik ise: son mizandan seçili aya kaç ay eksik (hiç mizan yoksa null).</summary>
        public int? EksikAySayisi { get; set; }

        /// <summary>Seçili aya kadar (dahil) son yüklenen mizan, "2026-09"; hiç yoksa null.</summary>
        public string? SonMizanDonemi { get; set; }

        /// <summary>Seçili ayın mizan uyarıları (sorunlu saymaz).</summary>
        public List<string> Uyarilar { get; set; } = new();

        public PortfoyGostergeleriDto Gostergeler { get; set; } = new();

        /// <summary>
        /// Seçili dönemin yılının Ocak–Aralık'ı (12 eleman, Ocak'tan Aralık'a). Mizanı olmayan ve gelecek aylarda
        /// değerler null, Not nedeni söyler.
        /// </summary>
        public List<PortfoyAyDto> Seri { get; set; } = new();
    }

    /// <summary>
    /// GET firma-portfoyu?donem=&amp;karsilastir= — yalnız AKTİF firmalar, her firma kendi rakamlarıyla. Firmalar
    /// arası TOPLAM alanı yok (bilerek); portföy düzeyindeki tek bilgi mizanı yüklenen firma sayısı.
    /// </summary>
    public class PortfoyDto
    {
        /// <summary>"2026-09".</summary>
        public string Donem { get; set; } = string.Empty;

        public int Yil { get; set; }
        public int Ay { get; set; }

        /// <summary>"ay" (önceki ay) ya da "yil" (geçen yılın aynı ayı).</summary>
        public string Karsilastirma { get; set; } = "ay";

        public string KarsilastirmaDonemi { get; set; } = string.Empty;

        public int AktifFirmaSayisi { get; set; }
        public int MizaniYuklenenFirmaSayisi { get; set; }

        public List<PortfoyFirmasiDto> Firmalar { get; set; } = new();
    }
}
