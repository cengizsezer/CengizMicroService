using CatalogService.Api.Features.FirmaKontrol.Domain;

namespace CatalogService.Api.Features.FirmaKontrol.Dtos
{
    // Etiket TANIMLARI. Türetilmiş veri (hangi düğüme hangi etiket düştüğü) taşınmaz;
    // onu istemci kural motoru çalışma zamanında hesaplar.

    public class EtiketDegeriDto
    {
        public int Id { get; set; }
        public int BoyutId { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string? Renk { get; set; }
        public int Sira { get; set; }

        /// <summary>Etiketin karşılık geldiği firma; null = sistemde firması yok ("Grup dışı").</summary>
        public int? FirmaId { get; set; }
    }

    public class EtiketKuraliDto
    {
        public long Id { get; set; }
        public int BoyutId { get; set; }
        public int Sira { get; set; }
        public EtiketEslesmeTipi EslesmeTipi { get; set; }
        public string Desen { get; set; } = string.Empty;
        public int DegerId { get; set; }

        /// <summary>null = tüm firmalar.</summary>
        public int? FirmaId { get; set; }
    }

    public class EtiketBoyutuDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public EtiketKapsami Kapsam { get; set; }
        public int? FirmaId { get; set; }
        public int Sira { get; set; }

        /// <summary>Virgülle ayrılmış ana hesap kodu deseni listesi; sondaki * joker.</summary>
        public string KapsamHesaplari { get; set; } = string.Empty;

        public List<EtiketDegeriDto> Degerler { get; set; } = new();
        public List<EtiketKuraliDto> Kurallar { get; set; } = new();
    }

    // ── Yazma ──

    public class EtiketBoyutuYazDto
    {
        public string Ad { get; set; } = string.Empty;
        public EtiketKapsami Kapsam { get; set; }
        public int Sira { get; set; }

        /// <summary>Virgülle ayrılmış ana hesap kodu deseni listesi; boş olamaz.</summary>
        public string KapsamHesaplari { get; set; } = string.Empty;
    }

    public class EtiketDegeriYazDto
    {
        public string Ad { get; set; } = string.Empty;
        public string? Renk { get; set; }
        public int Sira { get; set; }

        /// <summary>Etiketin karşılık geldiği firma; boş bırakılabilir.</summary>
        public int? FirmaId { get; set; }
    }

    public class EtiketKuraliYazDto
    {
        public int Sira { get; set; }
        public EtiketEslesmeTipi EslesmeTipi { get; set; }
        public string Desen { get; set; } = string.Empty;
        public int DegerId { get; set; }

        /// <summary>null = tüm firmalar; dolu ise yalnızca o firmada geçerli.</summary>
        public int? FirmaId { get; set; }
    }

    /// <summary>Kural sırası güncellemesi (ilk eşleşen kazandığı için sıra anlamlıdır).</summary>
    public class EtiketKuralSiraDto
    {
        public long KuralId { get; set; }
        public int Sira { get; set; }
    }
}
