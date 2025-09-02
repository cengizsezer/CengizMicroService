using FileApiService.Api.Domain.Dtos;

namespace FileApiService.Api.Core.Abstractions
{
    public interface IFileCommandsRepository
    {
        Task<int> AddFileMetaAsync(FileMetaDto dto, CancellationToken ct);
        Task UpsertCompanyDocMetaAsync(FileMetaDto dto, CancellationToken ct); // NEW
    }
}
