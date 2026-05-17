namespace CatalogService.Api.Features.KdvBeyanname.Domain
{
    // KDV beyannamesi XML'inde <duzenleyen> bloğunda yer alacak SMMM/YMM bilgisi.
    // Bir firma bir düzenleyen kullanır (Firma.DuzenleyenId). Soft delete: Aktif=false.
    public class Duzenleyen
    {
        public int Id { get; set; }

        // Dropdown'da görünen kısa isim (ör. "PKF SMMM").
        public string Kisaltma { get; set; } = string.Empty;

        public string Vkn { get; set; } = string.Empty;

        // BDP "Soyadı" = ünvanın 1. parçası, "Adı" = ünvanın 2. parçası.
        public string? Soyadi { get; set; }
        public string? Adi { get; set; }

        public string? TicaretSicilNo { get; set; }
        public string? Eposta { get; set; }
        public string? AlanKodu { get; set; }
        public string? TelNo { get; set; }

        public bool Aktif { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
