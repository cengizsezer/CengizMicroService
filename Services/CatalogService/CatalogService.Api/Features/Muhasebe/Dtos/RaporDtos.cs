using CatalogService.Api.Features.Muhasebe.Domain;

namespace CatalogService.Api.Features.Muhasebe.Dtos
{
    /// <summary>Bakiyenin hangi tarafta kaldığı. T cetvelinde "borç kalanı" / "alacak kalanı" olarak yazılır.</summary>
    public enum BakiyeYonu : byte
    {
        Yok = 0,
        Borc = 1,
        Alacak = 2
    }

    /// <summary>Rapor uçlarının ortak tarih aralığı filtresi. Sınırlar dâhildir.</summary>
    public class RaporFiltreDto
    {
        public DateTime? Bas { get; set; }
        public DateTime? Bit { get; set; }
    }

    // ---- Mizan ----

    /// <summary>
    /// Mizan satırı. Tutarlar alt ağaç dâhildir (iş kuralı 19); yalnızca kesinleşmiş
    /// fişlerden gelir (iş kuralı 21) ve hiçbir tabloda saklanmaz (iş kuralı 18).
    /// </summary>
    public class MizanSatirDto
    {
        public int HesapId { get; set; }
        public int? UstHesapId { get; set; }
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public byte Seviye { get; set; }
        public HesapTuru HesapTuru { get; set; }
        public HesapKarakter Karakter { get; set; }
        public bool HareketGorur { get; set; }
        public bool Aktif { get; set; }

        /// <summary>
        /// Alt hesabı olmayan düğüm. <see cref="HareketGorur"/>den farklıdır: yaprak bir hesap
        /// hareket görmüyor olabilir. Tutarlar yalnızca yaprak satırlarda mükerrersizdir.
        /// </summary>
        public bool YaprakMi { get; set; }

        public decimal ToplamBorc { get; set; }
        public decimal ToplamAlacak { get; set; }

        /// <summary>Mizan kolonu: borç kalanı varsa tutarı, yoksa 0.</summary>
        public decimal BorcBakiye { get; set; }

        /// <summary>Mizan kolonu: alacak kalanı varsa tutarı, yoksa 0.</summary>
        public decimal AlacakBakiye { get; set; }

        /// <summary>İş kuralı 20: karaktere göre yönlü bakiye. Ağaç ekranındaki bakiye kolonu bunu kullanır.</summary>
        public decimal Bakiye { get; set; }

        public BakiyeYonu Yon { get; set; }
    }

    /// <summary>
    /// Mizan genel toplamı. Yaprak hesaplar üzerinden hesaplanır; satırlar alt ağaç
    /// toplamı taşıdığı için satırların toplanması mükerrer sayıma yol açardı.
    /// </summary>
    public class MizanToplamDto
    {
        public decimal ToplamBorc { get; set; }
        public decimal ToplamAlacak { get; set; }
        public decimal BorcBakiye { get; set; }
        public decimal AlacakBakiye { get; set; }

        /// <summary>Borç ve alacak toplamları eşit değilse UI uyarı bandı gösterir.</summary>
        public bool Dengede => ToplamBorc == ToplamAlacak && BorcBakiye == AlacakBakiye;
    }

    /// <summary>İş kuralı 21: taslak fişler mizana girmez, ayrı gösterilir.</summary>
    public class TaslakOzetDto
    {
        public int FisSayisi { get; set; }
        public decimal ToplamBorc { get; set; }
        public decimal ToplamAlacak { get; set; }
    }

    public class MizanDto
    {
        public DateTime? Bas { get; set; }
        public DateTime? Bit { get; set; }

        /// <summary>İstenen en derin seviye; boşsa tüm seviyeler döner.</summary>
        public byte? Seviye { get; set; }

        public List<MizanSatirDto> Satirlar { get; set; } = new();
        public MizanToplamDto GenelToplam { get; set; } = new();
        public TaslakOzetDto Taslak { get; set; } = new();
    }

    // ---- Ekstre (T cetveli) ----

