namespace CatalogService.Api.Infrastructure.Auth
{
    public interface IHttpCurrentUser
    {
        bool IsAuthenticated { get; }
        string UserId { get; }
        string? UserName { get; }
        string? Email { get; }
    }
}
