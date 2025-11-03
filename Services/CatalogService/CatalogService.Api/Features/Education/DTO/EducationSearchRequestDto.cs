namespace CatalogService.Api.Features.Education.DTO
{
    /// <summary> Arama + sayfalama istek DTO’su. </summary>
    public sealed class EducationSearchRequestDto
    {
        public string? Q { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        /// <summary> createdAtDesc (default), createdAtAsc, titleAsc, titleDesc </summary>
        public string? OrderBy { get; set; } = "createdAtDesc";
    }
}
