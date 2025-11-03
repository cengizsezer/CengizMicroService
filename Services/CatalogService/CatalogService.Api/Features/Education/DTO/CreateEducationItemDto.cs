using System.ComponentModel.DataAnnotations;

namespace CatalogService.Api.Features.Education.DTO
{
    /// <summary> Kayıt oluşturma isteği. (Düz metin) </summary>
    public sealed class CreateEducationItemDto
    {
        [Required, MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        /// <summary> Düz metin içerik (HTML yok). </summary>
        public string? BodyText { get; set; }

        /// <summary> Varsayılan true. </summary>
        public bool IsPublished { get; set; } = true;
    }
}
