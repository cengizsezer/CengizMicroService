namespace CatalogService.Api.Features.Muhasebe.Domain
{
    /// <summary>
    /// Fiş satırı (çift taraflı kayıt). Bir satırda ya borç ya alacak dolu olur.
    /// Tenant izolasyonu bağlı olduğu <see cref="Fis"/> üzerinden sağlanır.
    /// </summary>
    public class FisSatir
    {
        public int Id { get; set; }

        public int FisId { get; set; }

        public short SiraNo { get; set; }

        public int HesapId { get; set; }

        public int? MasrafMerkeziId { get; set; }

        public string? Aciklama { get; set; }

        public decimal Borc { get; set; }

        public decimal Alacak { get; set; }

        public string ParaBirimi { get; set; } = "TRY";

        /// <summary>Döviz satırında döviz tutarı (zorunlu).</summary>
        public decimal? Doviz { get; set; }

        /// <summary>Döviz satırında kur (zorunlu). Borc/Alacak TL karşılığıdır.</summary>
        public decimal? Kur { get; set; }

        public Fis? Fis { get; set; }
        public HesapPlani? Hesap { get; set; }
        public MasrafMerkezi? MasrafMerkezi { get; set; }
    }
}
