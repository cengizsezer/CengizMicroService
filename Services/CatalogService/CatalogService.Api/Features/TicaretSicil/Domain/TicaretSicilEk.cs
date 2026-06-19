namespace CatalogService.Api.Features.TicaretSicil.Domain
{
    public enum EkTur
    {
        Pdf = 0,
        Resim = 1
    }

    /// <summary>
    /// Bir adıma bağlı dilekçe/örnek belge. Asıl dosya FileApiService'te saklanır;
    /// burada yalnızca FileApiService'in döndürdüğü <see cref="FileId"/> ve görüntüleme
    /// için gerekli metadata tutulur.
    /// </summary>
    public class TicaretSicilEk
    {
        public int Id { get; set; }
        public int AdimId { get; set; }

        public string Ad { get; set; } = string.Empty;
        public EkTur Tur { get; set; } = EkTur.Pdf;

        public string DosyaAdi { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/pdf";

        /// <summary>FileApiService'teki dosya kaydının kimliği (/download?id=, /delete?id=).</summary>
        public int FileId { get; set; }

        public int Sira { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
