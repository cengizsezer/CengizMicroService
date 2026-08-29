using CatalogService.Api.Features.Declarations.Entities;

namespace CatalogService.Api.Features.Declarations.Dtos
{
    public class BeyannameEkDto
    {
        public int Id { get; set; }
        public int DeclarationId { get; set; }
        public BeyannameEkTuru Tur { get; set; }
        public int FileId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/pdf";
        public long Length { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? YukleyenKullanici { get; set; }
    }

    /// <summary>
    /// Yeni ek kaydı. Dosyanın kendisi <b>önce</b> FileApiService'e yüklenir
    /// (<c>POST /uploads</c>), dönen <c>Id</c> buraya <see cref="FileId"/> olarak yazılır —
    /// JobAttachment akışının aynısı.
    /// </summary>
    public class BeyannameEkOlusturDto
    {
        public BeyannameEkTuru Tur { get; set; }
        public int FileId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/pdf";
        public long Length { get; set; }
    }
}
