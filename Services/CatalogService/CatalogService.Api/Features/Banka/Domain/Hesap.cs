using CatalogService.Api.Features.Firmalar.Domain;

namespace CatalogService.Api.Features.Banka.Domain
{
    /// <summary>
    /// Banka Tanımı — bir Firma'ya bağlı banka / kredi kartı hesabı.
    /// Mukellef'e DEĞİL, Firma'ya bağlıdır.
    /// </summary>
    public class Hesap
    {
        public int Id { get; set; }

        // Firma'ya FK (Firmalar tablosu).
        public int FirmaId { get; set; }

        public HesapTip Tip { get; set; } = HesapTip.Banka;

        public string Ad { get; set; } = string.Empty;

        // Bu hesabın ne sıklıkla işlenmesi (kontrol edilmesi) beklendiği.
        public Siklik Siklik { get; set; } = Siklik.Gunluk;

        public bool AktifMi { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
