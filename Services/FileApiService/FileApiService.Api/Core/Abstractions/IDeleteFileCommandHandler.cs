using FileApiService.Api.Domain.Commands;
using SmallApiToolkit.Core.Response;

namespace FileApiService.Api.Core.Abstractions
{
    public interface IDeleteFileCommandHandler
    {
        Task<HttpDataResponse<bool>> HandleAsync(DeleteFileCommand cmd, CancellationToken ct);
    }
}
