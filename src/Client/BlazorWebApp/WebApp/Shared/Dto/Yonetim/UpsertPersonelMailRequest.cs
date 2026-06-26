namespace WebApp.Shared.Dto.Yonetim
{
    public class UpsertPersonelMailRequest
    {
        public string UserId { get; set; } = "";
        public string? UserName { get; set; }
        public string? Email { get; set; }
    }
}
