namespace CatalogService.Api.Features.MevzuatNotlari.Dtos
{
    /// <summary>
    /// Mevzuat notu okuma/yazma DTO'su.
    /// Yazma (Create/Update) sırasında OlusturmaTarihi/GuncellemeTarihi sunucuda set edilir;
    /// istemciden gelen değerler yok sayılır.
    /// </summary>
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
