using Ardalis.GuardClauses;
using FileApiService.Api.Core.Abstractions;
using FileApiService.Api.Core.Resources;      // ValidationErrorMessages
using FileApiService.Api.Domain.Abstractions; // IFileProxy
using FileApiService.Api.Domain.Options;      // FilesOptions
using FluentResults;
using Microsoft.Extensions.Options;

namespace FileApiService.Api.Core.Validation
{
    internal sealed class FileByOptionsValidator : IFileByOptionsValidator
    {
        private readonly FilesOptions _opts;
        public FileByOptionsValidator(IOptions<FilesOptions> opts)
        {
            _opts = Guard.Against.Null(opts).Value;
        }

        public Result<bool> Validate(IFileProxy file)
        {
            // 1) boş / boyut
            if (file.Length == 0)
                return Result.Fail(string.Format(ValidationErrorMessages.FileIsEmpty, file.FileName));

            if (_opts.MaxFileLength > 0 && file.Length > _opts.MaxFileLength)
                return Result.Fail(string.Format(ValidationErrorMessages.MaximalFileSize, file.FileName));

            // 2) uzantı + MIME
            var ext = (System.IO.Path.GetExtension(file.FileName) ?? "").TrimStart('.').ToLowerInvariant();
            var ct = (file.ContentType ?? "").ToLowerInvariant();

            var allowed = _opts.AllowedFiles.FirstOrDefault(a =>
                a.Format.Equals(ext, StringComparison.OrdinalIgnoreCase) &&
                a.ContentType.Equals(ct, StringComparison.OrdinalIgnoreCase));

            if (allowed is null)
                return Result.Fail(string.Format(ValidationErrorMessages.UnsupportedFormat, file.FileName, file.ContentType));

            return Result.Ok(true);
        }

        // Şimdilik gereksiz:
        public Result<bool> Validate(string extension) =>
            Result.Ok(true); // veya hiç implement etme / exception fırlat

        public Result<bool> ValidateConversion(string sourceExtension, string destinationExtension) =>
            Result.Ok(true); // veya hiç implement etme / exception fırlat
    }
}