    /// <summary>T cetvelinin bir kolonundaki hareket satırı. Satıra tıklanınca <see cref="FisId"/> açılır.</summary>
    public class EkstreSatirDto
    {
        public int FisId { get; set; }
        public string FisNo { get; set; } = string.Empty;
        public DateTime Tarih { get; set; }
        public FisTuru FisTuru { get; set; }
        public string? Aciklama { get; set; }
        public decimal Tutar { get; set; }

        /// <summary>Üst hesabın ekstresinde hareketin geldiği alt hesap.</summary>
        public int HesapId { get; set; }
        public string HesapKod { get; set; } = string.Empty;
    }

    /// <summary>
    /// T cetveli verisi. Üst hesap için alt ağacın tamamı toplanır (iş kuralı 19);
    /// yalnızca kesinleşmiş fişleri içerir, taslaklar ayrı listelenir (iş kuralı 21).
    /// </summary>
    public class EkstreDto
    {
        public int HesapId { get; set; }
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public byte Seviye { get; set; }
        public HesapTuru HesapTuru { get; set; }
        public HesapKarakter Karakter { get; set; }
        public bool HareketGorur { get; set; }
        public bool Aktif { get; set; }

        public DateTime? Bas { get; set; }
        public DateTime? Bit { get; set; }

        public List<EkstreSatirDto> BorcHareketleri { get; set; } = new();
        public List<EkstreSatirDto> AlacakHareketleri { get; set; } = new();

        /// <summary>
        /// Devir: <see cref="Bas"/> tarihinden önceki kesinleşmiş hareketlerin toplamı.
        /// T cetvelinde kolonların en üstünde devir satırı olarak gösterilir.
        /// <see cref="Bas"/> verilmemişse 0'dır (rapor zaten tüm geçmişi kapsar).
        /// </summary>
        public decimal DevirBorc { get; set; }
        public decimal DevirAlacak { get; set; }

        /// <summary>İş kuralı 20'ye göre yönlü devir bakiyesi.</summary>
        public decimal DevirBakiye { get; set; }

        /// <summary>Dönem içi hareket toplamları; devir hariçtir.</summary>
        public decimal ToplamBorc { get; set; }
        public decimal ToplamAlacak { get; set; }

        /// <summary>Kolon toplamları: devir + dönem hareketleri.</summary>
        public decimal KapanisBorc { get; set; }
        public decimal KapanisAlacak { get; set; }

        /// <summary>İş kuralı 20: karaktere göre yönlü kapanış bakiyesi (devir + dönem).</summary>
        public decimal Bakiye { get; set; }
        public BakiyeYonu Yon { get; set; }

        public List<EkstreSatirDto> TaslakHareketler { get; set; } = new();
        public TaslakOzetDto Taslak { get; set; } = new();
    }

    // ---- Masraf merkezi ----

    /// <summary>Masraf merkezi kırılımındaki bir hesabın tutarları.</summary>
    public class MasrafMerkeziHesapDto
    {
        public int HesapId { get; set; }
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public decimal Borc { get; set; }
        public decimal Alacak { get; set; }
    }

    public class MasrafMerkeziSatirDto
    {
        public int MasrafMerkeziId { get; set; }
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public bool Aktif { get; set; }

        public decimal ToplamBorc { get; set; }
        public decimal ToplamAlacak { get; set; }

        /// <summary>Masraf merkezi gider toplayıcıdır; bakiye <c>Borç − Alacak</c>.</summary>
        public decimal Bakiye { get; set; }

        public List<MasrafMerkeziHesapDto> Hesaplar { get; set; } = new();
    }

    public class MasrafMerkeziRaporDto
    {
        public DateTime? Bas { get; set; }
        public DateTime? Bit { get; set; }

        public List<MasrafMerkeziSatirDto> Satirlar { get; set; } = new();
        public decimal ToplamBorc { get; set; }
        public decimal ToplamAlacak { get; set; }

        /// <summary>Masraf merkezi seçilmemiş satırların toplamı; dağıtılmamış tutarı gösterir.</summary>
        public MasrafMerkeziSatirDto? Dagitilmamis { get; set; }
    }
}
