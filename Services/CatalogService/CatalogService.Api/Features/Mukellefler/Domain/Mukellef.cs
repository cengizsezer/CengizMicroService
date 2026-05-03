namespace CatalogService.Api.Features.Mukellefler.Domain
{
    public class Mukellef
    {
        public int Id { get; set; }

        public string SozlesmeNo { get; set; } = string.Empty;
        public DateTime SozlesmeTarihi { get; set; }

        public string Unvan { get; set; } = string.Empty;
        public string VergiKimlikNo { get; set; } = string.Empty;

        public MukellefDurumu Durum { get; set; } = MukellefDurumu.DevamEdiyor;
        public DateTime? IptalFeshTarihi { get; set; }

        public string Hizmet { get; set; } = string.Empty;
        public string Telefon { get; set; } = string.Empty;
        public string? Email { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
