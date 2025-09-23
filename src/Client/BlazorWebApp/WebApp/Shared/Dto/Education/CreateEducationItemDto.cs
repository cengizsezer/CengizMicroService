namespace WebApp.Shared.Dto.Education
{
    public sealed class CreateEducationItemDto
    {
        public string Title { get; set; } = string.Empty;
        public string? BodyText { get; set; }
        public bool IsPublished { get; set; } = true;
    }
}
