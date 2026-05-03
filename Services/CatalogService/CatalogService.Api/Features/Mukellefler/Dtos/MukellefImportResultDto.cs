namespace CatalogService.Api.Features.Mukellefler.Dtos
{
    public class MukellefImportResultDto
    {
        public int TotalRows { get; set; }
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public List<MukellefImportError> Errors { get; set; } = new();
    }

    public class MukellefImportError
    {
        public int Row { get; set; }
        public string? Field { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
