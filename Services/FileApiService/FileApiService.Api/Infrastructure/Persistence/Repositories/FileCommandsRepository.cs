using FileApiService.Api.Core.Abstractions;
using FileApiService.Api.Domain.Dtos;
using FileApiService.Api.Infrastructure.Persistence;
using FileApiService.Api.Infrastructure.Persistence.Entities;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace FileApiService.Api.Infrastructure.Persistence.Repositories
{
    public sealed class FileCommandsRepository : IFileCommandsRepository
    {
        private readonly FileDbContext _db;
        public FileCommandsRepository(FileDbContext db) => _db = db;


        public async Task DeleteFileMetaAsync(int id, CancellationToken ct)
        {
            var rec = await _db.Files.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (rec == null) return;

            _db.Files.Remove(rec);                // Soft delete istiyorsan burada işaretle
            await _db.SaveChangesAsync(ct);
        }
        // INSERT (genel kullanım)
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
                CreatedAtUtc = dto.CreatedAtUtc,
                  Description = dto.Description,
                SequenceNo = dto.SequenceNo
            };

            _db.Files.Add(rec);
            await _db.SaveChangesAsync(ct);
            return rec.Id;
        }

        // UPSERT (şirket evrakları için: benzersiz = CompanyId + Year + DeclType('COMPANYDOC') + DocType)
        public async Task UpsertCompanyDocMetaAsync(FileMetaDto dto, CancellationToken ct)
        {
            var existing = await _db.Files
                .FirstOrDefaultAsync(x =>
                    x.CompanyId == dto.CompanyId &&
                    x.Year == dto.Year &&
                    x.DeclType == "COMPANYDOC" &&
                    x.DocType == dto.DocType, ct);

            if (existing is null)
            {
                var rec = new FileRecord
                {
                    CompanyId = dto.CompanyId,
                    Year = dto.Year,
                    Month = null,              // şirket evraklarında ay yok
                    DeclType = "COMPANYDOC",
                    DocType = dto.DocType,       // ENVANTERDEFTERI, VERGILEVHASI, ...
                    Key = dto.Key,
                    FileName = dto.FileName,
                    ContentType = dto.ContentType,
                    Length = dto.Length,
                    CreatedAtUtc = dto.CreatedAtUtc,
                      Description = dto.Description,
                    SequenceNo = dto.SequenceNo
                };
                _db.Files.Add(rec);
            }
            else
            {
                existing.Key = dto.Key;
                existing.FileName = dto.FileName;
                existing.ContentType = dto.ContentType;
                existing.Length = dto.Length;
                existing.CreatedAtUtc = dto.CreatedAtUtc;
                existing.Description = dto.Description;
                existing.SequenceNo = dto.SequenceNo;

                _db.Files.Update(existing);
            }

            await _db.SaveChangesAsync(ct);
        }
    }
}
