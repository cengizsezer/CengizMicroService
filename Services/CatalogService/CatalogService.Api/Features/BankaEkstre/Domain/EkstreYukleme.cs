using CatalogService.Api.Infrastructure.Domain;

namespace CatalogService.Api.Features.BankaEkstre.Domain
{
    /// <summary>Tek bir ekstre dosyası yüklemesi; satırların üst kaydı.</summary>
    public class EkstreYukleme : TenantEntity
    {
        public int Id { get; set; }

        public int BankaHesabiId { get; set; }

        public string DosyaAdi { get; set; } = string.Empty;

        /// <summary>Yükleme anı (yerel duvar saati; sunucu saatiyle yazılır).</summary>
        public DateTime YuklemeTarihi { get; set; }

        public DateTime? DonemBaslangic { get; set; }
        public DateTime? DonemBitis { get; set; }

        public int SatirSayisi { get; set; }

        public YuklemeDurum Durum { get; set; } = YuklemeDurum.Isleniyor;

        /// <summary>Parser'ın ürettiği uyarılar (kolon adı bulunamadı vb.), satır başına birer satır.</summary>
        public string? Uyarilar { get; set; }

        /// <summary>
        /// Yüklenen dosyanın ham içeriği. Dışa aktarımın birinci parçası "orijinal ekstre
        /// yapısında, açıklama kolonu değiştirilmiş dosya" olduğu için kaynak saklanır;
        /// yeniden üretmek dosya yapısını kaybettirirdi.
        /// </summary>
        public byte[]? DosyaIcerik { get; set; }

        /// <summary>Kaynak dosyadaki açıklama kolonunun numarası (1 tabanlı); dışa aktarımda kullanılır.</summary>
        public int AciklamaKolonu { get; set; }

        public BankaHesabi? BankaHesabi { get; set; }
        public ICollection<EkstreSatiri> Satirlar { get; set; } = new List<EkstreSatiri>();
    }
}
