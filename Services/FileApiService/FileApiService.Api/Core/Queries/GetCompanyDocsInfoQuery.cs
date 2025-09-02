namespace FileApiService.Api.Core.Queries
{
    public sealed class GetCompanyDocsInfoQuery
    {
        public string CompanyId { get; init; } = default!;
        public string? Year { get; init; }
        public string? DocCategory { get; init; }
        public bool LatestOnly { get; init; } = false; // aynı kategori+yıl için en güncel tek kayıt
    }
}
