using CatalogService.Api.Features.TicaretSicil.Domain;

namespace CatalogService.Api.Features.TicaretSicil.Dtos
{
    // ---- Okuma (public + admin) ----

    public class TicaretSicilIslemListDto
    {
        public int Id { get; set; }
        public string Slug { get; set; } = string.Empty;
        public string Kategori { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public string Aciklama { get; set; } = string.Empty;
        public int Sira { get; set; }
        public int AdimSayisi { get; set; }
    }

    public class TicaretSicilIslemDetayDto
    {
        public int Id { get; set; }
        public string Slug { get; set; } = string.Empty;
        public string Kategori { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public string Aciklama { get; set; } = string.Empty;
        public int Sira { get; set; }
        public List<TicaretSicilAdimDto> Adimlar { get; set; } = new();
    }

    public class TicaretSicilAdimDto
    {
        public int Id { get; set; }
        public int Sira { get; set; }
        public string Baslik { get; set; } = string.Empty;
        public string? Aciklama { get; set; }
        public string? Not { get; set; }
        public List<TicaretSicilEkDto> Ekler { get; set; } = new();
    }

    public class TicaretSicilEkDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public EkTur Tur { get; set; }
        public string DosyaAdi { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public int FileId { get; set; }
    }

    // ---- Yazma (admin) ----

    public class TicaretSicilIslemSaveDto
    {
        public string Slug { get; set; } = string.Empty;
        public string Kategori { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public string Aciklama { get; set; } = string.Empty;
        public int Sira { get; set; }
    }

    public class TicaretSicilAdimSaveDto
    {
        public string Baslik { get; set; } = string.Empty;
        public string? Aciklama { get; set; }
        public string? Not { get; set; }
    }

    public class TicaretSicilEkSaveDto
    {
        public string Ad { get; set; } = string.Empty;
        public EkTur Tur { get; set; }
        public string DosyaAdi { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/pdf";
        public int FileId { get; set; }
    }
}
