namespace WebApp.Shared.Dto.FirmaKontrol
{
    /// <summary>Etiket boyutunun kapsamı. Sunucudaki <c>EtiketKapsami</c> ile aynı değerler.</summary>
    public enum EtiketKapsami : byte
    {
        TumFirmalar = 0,
        BuFirma = 1
    }

    /// <summary>Kuralın düğümle nasıl eşleştiği. Sunucudaki karşılığıyla aynı değerler.</summary>
    public enum EtiketEslesmeTipi : byte
    {
        /// <summary>Sondaki * joker: "600 1 21*" kendisini ve altındaki tüm kodları eşler.</summary>
        KodDeseni = 0,

        /// <summary>Hesap adında geçen metin; büyük/küçük harf ve Türkçe karakter duyarsız.</summary>
        AdIcerir = 1,

        /// <summary>Tam kod eşleşmesi, tek düğüm.</summary>
        TekSecim = 2
    }

    public class EtiketDegeriDto
    {
        public int Id { get; set; }
        public int BoyutId { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string? Renk { get; set; }
        public int Sira { get; set; }

        /// <summary>
        /// Etiketin karşılık geldiği firma ("PKF Aday" etiketi → PKF Aday firması).
        /// null = sistemde firması olmayan etiket ("Grup dışı"). Yönetsel görünüm ve grup
        /// matrisi etiketli tutarın kime ait olduğunu buradan bilir.
        /// </summary>
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
        public int? FirmaId { get; set; }
    }

    public class EtiketBoyutuDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public EtiketKapsami Kapsam { get; set; }
        public int? FirmaId { get; set; }
        public int Sira { get; set; }

        /// <summary>Virgülle ayrılmış ana hesap kodu deseni listesi ("600,601,610" / "6*").</summary>
        public string KapsamHesaplari { get; set; } = string.Empty;

        public List<EtiketDegeriDto> Degerler { get; set; } = new();
        public List<EtiketKuraliDto> Kurallar { get; set; } = new();
    }

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
        public int? FirmaId { get; set; }
    }

    public class EtiketKuralSiraDto
    {
        public long KuralId { get; set; }
        public int Sira { get; set; }
    }
}
