namespace CatalogService.Api.Features.BankaEkstre.Domain
{
    /// <summary>
    /// Ham açıklamadan karşı tarafın unvanını çıkaran regex. Banka bazlı, sıralı denenir,
    /// ilk yakalayan kazanır. Kod içine gömülmez.
    /// </summary>
    public class UnvanDeseni
    {
        public int Id { get; set; }

        public string ParserTipi { get; set; } = string.Empty;

        /// <summary>.NET regex. Büyük/küçük harf duyarlı çalışır (desenler ölçümde öyle kalibre edildi).</summary>
        public string Desen { get; set; } = string.Empty;

        /// <summary>Unvanın alınacağı yakalama grubu.</summary>
        public int GrupNo { get; set; } = 1;

        public int Sira { get; set; }

        public bool Aktif { get; set; } = true;

        /// <summary>Desenin ne yakaladığının insan okuru için açıklaması.</summary>
        public string? Aciklama { get; set; }
    }
}
