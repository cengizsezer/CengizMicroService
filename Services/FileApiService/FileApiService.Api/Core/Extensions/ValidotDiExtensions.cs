using Validot;

namespace FileApiService.Api.Core.Extensions
{
    public static class ValidotDiExtensions
    {
        public static IServiceCollection AddValidotSingleton<THolder, TModel>(this IServiceCollection services)
            where THolder : ISpecificationHolder<TModel>, new()
        {
            return services.AddSingleton<IValidator<TModel>>(
                _ => Validator.Factory.Create(new THolder())
            );
        }
    }
}
