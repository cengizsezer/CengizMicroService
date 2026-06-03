using CatalogService.Api.Features.Banka.Domain;

namespace CatalogService.Api.Features.Banka.Dtos
{
    public class NotDto
    {
        public int Id { get; set; }
        public int HesapId { get; set; }
        public NotKapsam Kapsam { get; set; }
        public DateTime? Tarih { get; set; }
        public int? Yil { get; set; }
        public int? Ay { get; set; }
        public string Metin { get; set; } = string.Empty;
        public bool Sabit { get; set; }
        public string? OlusturanKullanici { get; set; }
        public DateTime OlusturmaZamani { get; set; }
    }
}
