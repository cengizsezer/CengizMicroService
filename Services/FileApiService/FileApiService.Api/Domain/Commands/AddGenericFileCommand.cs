using FileApiService.Api.Domain.Abstractions;

namespace FileApiService.Api.Domain.Commands
{
    /// <summary>
    /// Belirli bir modüle bağlı olmayan (companyId/year/declType gerektirmeyen) genel dosya yüklemesi.
    /// </summary>
    public sealed class AddGenericFileCommand
    {
        public IFileProxy File { get; init; } = default!;

        /// <summary>Depolama içindeki mantıksal klasör/önek (örn: "ticaret-sicil").</summary>
        public string Folder { get; init; } = "genel";
    }
}
