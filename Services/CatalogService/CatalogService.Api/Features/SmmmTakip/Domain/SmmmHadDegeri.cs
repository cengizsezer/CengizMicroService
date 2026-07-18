namespace CatalogService.Api.Features.SmmmTakip.Domain
{
    /// <summary>Bir haddin belirli bir yıla ait değeri. (HadId, Yil) benzersizdir.</summary>
    public class SmmmHadDegeri
    {
        public int Id { get; set; }
        public int HadId { get; set; }

        public int Yil { get; set; }

        public decimal Deger { get; set; }

        /// <summary>İlgili tebliğ. Örn: "332 Seri No'lu GVK GT".</summary>
        public string? Teblig { get; set; }

        public string? Not { get; set; }

        public DateTime? GuncellenmeTarihi { get; set; }
    }
}
