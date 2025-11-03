namespace IdentityService.Application.Models.Register
{
    public record RegisterResult(bool Success, int UserId, string Email, string? FullName, string? ErrorMessage);
}
