using CatalogService.Api.Infrastructure.Domain;

namespace CatalogService.Api.Features.BankaEkstre.Domain
{
    /// <summary>
    /// ORKA'dan içe aktarılan düz hesap planı satırı (banka ekstresi eşleştirmesi için).
    /// Muhasebe modülündeki ağaç yapılı <c>HesapPlani</c>'dan kasıtlı olarak ayrıdır:
    /// buradaki kodlar ORKA formatında, boşluklu ve harf içerebilir ("120 D22").
    /// </summary>
    public class HesapPlaniKaydi : TenantEntity
    {
        public int Id { get; set; }

        /// <summary>Boşluklu ORKA kodu. Format değiştirilmez, ORKA tanımaz.</summary>
        public string Kod { get; set; } = string.Empty;

        public string Ad { get; set; } = string.Empty;

        /// <summary>Gürültü kelimeleri atılmış, Türkçe karakterleri sadeleştirilmiş ad.</summary>
        public string NormalizeAd { get; set; } = string.Empty;

        /// <summary>Kodun ilk segmenti, ör. "120", "329". Yön → ana grup daraltmasında kullanılır.</summary>
        public string AnaGrup { get; set; } = string.Empty;

        /// <summary>
        /// Ana gruptan sonraki ilk harf, ör. "120 D22" → "D". Cari kodları unvanın ilk
        /// harfiyle başladığı için arama uzayı bununla daraltılır.
        /// </summary>
        public string? BaslangicHarfi { get; set; }

        public bool Aktif { get; set; } = true;
    }
}
