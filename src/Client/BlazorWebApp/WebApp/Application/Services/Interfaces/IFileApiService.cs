using Microsoft.AspNetCore.Components.Forms;
using WebApp.Shared.Dto.CompanyDoc;
using WebApp.Shared.Dto.Declaration;

namespace WebApp.Application.Services.Interfaces
{
    public interface IFileApiService
    {
        Task<bool> UploadAsync(IBrowserFile file, string companyId, int year, int month,
           string declType, string docType, CancellationToken ct = default);
        Task<List<FileInfoDto>?> ListAsync(string companyId, int year, int month, CancellationToken ct = default);
        Task<List<FileInfoDto>?> ListAsyncForDeclType(string companyId, int year, int month, string declType, CancellationToken ct = default);
        Task<FileDto?> GetDownloadAsync(int id, CancellationToken ct = default);

        /// <summary>Modüle bağlı olmayan genel dosya yükler ve FileApiService kaydının Id'sini döner.</summary>
        Task<GenericUploadResultDto?> UploadGenericAsync(IBrowserFile file, string folder, CancellationToken ct = default);

        /// <summary>
        /// Ham byte'lardan genel dosya yükler (ör. panodan yapıştırılan görsel; IBrowserFile yoktur)
        /// ve FileApiService kaydının Id'sini döner. Aynı /uploads endpoint'ini kullanır.
        /// </summary>
        Task<GenericUploadResultDto?> UploadGenericBytesAsync(byte[] bytes, string fileName, string contentType, string folder, CancellationToken ct = default);

        Task<bool> UploadCompanyDocAsync(
    IBrowserFile file, string companyId, int year,
    string docCategory, string? description = null, int? sequenceNo = null,
    CancellationToken ct = default);


        Task<List<CompanyDocInfoDto>?> ListCompanyDocsAsync(string companyId, string? year = null, string? docCategory = null, CancellationToken ct = default);

        Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    }
}
