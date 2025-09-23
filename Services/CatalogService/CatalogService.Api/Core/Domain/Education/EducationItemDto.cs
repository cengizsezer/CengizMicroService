namespace CatalogService.Api.Core.Domain.Education
{
    /// <summary> Detay/tekil kayıt DTO’su. </summary>
    public sealed class EducationItemDto
    {
        public int Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? BodyText { get; init; }
        public bool IsPublished { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }
}
