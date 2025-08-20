using FileApiService.Api.Api.Files;
using FileApiService.Api.Core.Abstractions;
using FileApiService.Api.Domain.Commands;
using FileApiService.Api.Domain.Dtos;
using FileApiService.Api.Domain.Queries;
using Microsoft.AspNetCore.Mvc;
using SmallApiToolkit.Core.Extensions;

namespace FileApiService.Api.Api.EndpointBuilders
{
    public static class FileEndpointsBuilder
    {
        public static IEndpointRouteBuilder BuildFileEndpoints(this IEndpointRouteBuilder endpoints)
        {
            return endpoints
                .MapGroup("file")
              /*  .MapVersionGroup(1) */               // SmallApiToolkit
                .BuildUpload()
                .BuildDownload()
                .BuildPresign();
        }

        // -------- Upload (multipart) --------
        private static IEndpointRouteBuilder BuildUpload(this IEndpointRouteBuilder g)
        {
            g.MapPost("upload",
                async (
                    IFormFile file,
                    [FromForm] string companyId,
                    [FromForm] string year,
                    [FromForm] string month,
                    [FromForm] string declType,
                    [FromForm] string docType,
                    [FromServices] IAddFilesCommandHandler handler,
                    CancellationToken ct) =>
                {
                    var proxy = new FormFileProxy(file);
                    proxy.Metadata["CompanyId"] = companyId;
                    proxy.Metadata["Year"] = year;
                    proxy.Metadata["Month"] = month;
                    proxy.Metadata["DeclType"] = declType;
                    proxy.Metadata["DocType"] = docType;

                    return await handler.HandleAsync(new AddFilesCommand(new[] { proxy }), ct);
                })
                .DisableAntiforgery()
                //.ProducesDataResponse<bool>()       // SmallApiToolkit
                .WithName("AddFiles")
                .WithTags("Post");

            return g;
        }

        // -------- Download (Presigned GET url döner) --------
        private static IEndpointRouteBuilder BuildDownload(this IEndpointRouteBuilder g)
        {
            g.MapGet("download",
                async ([FromQuery] int id,
                       [FromServices] IDownloadFileQueryHandler handler,
                       CancellationToken ct) =>
                    await handler.HandleAsync(new DownloadFileQuery(id), ct))
                .DisableAntiforgery()
                //.ProducesDataResponse<FileDto>()
                .WithName("DownloadFile")
                .WithTags("Get");

            return g;
        }

        // -------- İsteğe bağlı: doğrudan key ile presigned url --------
        private static IEndpointRouteBuilder BuildPresign(this IEndpointRouteBuilder g)
        {
            g.MapGet("presign",
                async ([FromQuery] string key,
                       [FromServices] IPresignUrlService svc,
                       CancellationToken ct) =>
                {
                    var url = await svc.GetReadUrlAsync(key, TimeSpan.FromMinutes(30), ct);
                    return HttpDataResponses.AsOK(new { url });
                })
                .DisableAntiforgery()
                //.ProducesDataResponse<object>()
                .WithName("PresignGet")
                .WithTags("Get");

            return g;
        }
    }

}
