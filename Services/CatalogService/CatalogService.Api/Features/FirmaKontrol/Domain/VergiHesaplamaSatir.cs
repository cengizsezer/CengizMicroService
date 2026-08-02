namespace CatalogService.Api.Features.FirmaKontrol.Domain
{
    /// <summary>
    /// Beyannamedeki bir kaleme girilen tutar. Kalem başına tek satır tutulur;
    /// tutarı sıfır olan kalemler için satır oluşturulmaz.
    /// </summary>
    public class VergiHesaplamaSatir
    {
        public int Id { get; set; }

        public int HesaplamaId { get; set; }
        public VergiHesaplama? Hesaplama { get; set; }

        public int VergiKalemiId { get; set; }
        public VergiKalemi? VergiKalemi { get; set; }

        /// <summary>Kullanıcının girdiği tutar. İstisnaya ilişkin KKEG'de bağlı istisnayı büyüten tutardır.</summary>
        public decimal Tutar { get; set; }

        /// <summary>Karşılaştırma için önceki dönem tutarı; hesaplamaya girmez.</summary>
        public decimal? OncekiDonem { get; set; }

        /// <summary>Kullanıcı notu; Excel çıktısında da yer alır.</summary>
        public string? Aciklama { get; set; }
    }
}
