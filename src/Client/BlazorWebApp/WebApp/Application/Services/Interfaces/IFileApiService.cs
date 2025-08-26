using Microsoft.AspNetCore.Components.Forms;
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
    }
}
