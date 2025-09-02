using FileApiService.Api.Core.Abstractions;
using FileApiService.Api.Domain.Commands;
using FileApiService.Api.Domain.Options;
using FluentResults;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using Validot.Settings;

namespace FileApiService.Api.Core.Validation
{
    internal sealed class AddCompanyDocCommandValidator : IAddCompanyDocCommandValidator
    {
        private readonly FilesOptions _opts;
        public AddCompanyDocCommandValidator(IOptions<FilesOptions> o) => _opts = o.Value;

        public Result<bool> Validate(AddCompanyDocCommand c)
        {
            // Temel alanlar
            if (c.File is null) return Result.Fail("Dosya zorunlu.");
            if (string.IsNullOrWhiteSpace(c.CompanyId)) return Result.Fail("CompanyId zorunlu.");
            if (string.IsNullOrWhiteSpace(c.Year)) return Result.Fail("Year zorunlu.");
            if (string.IsNullOrWhiteSpace(c.DocCategory)) return Result.Fail("DocCategory zorunlu.");

            // Boyut kontrolü
            if (c.File.Length <= 0) return Result.Fail("Dosya boş.");
            if (_opts.MaxFileLength > 0 && c.File.Length > _opts.MaxFileLength)
                return Result.Fail($"Boyut sınırı aşıldı. Max: {_opts.MaxFileLength} byte.");

            // Tür kontrolü: iki seçenek
            // A) Sadece PDF kabul et (tercihim buysa aşağıdaki satırı aç)
            // if (!string.Equals(c.File.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            //     return Result.Fail("Sadece PDF yükleyebilirsiniz.");

            // B) FilesOptions.AllowedFiles listesini kullan (beyanname validator’ındaki gibi)
            var ext = Path.GetExtension(c.File.FileName).TrimStart('.').ToLowerInvariant();
            var ct = (c.File.ContentType ?? "").ToLowerInvariant();

            var allowed = _opts.AllowedFiles.FirstOrDefault(a =>
                a.Format.Equals(ext, StringComparison.OrdinalIgnoreCase) &&
                a.ContentType.Equals(ct, StringComparison.OrdinalIgnoreCase));

            if (allowed is null)
                return Result.Fail($"{c.File.FileName}: izin verilmeyen tür ({ext}/{ct}).");

            return Result.Ok(true);
        }
    }
}
