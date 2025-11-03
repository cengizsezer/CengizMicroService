using System.Security.Claims;

namespace CatalogService.Api.Infrastructure.Auth
{
    public class HttpCurrentUser : IHttpCurrentUser
    {
        private readonly IHttpContextAccessor _http;
        public HttpCurrentUser(IHttpContextAccessor http) => _http = http;
        private ClaimsPrincipal? P => _http.HttpContext?.User;

        public bool IsAuthenticated => P?.Identity?.IsAuthenticated == true;

        public string UserId =>
            P?.FindFirst("sub")?.Value ??
            P?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
            throw new InvalidOperationException("sub claim not found");

        public string? UserName =>
            P?.FindFirst("name")?.Value ??
            P?.FindFirst("preferred_username")?.Value ??
            P?.FindFirst(ClaimTypes.Name)?.Value;

        public string? Email =>
            P?.FindFirst("email")?.Value ??
            P?.FindFirst(ClaimTypes.Email)?.Value;
    }
}
