using CatalogService.Api.Infrastructure.Domain;

namespace CatalogService.Api.Features.Muhasebe.Domain
{
    /// <summary>
    /// Tekdüzen hesap planı düğümü (sınıf → grup → kebir → muavin, n seviyeli).
    /// Firma (tenant) bazlıdır; ağaç self-referans + materialized <see cref="Yol"/> ile tutulur.
    /// </summary>
    public class HesapPlani : TenantEntity
    {
        public int Id { get; set; }

        /// <summary>Üst hesap (kök sınıf ise null).</summary>
        public int? UstHesapId { get; set; }

        /// <summary>Tam kod, ör. "102.01.01.0046".</summary>
        public string Kod { get; set; } = string.Empty;

        /// <summary>Ayraçsız düz kod, sıralama için. Ör. "10201010046".</summary>
        public string KodDuz { get; set; } = string.Empty;

        /// <summary>Bu düğümün yalnızca son segmenti, ör. "0046".</summary>
        public string SegmentKod { get; set; } = string.Empty;

        public string Ad { get; set; } = string.Empty;

        /// <summary>1=sınıf, 2=grup, 3=kebir, 4+=muavin.</summary>
        public byte Seviye { get; set; }

        public HesapTuru HesapTuru { get; set; }

        public HesapKarakter Karakter { get; set; }

        /// <summary>Yalnızca yaprak hesap hareket görür.</summary>
        public bool HareketGorur { get; set; }

        /// <summary>MSUGT standart hesabı → kodu kilitli, silinemez.</summary>
        public bool SistemHesabi { get; set; }

        /// <summary>Döviz takipli hesaplarda dolu, ör. "USD".</summary>
        public string? ParaBirimi { get; set; }

        /// <summary>TCMB EFT katılımcı kodu (banka muavinlerinde).</summary>
        public string? BankaKodu { get; set; }

        public string? Iban { get; set; }

        /// <summary>Materialized path, ör. "/1/10/102/". Üst hesaptan türetilir.</summary>
        public string Yol { get; set; } = string.Empty;

        public bool Aktif { get; set; } = true;

        public HesapPlani? UstHesap { get; set; }
        public ICollection<HesapPlani> AltHesaplar { get; set; } = new List<HesapPlani>();
    }
}
