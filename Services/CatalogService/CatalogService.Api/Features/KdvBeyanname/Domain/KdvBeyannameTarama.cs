using CatalogService.Api.Features.Firmalar.Domain;

namespace CatalogService.Api.Features.KdvBeyanname.Domain
{
    public class KdvBeyannameTarama
    {
        public long Id { get; set; }
        public int FirmaId { get; set; }
        public Firma? Firma { get; set; }

        public DateTime BaslangicTarihi { get; set; }
        public DateTime BitisTarihi { get; set; }

        public KdvBeyannameTaramaDurumu Durum { get; set; } = KdvBeyannameTaramaDurumu.Beklemede;

        public DateTime BaslangicAt { get; set; } = DateTime.UtcNow;
        public DateTime? BitisAt { get; set; }
        public string? HataMesaji { get; set; }

        public int? BulunanFaturaSayisi { get; set; }
    }
}
