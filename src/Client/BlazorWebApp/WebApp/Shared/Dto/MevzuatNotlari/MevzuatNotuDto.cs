namespace WebApp.Shared.Dto.MevzuatNotlari
{
    public class MevzuatNotuDto
    {
        public int Id { get; set; }
        public string Kategori { get; set; } = string.Empty;
        public string? MaddeNo { get; set; }
        public string Baslik { get; set; } = string.Empty;
        public string? Ozet { get; set; }
        public string? Icerik { get; set; }
        public string? Etiketler { get; set; }
        public string? Kaynak { get; set; }
        public DateTime OlusturmaTarihi { get; set; }
        public DateTime? GuncellemeTarihi { get; set; }
    }
}
