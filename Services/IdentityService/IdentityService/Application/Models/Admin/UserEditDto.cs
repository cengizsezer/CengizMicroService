namespace IdentityService.Application.Models.Admin
{
    public class UserEditDto
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Phone { get; set; }
        public string? Password { get; set; } // create sırasında isteğe bağlı
    }
}
