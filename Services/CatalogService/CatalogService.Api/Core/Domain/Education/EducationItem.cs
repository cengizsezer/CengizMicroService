using System.ComponentModel.DataAnnotations;

namespace CatalogService.Api.Core.Domain.Education
{
    public class EducationItem
    {
        public int Id { get; set; }

        [Required, MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        /// <summary> Sadece düz metin. </summary>
        public string? BodyText { get; set; }

        public bool IsPublished { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
