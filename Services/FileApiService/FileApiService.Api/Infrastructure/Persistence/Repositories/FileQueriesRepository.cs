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
                CreatedAtUtc = r.CreatedAtUtc,
                Description = r.Description,
                SequenceNo = r.SequenceNo
            });
        }

        public Task<IEnumerable<FileInfoDto>> GetFilesInfo(CancellationToken ct)
            => GetFilesInfo(null, null, null,null,null ,ct);
        public async Task<int> CountFiles(string? companyId, string? year, string? month, CancellationToken ct)
        {
            var q = _db.Files.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(companyId)) q = q.Where(x => x.CompanyId == companyId);
            if (!string.IsNullOrWhiteSpace(year)) q = q.Where(x => x.Year == year);
            if (!string.IsNullOrWhiteSpace(month)) q = q.Where(x => x.Month == month);
            return await q.CountAsync(ct);
        }
        public async Task<IEnumerable<FileInfoDto>> GetFilesInfo(
      string? companyId, string? year, string? month, string? declTypeNorm, string? docTypeNorm, CancellationToken ct)
        {
            var q = _db.Files.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(companyId)) q = q.Where(x => x.CompanyId == companyId);
            if (!string.IsNullOrWhiteSpace(year)) q = q.Where(x => x.Year == year);
            if (!string.IsNullOrWhiteSpace(month)) q = q.Where(x => x.Month == month);

            if (!string.IsNullOrWhiteSpace(declTypeNorm))
            {
                // Sol tarafı SQL'de normalize ediyoruz:
                q = q.Where(x =>
                    (x.DeclType ?? "") != "" &&
                    x.DeclType.Trim()
                              .ToUpper()
                              .Replace(" ", "")
                              .Replace("_", "")
                              .Replace("-", "")
                              .Replace("\u00A0", "") // NBSP
                              .Replace("\u2010", "") // HYPHEN
                              .Replace("\u2011", "") // NON-BREAKING HYPHEN
                              .Replace("\u2012", "") // FIGURE DASH
                              .Replace("\u2013", "") // EN DASH
                              .Replace("\u2014", "") // EM DASH
                              .Replace("\u2212", "") // MINUS SIGN
                    == declTypeNorm);
            }
            // docType normalize edilerek filtre
            if (!string.IsNullOrWhiteSpace(docTypeNorm))
            {
                q = q.Where(x =>
                    (x.DocType ?? "") != "" &&
                    x.DocType.Trim()
                             .ToUpper()
                             .Replace(" ", "")
                             .Replace("_", "")
                             .Replace("-", "")
                             .Replace("\u00A0", "")
                             .Replace("\u2010", "")
                             .Replace("\u2011", "")
                             .Replace("\u2012", "")
                             .Replace("\u2013", "")
                             .Replace("\u2014", "")
                             .Replace("\u2212", "")
                    == docTypeNorm);
            }
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
                              Key = x.Key,

                               Description = x.Description,
                              SequenceNo = x.SequenceNo
                          })
                          .ToListAsync(ct);
        }

    }

}
