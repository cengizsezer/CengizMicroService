using FileApiService.Api.Domain.Commands;
using FileApiService.Api.Domain.Dtos;
using SmallApiToolkit.Core.Response;

namespace FileApiService.Api.Core.Abstractions
{
    public interface IAddGenericFileCommandHandler
    {
        Task<HttpDataResponse<GenericUploadResultDto>> HandleAsync(AddGenericFileCommand command, CancellationToken ct);
    }
}
