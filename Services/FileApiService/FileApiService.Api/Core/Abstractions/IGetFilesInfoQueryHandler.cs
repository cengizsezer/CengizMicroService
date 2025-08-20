using FileApiService.Api.Domain.Dtos;
using FileApiService.Api.Domain.Queries;
using SmallApiToolkit.Core.RequestHandlers;
using SmallApiToolkit.Core.Response;

namespace FileApiService.Api.Core.Abstractions
{
    public interface IGetFilesInfoQueryHandler : IHttpRequestHandler<IEnumerable<FileInfoDto>, GetFilesInfoQuery> { }
}
