using Microsoft.AspNetCore.Components.Authorization;
using WebApp.Application.Services.Interfaces;

namespace WebApp.Application.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly AuthenticationStateProvider _authStateProvider;

        public PermissionService(AuthenticationStateProvider authStateProvider)
        {
            _authStateProvider = authStateProvider;
        }

        public async Task<bool> HasPermissionAsync(string permission)
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (user?.Identity?.IsAuthenticated != true)
                return false;

            return user.Claims.Any(c => c.Type == "perm" && c.Value == permission);
        }
    }
}
