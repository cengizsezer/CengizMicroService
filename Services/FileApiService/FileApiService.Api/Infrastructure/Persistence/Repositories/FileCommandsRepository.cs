using FileApiService.Api.Core.Abstractions;
using FileApiService.Api.Domain.Dtos;
using FileApiService.Api.Infrastructure.Persistence.Entities;

namespace FileApiService.Api.Infrastructure.Persistence.Repositories
{
    public sealed class FileCommandsRepository : IFileCommandsRepository
    {
        private readonly FileDbContext _db;
        public FileCommandsRepository(FileDbContext db) => _db = db;

        public async Task<int> AddFileMetaAsync(FileMetaDto dto, CancellationToken ct)
        {
            var rec = new FileRecord
            {
                CompanyId = dto.CompanyId,
                Year = dto.Year,
                Month = dto.Month,
                DeclType = dto.DeclType,
                DocType = dto.DocType,
                Key = dto.Key,
                FileName = dto.FileName,
                ContentType = dto.ContentType,
                Length = dto.Length,
                CreatedAtUtc = dto.CreatedAtUtc
            };
            _db.Files.Add(rec);
            await _db.SaveChangesAsync(ct);
            return rec.Id;
        }
    }
}
