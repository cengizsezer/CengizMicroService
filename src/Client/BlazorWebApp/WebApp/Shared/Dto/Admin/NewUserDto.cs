namespace WebApp.Shared.Dto.Admin
{
    public class NewUserDto
    {
        public int Id { get; set; }
        public string UserName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Phone { get; set; }
        public string? Password { get; set; } // create sırasında isteğe bağlı
    }
}
