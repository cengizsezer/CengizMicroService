using FileApiService.Api.Core.Abstractions;
using FileApiService.Api.Core.Queries;
using FileApiService.Api.Core.Validation;

namespace FileApiService.Api.Core.Configuration
{
    public static class ContainerConfigurationExtension
    {
        public static IServiceCollection AddCore(this IServiceCollection services, IConfiguration _)
        {
            return services
                // Handlers
                .AddScoped<IAddFilesCommandHandler, AddFilesCommandHandler>()
                .AddScoped<IDownloadFileQueryHandler, DownloadFileQueryHandler>()
                .AddScoped<IGetFilesInfoQueryHandler, GetFilesInfoQueryHandler>()
                // Validators
                .AddScoped<IAddFilesCommandValidator, AddFilesCommandValidator>();
        }
    }
}
