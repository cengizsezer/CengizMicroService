namespace FileApiService.Api.Domain.Queries
{
    public sealed class GetFilesInfoQuery
    {
        public string? CompanyId { get; init; }
        public string? Year { get; init; }
        public string? Month { get; init; } // "01".."12"
        public string? DeclType { get; init; }
        public string? DocType { get; init; }
    }
}
