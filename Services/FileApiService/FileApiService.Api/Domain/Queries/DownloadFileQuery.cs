namespace FileApiService.Api.Domain.Queries
{
    public sealed class DownloadFileQuery
    {
        public int Id { get; init; }

        public DownloadFileQuery(int id)
        {
            Id = id;
        }
    }
}
