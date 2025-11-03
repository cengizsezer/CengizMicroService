using FileApiService.Api.Domain.Dtos;
using FluentResults;

namespace FileApiService.Api.Core.Abstractions
{
    public interface IFileQueriesRepository
    {
        Task<Result<FileMetaDto>> GetMetaByIdAsync(int id, CancellationToken ct);
        Task<IEnumerable<FileInfoDto>> GetFilesInfo(string? companyId, string? year, string? month, string? declType, string? docType, CancellationToken ct);
        Task<IEnumerable<FileInfoDto>> GetFilesInfo(CancellationToken ct);
        Task<int> CountFiles(string? companyId, string? year, string? month, CancellationToken ct);
       
    }
}
