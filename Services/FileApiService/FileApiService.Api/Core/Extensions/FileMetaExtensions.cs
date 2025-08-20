using FileApiService.Api.Domain.Abstractions;
using FileApiService.Api.Domain.Dtos;

namespace FileApiService.Api.Core.Extensions
{
    internal static class FileMetaExtensions
    {
        public static FileMetaDto ToMeta(this IFileProxy file, string key, IDictionary<string, string> meta)
            => new FileMetaDto
            {
                CompanyId = meta["CompanyId"],
                Year = meta["Year"],
                Month = meta["Month"],
                DeclType = meta["DeclType"],
                DocType = meta["DocType"],
                Key = key,
                FileName = Path.GetFileName(file.FileName),
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/pdf" : file.ContentType,
                Length = file.Length
            };
    }
}
