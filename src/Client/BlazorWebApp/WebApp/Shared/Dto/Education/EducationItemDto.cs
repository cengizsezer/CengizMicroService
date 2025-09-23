namespace WebApp.Shared.Dto.Education
{
    public sealed class EducationItemDto
    {
        public int Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? BodyText { get; init; }
        public bool IsPublished { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }

        // UI için yardımcı (accordion seçili vb.) — API’ye gitmez
        public bool Selected { get; set; }
    }
}
