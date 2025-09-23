namespace CatalogService.Api.Core.Domain.Education
{
    /// <summary> Liste endpoint’i satırı için hafif DTO. </summary>
    public sealed class EducationItemListItemDto
    {
        public int Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? BodyText { get; init; } // UI gerekirse kısaltıp gösterebilirsin
        public bool IsPublished { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
