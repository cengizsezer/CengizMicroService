using FileApiService.Api.Core.Abstractions;
using FileApiService.Api.Domain.Dtos;
using FileApiService.Api.Infrastructure.Persistence.Entities;
using FluentResults;
using Microsoft.EntityFrameworkCore;

namespace FileApiService.Api.Infrastructure.Persistence.Repositories
{
    public sealed class FileQueriesRepository : IFileQueriesRepository
    {
        private readonly FileDbContext _db;
        public FileQueriesRepository(FileDbContext db) => _db = db;

        public async Task<Result<FileMetaDto>> GetMetaByIdAsync(int id, CancellationToken ct)
        {
            var r = await _db.Files.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (r is null) return Result.Fail($"File not found. Id={id}");
            return Result.Ok(new FileMetaDto
            {
                Id = r.Id,
                CompanyId = r.CompanyId,
                Year = r.Year,
                Month = r.Month,
                DeclType = r.DeclType,
                DocType = r.DocType,
                Key = r.Key,
                FileName = r.FileName,
                ContentType = r.ContentType,
                Length = r.Length,
                CreatedAtUtc = r.CreatedAtUtc
            });
        }

        public Task<IEnumerable<FileInfoDto>> GetFilesInfo(CancellationToken ct)
            => GetFilesInfo(null, null, null, ct);

        public async Task<IEnumerable<FileInfoDto>> GetFilesInfo(string? companyId, string? year, string? month, CancellationToken ct)
        {
            var q = _db.Files.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(companyId)) q = q.Where(x => x.CompanyId == companyId);
            if (!string.IsNullOrWhiteSpace(year)) q = q.Where(x => x.Year == year);
            if (!string.IsNullOrWhiteSpace(month)) q = q.Where(x => x.Month == month);

            return await q.OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => new FileInfoDto
                {
                    Id = x.Id,
                    CompanyId = x.CompanyId,
                    Year = x.Year,
                    Month = x.Month,
                    DeclType = x.DeclType,
                    DocType = x.DocType,
                    FileName = x.FileName,
                    ContentType = x.ContentType,
                    Length = x.Length,
                    CreatedAtUtc = x.CreatedAtUtc,
                    Key = x.Key
                })
                .ToListAsync(ct);
        }
    }

}
