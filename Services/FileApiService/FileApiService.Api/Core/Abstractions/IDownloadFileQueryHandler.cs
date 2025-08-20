using FileApiService.Api.Domain.Dtos;
using FileApiService.Api.Domain.Queries;
using SmallApiToolkit.Core.Response;

namespace FileApiService.Api.Core.Abstractions
{
    public interface IDownloadFileQueryHandler : IHttpRequestHandler<FileDto, DownloadFileQuery> { }
}
