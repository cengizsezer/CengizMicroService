namespace WebApp.Shared.Dto.Education
{
    public sealed class UpdateEducationItemDto
    {
        public string? Title { get; set; }
        public string? BodyText { get; set; }
        public bool? IsPublished { get; set; }
    }
}
