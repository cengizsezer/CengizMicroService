namespace CatalogService.Api.Features.Banka.Domain
{
    /// <summary>
    /// Banka Takibi — bir hesabın belirli bir GÜN işlenip işlenmediğini tutar.
    /// Bakiye/para hareketi TUTMAZ; sadece "o gün işlendi mi" bilgisidir.
    /// Bir hesap bir gün işaretlenince o tarihe (HesapId+Tarih) tek kayıt yazılır.
    /// </summary>
    public class IslemKaydi
    {
        public int Id { get; set; }

        // Hesap'a FK.
        public int HesapId { get; set; }

        // Gün (saat bileşeni anlamsız; date olarak kullanılır).
        public DateTime Tarih { get; set; }

        public bool IslendiMi { get; set; }

        // İşlemi yapan kullanıcının adı (claim'den doldurulur, boş olabilir).
        public string? IsleyenKullanici { get; set; }

        // İşaretlemenin yapıldığı an.
        public DateTime? IslemZamani { get; set; }
    }
}
