using Ardalis.GuardClauses;
using FileApiService.Api.Core.Abstractions;
using FileApiService.Api.Domain.Commands;
using FileApiService.Api.Domain.Options;
using FluentResults;
using Microsoft.Extensions.Options;
using Validot;

namespace FileApiService.Api.Core.Validation
{
    internal sealed class AddFilesCommandValidator : IAddFilesCommandValidator
    {
        private readonly IValidator<AddFilesCommand> _validot;
        private readonly FilesOptions _opts;
        public AddFilesCommandValidator(IValidator<AddFilesCommand> v, IOptions<FilesOptions> o) { _validot = v; _opts = o.Value; }

        public Result<bool> Validate(AddFilesCommand cmd)
        {
            var vr = _validot.Validate(cmd);
            if (vr.AnyErrors) return Result.Fail(vr.ToString());

            foreach (var f in cmd.Files)
            {
                if (f.Length <= 0) return Result.Fail($"{f.FileName}: boş dosya.");
                if (_opts.MaxFileLength > 0 && f.Length > _opts.MaxFileLength) return Result.Fail($"{f.FileName}: boyut sınırı aşıldı.");

                var ext = Path.GetExtension(f.FileName).TrimStart('.').ToLowerInvariant();
                var ct = (f.ContentType ?? "").ToLowerInvariant();
                var allowed = _opts.AllowedFiles.FirstOrDefault(a =>
                    a.Format.Equals(ext, StringComparison.OrdinalIgnoreCase) &&
                    a.ContentType.Equals(ct, StringComparison.OrdinalIgnoreCase));
                if (allowed is null) return Result.Fail($"{f.FileName}: izin verilmeyen tür ({ext}/{ct}).");
            }
            return Result.Ok(true);
        }
    }
}
