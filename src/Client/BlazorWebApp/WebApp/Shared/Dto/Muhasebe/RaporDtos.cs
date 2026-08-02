namespace WebApp.Shared.Dto.Muhasebe
{
    // ---- Ekstre (T cetveli) ----

    /// <summary>
    /// T cetvelinin bir kolonundaki hareket satırı. Satıra tıklanınca
    /// <see cref="FisId"/> numaralı fiş açılır.
    /// </summary>
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
        /// T cetvelinde kolonların en üstünde ayrı satır olarak gösterilir.
        /// <see cref="Bas"/> verilmemişse 0'dır (rapor zaten tüm geçmişi kapsar).
        /// </summary>
        public decimal DevirBorc { get; set; }
        public decimal DevirAlacak { get; set; }
        public decimal DevirBakiye { get; set; }

        /// <summary>Dönem içi hareket toplamları; devir hariçtir.</summary>
        public decimal ToplamBorc { get; set; }
        public decimal ToplamAlacak { get; set; }

        /// <summary>Kolon toplamları: devir + dönem hareketleri.</summary>
        public decimal KapanisBorc { get; set; }
        public decimal KapanisAlacak { get; set; }

        public decimal Bakiye { get; set; }
        public BakiyeYonu Yon { get; set; }

        public List<EkstreSatirDto> TaslakHareketler { get; set; } = new();
        public TaslakOzetDto Taslak { get; set; } = new();
    }

    // ---- Masraf merkezi raporu ----

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

    // ---- Masraf merkezi tanımı (yazma) ----

    /// <summary>Yeni masraf merkezi. Kod firma içinde tekildir, en fazla 10 karakter.</summary>
    public class MasrafMerkeziYazDto
    {
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
    }
}
