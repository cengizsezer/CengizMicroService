using CatalogService.Api.Features.MevzuatGrafigi.Domain;

namespace CatalogService.Api.Features.MevzuatGrafigi.Dtos
{
    /// <summary>GET / cevabı: grafiğin tamamı tek seferde (ekran bütün kartları birlikte çizer).</summary>
    public class MevzuatGrafigiDto
    {
        public List<MevzuatKartDto> Kartlar { get; set; } = new();
        public List<MevzuatBaglantiDto> Baglantilar { get; set; } = new();
        public List<MevzuatDislamaDto> Dislamalar { get; set; } = new();
        public List<MevzuatTuvalDto> Tuvaller { get; set; } = new();
        public List<MevzuatSartTakipDto> SartTakipleri { get; set; } = new();
    }

    /// <summary>
    /// Kart okuma DTO'su. Tarihler sunucuda set edilir.
    /// </summary>
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

    /// <summary>Kart oluşturma/güncelleme gövdesi.</summary>
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

    /// <summary>Dışlama; AKartId her zaman BKartId'den küçüktür (sunucu sıralar).</summary>
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

    /// <summary>Tuval oluşturma/güncelleme. Sira boşsa oluştururken sona eklenir, güncellerken korunur.</summary>
    public class MevzuatTuvalSaveDto
    {
        public string Ad { get; set; } = string.Empty;
        public int? Sira { get; set; }
    }

    /// <summary>Bir kartın tuvaldeki yeri. Tuvale ekleme ve toplu yerleşim PUT'unda da kullanılır.</summary>
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
