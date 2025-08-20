using FileApiService.Api.Domain.Abstractions;

namespace FileApiService.Api.Domain.Commands
{
    public sealed record AddFilesCommand(IEnumerable<IFileProxy> Files);
}
