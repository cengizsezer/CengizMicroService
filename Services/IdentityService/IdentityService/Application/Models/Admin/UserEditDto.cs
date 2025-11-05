namespace IdentityService.Application.Models.Admin
{
    public class UserEditDto
    {
        public int Id { get; set; }
        public string UserName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Phone { get; set; }
       
    }
}
