namespace WebApp.Shared.Dto.MevzuatGrafigi
{
    // CatalogService Features/MevzuatGrafigi/Dtos/MevzuatGrafigiDtos.cs ile birebir.
    // Alan eklerken iki tarafı birlikte değiştirin.

    /// <summary>Sunucudaki MevzuatKartTuru (byte). JSON'da sayı olarak gider; değerleri değiştirmeyin.</summary>
    public enum MevzuatKartTuru : byte
    {
        Firma = 1,
        Islem = 2,
        Istisna = 3,
        Indirim = 4,
        Kanun = 5,
        Sart = 6
    }

    public class MevzuatGrafigiDto
    {
        public List<MevzuatKartDto> Kartlar { get; set; } = new();
        public List<MevzuatBaglantiDto> Baglantilar { get; set; } = new();
        public List<MevzuatDislamaDto> Dislamalar { get; set; } = new();
        public List<MevzuatTuvalDto> Tuvaller { get; set; } = new();
        public List<MevzuatSartTakipDto> SartTakipleri { get; set; } = new();
    }

    public class MevzuatKartDto
    {
        public int Id { get; set; }
        public MevzuatKartTuru Tur { get; set; }
        public string Baslik { get; set; } = string.Empty;
        public string? Madde { get; set; }
        public string? Ozet { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public DateTime? GuncellemeTarihi { get; set; }
    }

    public class MevzuatKartSaveDto
    {
        public MevzuatKartTuru Tur { get; set; }
        public string Baslik { get; set; } = string.Empty;
        public string? Madde { get; set; }
        public string? Ozet { get; set; }
    }

    public class MevzuatBaglantiDto
    {
        public int Id { get; set; }
        public int KaynakKartId { get; set; }
        public int HedefKartId { get; set; }
    }

    public class MevzuatBaglantiSaveDto
    {
        public int KaynakKartId { get; set; }
        public int HedefKartId { get; set; }
    }

    /// <summary>AKartId her zaman BKartId'den küçüktür (sunucu sıralar).</summary>
    public class MevzuatDislamaDto
    {
        public int Id { get; set; }
        public int AKartId { get; set; }
        public int BKartId { get; set; }
        public string? Not { get; set; }
    }

    public class MevzuatDislamaSaveDto
    {
        public int AKartId { get; set; }
        public int BKartId { get; set; }
        public string? Not { get; set; }
    }

    public class MevzuatTuvalDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public int Sira { get; set; }
        public List<MevzuatYerlesimDto> Yerlesimler { get; set; } = new();
    }

    /// <summary>Sira boşsa oluştururken sona eklenir, güncellerken korunur.</summary>
    public class MevzuatTuvalSaveDto
    {
        public string Ad { get; set; } = string.Empty;
        public int? Sira { get; set; }
    }

    public class MevzuatYerlesimDto
    {
        public int KartId { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class MevzuatSartTakipDto
    {
        public int FirmaKartId { get; set; }
        public int SartKartId { get; set; }
        public bool Tamamlandi { get; set; }
    }
}
