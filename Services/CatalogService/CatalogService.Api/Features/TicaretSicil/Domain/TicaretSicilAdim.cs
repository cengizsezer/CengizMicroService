namespace CatalogService.Api.Features.TicaretSicil.Domain
{
    /// <summary>İşlemin sırayla yapılacak tek bir adımı.</summary>
    public class TicaretSicilAdim
    {
        public int Id { get; set; }
        public int IslemId { get; set; }

        public int Sira { get; set; }
        public string Baslik { get; set; } = string.Empty;
        public string? Aciklama { get; set; }
        public string? Not { get; set; }

        public ICollection<TicaretSicilEk> Ekler { get; set; } = new List<TicaretSicilEk>();
    }
}
