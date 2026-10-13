namespace CatalogService.Api.Features.FirmaPortfoyu.Domain
{
    /// <summary>
    /// Firma Portföyü'nün aylık mizanı (Aşama 1). Bir firmanın bir ayı için TEK kayıt (FirmaId+Yil+Ay unique);
    /// aynı aya yeniden yükleme onayla eskisinin yerine geçer. Firma Kontrol'ün yıllık (önceki/cari dönem)
    /// mizanlarından tamamen ayrıdır.
    ///
    /// Gelir tablosu hesapları yıl başından KÜMÜLATİF saklanır (mizan nasıl geldiyse); aylık tutar okuma
    /// sırasında komşu ayla farktan türer (PortfoyHesaplayici). Global — tenant'a bakmaz, firma veri boyutudur.
    /// </summary>
    public class AylikMizan
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public int Yil { get; set; }

        /// <summary>1–12.</summary>
        public int Ay { get; set; }

        public string DosyaAdi { get; set; } = string.Empty;
        public DateTime YuklemeTarihi { get; set; }
        public int? YukleyenKullaniciId { get; set; }
        public string? YukleyenKullanici { get; set; }

        /// <summary>Yalnız 3 haneli ana hesaplar üzerinden (alt kırılımlar saklanmaz, çift sayılmaz).</summary>
        public decimal BorcBakiyeToplami { get; set; }

        public decimal AlacakBakiyeToplami { get; set; }

        /// <summary>Borç − alacak bakiye toplamı; 0 değilse mizan dengesizdir.</summary>
        public decimal DengeFarki { get; set; }

        /// <summary>Dengesiz mizan reddedilmez; kaydedilir ve "Mizan sorunlu" sayılır.</summary>
        public bool Dengeli { get; set; }

        /// <summary>Aralık mizanı kapanış kayıtlarından sonra alınmış (6xx sıfır, 690/692 ya da 590/591 dolu).</summary>
        public bool KapanisSonrasiMi { get; set; }

        public GiderYontemi GiderYontemi { get; set; }

        public int SatirSayisi { get; set; }

        public List<AylikMizanSatiri> Satirlar { get; set; } = new();
    }

    /// <summary>Mizanın bir ana hesap satırı. Bakiye borç-pozitif: Borç − Alacak.</summary>
    public class AylikMizanSatiri
    {
        public int Id { get; set; }
        public int AylikMizanId { get; set; }

        /// <summary>Üç haneli ana hesap kodu ("600").</summary>
        public string HesapKodu { get; set; } = string.Empty;

        public string? HesapAdi { get; set; }
        public decimal BorcBakiye { get; set; }
        public decimal AlacakBakiye { get; set; }
        public decimal Bakiye { get; set; }
    }

    /// <summary>Faaliyet giderlerinin hangi hesaplarda durduğu (firma + ay bazında tespit).</summary>
    public enum GiderYontemi : byte
    {
        /// <summary>Ne 630–632 ne 750/760/770 dolu.</summary>
        Belirsiz = 0,

        /// <summary>7/B: 630–632.</summary>
        YediB = 1,

        /// <summary>7/A: 750/760/770 (751/761/771 yansıtma hariç).</summary>
        YediA = 2,

        /// <summary>İkisi de dolu: 630–632 kullanılır, çift sayılmaz, uyarı verilir.</summary>
        Ikisi = 3
    }
}
