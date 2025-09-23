using System.ComponentModel.DataAnnotations;

namespace CatalogService.Api.Core.Domain.Education
{
    /// <summary> Kayıt güncelleme isteği (partial). </summary>
    public sealed class UpdateEducationItemDto
    {
        [MaxLength(300)]
        public string? Title { get; set; }

        public string? BodyText { get; set; }

        public bool? IsPublished { get; set; }
    }
}
