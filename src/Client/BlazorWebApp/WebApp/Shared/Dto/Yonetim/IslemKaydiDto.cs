namespace WebApp.Shared.Dto.Yonetim
{
    public class IslemKaydiDto
    {
        public int Id { get; set; }
        public int HesapId { get; set; }
        public DateTime Tarih { get; set; }
        public bool IslendiMi { get; set; }
        public string? IsleyenKullanici { get; set; }
        public DateTime? IslemZamani { get; set; }
    }
}
