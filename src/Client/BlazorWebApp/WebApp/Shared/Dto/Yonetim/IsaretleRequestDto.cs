namespace WebApp.Shared.Dto.Yonetim
{
    public class IsaretleRequestDto
    {
        public int HesapId { get; set; }
        public DateTime Tarih { get; set; }
        public bool IslendiMi { get; set; } = true;
    }
}
