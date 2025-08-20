namespace FileApiService.Api.Domain.Abstractions
{
    public interface IFileProxy
    {

        string ContentType { get; }
        long Length { get; }
        string FileName { get; }
        IDictionary<string, string> Metadata { get; }  // CompanyId, Year, Month, DeclType, DocType
        Task CopyToAsync(Stream target, CancellationToken ct = default);
        Task<byte[]> GetData(CancellationToken ct = default); // presigned akışında kullanmıyoruz ama dursun
    }
}
