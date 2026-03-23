namespace WebApp.Application.Services.Interfaces
{
    public interface IPermissionService
    {
        Task<bool> HasPermissionAsync(string permission);
    }
}
