using Consul;
using FileApiService.Api.Api.Files;
using FileApiService.Api.Core.Abstractions;
using FileApiService.Api.Core.Queries;
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

        [HttpPost("company-docs/upload")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<HttpDataResponse<bool>>> UploadCompanyDoc(
            [FromForm] IFormFile file,
            [FromForm] string companyId,
            [FromForm] string year,
            [FromForm] string docCategory,         // ENVANTERDEFTERI | VERGILEVHASI | TICARETSICILGAZETESI | IMZASIRKULERI ...
            [FromForm] string? description,        // opsiyonel
            [FromForm] int? sequenceNo,            // opsiyonel
            [FromServices] IAddCompanyDocCommandHandler handler,
            CancellationToken ct)
        {
            if (file is null || file.Length == 0)
                return BadRequest("Dosya zorunlu.");

            var cmd = new AddCompanyDocCommand
            {
                File = new FormFileProxy(file),
                CompanyId = companyId,
                Year = year,
                DocCategory = docCategory, // Normalizer içeride canon'layacak
                Description = description,
                SequenceNo = sequenceNo
            };

            var res = await handler.HandleAsync(cmd, ct);
            return Ok(res);
        }

        [HttpPost("uploads")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<HttpDataResponse<GenericUploadResultDto>>> UploadGeneric(
            [FromForm] IFormFile file,
            [FromForm] string? folder,
            [FromServices] IAddGenericFileCommandHandler handler,
            CancellationToken ct)
        {
            if (file is null || file.Length == 0)
                return BadRequest("Dosya zorunlu.");

            var cmd = new AddGenericFileCommand
            {
                File = new FormFileProxy(file),
                Folder = string.IsNullOrWhiteSpace(folder) ? "genel" : folder
            };

            var res = await handler.HandleAsync(cmd, ct);
            return Ok(res);
        }

        [HttpGet("company-docs/list")]
        public Task<HttpDataResponse<IEnumerable<CompanyDocInfoDto>>> ListCompanyDocs(
         [FromQuery] string companyId,
         [FromQuery] string? year,
         [FromQuery] string? docCategory,
         [FromQuery] string? description,// ENVANTERDEFTERI vs.

         [FromServices] IGetCompanyDocsInfoQueryHandler h,
         CancellationToken ct, [FromQuery] bool latestOnly = false)
         => h.HandleAsync(new GetCompanyDocsInfoQuery
         {
             CompanyId = companyId,
             Year = year,
             DocCategory = docCategory,
             Description= description,
             LatestOnly = latestOnly
         }, ct);

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


        [HttpDelete("delete")]
        public async Task<ActionResult<HttpDataResponse<bool>>> Delete(
    [FromQuery] int id,
    [FromServices] IDeleteFileCommandHandler handler,
    CancellationToken ct)
        {
            var companyId = User.FindFirst("company_id")?.Value; // varsa
            var res = await handler.HandleAsync(new DeleteFileCommand(id, companyId), ct);
            return Ok(res); // { data: true/false } — sizin pattern’le uyumlu
        }
    }
}
