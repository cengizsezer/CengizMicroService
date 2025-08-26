using FileApiService.Api.Api.Files;
using FileApiService.Api.Core.Abstractions;
using FileApiService.Api.Domain.Commands;
using FileApiService.Api.Domain.Dtos;
using FileApiService.Api.Domain.Queries;
using Microsoft.AspNetCore.Mvc;
using SmallApiToolkit.Core.Response;

namespace FileApiService.Api.Api.Controllers
{
    [ApiController]
    [Route("api/file/v1")]
    public class FileController : ControllerBase
    {
        [HttpPost("upload")]
        public async Task<ActionResult<HttpDataResponse<bool>>> Upload(
            IFormFile file,
            [FromForm] string companyId,
            [FromForm] string year,
            [FromForm] string month,
            [FromForm] string declType,
            [FromForm] string docType,
            [FromServices] IAddFilesCommandHandler handler,
            CancellationToken ct)
        {
            var proxy = new FormFileProxy(file);
            proxy.Metadata["CompanyId"] = companyId;
            proxy.Metadata["Year"] = year;
            proxy.Metadata["Month"] = month;
            proxy.Metadata["DeclType"] = declType;
            proxy.Metadata["DocType"] = docType;

            var res = await handler.HandleAsync(new AddFilesCommand(new[] { proxy }), ct);
            return Ok(res);
        }

        [HttpGet("download")]
        public Task<HttpDataResponse<FileDto>> Download([FromQuery] int id, [FromServices] IDownloadFileQueryHandler h, CancellationToken ct)
            => h.HandleAsync(new DownloadFileQuery(id), ct);

        [HttpGet("files-info")]
        public Task<HttpDataResponse<IEnumerable<FileInfoDto>>> List(
            [FromQuery] string? companyId,
            [FromQuery] string? year,
            [FromQuery] string? month,
            [FromQuery] string? declType,
            [FromServices] IGetFilesInfoQueryHandler h,
            CancellationToken ct)
            => h.HandleAsync(new GetFilesInfoQuery { CompanyId = companyId, Year = year, Month = month, DeclType = declType }, ct);
    }
}
