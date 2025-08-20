using FileApiService.Api.Domain.Commands;
using SmallApiToolkit.Core.Response;

namespace FileApiService.Api.Core.Abstractions
{
    public interface IAddFilesCommandHandler
        : IHttpRequestHandler<bool, AddFilesCommand>
    { }
}
