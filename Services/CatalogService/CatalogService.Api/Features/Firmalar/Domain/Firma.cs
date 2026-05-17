namespace CatalogService.Api.Features.Firmalar.Domain
{
    public class Firma
    {
        public int Id { get; set; }

        public string VergiKimlikNo { get; set; } = string.Empty;
        public string Unvan { get; set; } = string.Empty;
        public string KisaAd { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefon { get; set; } = string.Empty;
        public string TicaretSicilNo { get; set; } = string.Empty;
        public string VergiDairesi { get; set; } = string.Empty;
        public bool Aktif { get; set; } = true;

        // BDP / KDV Beyannamesi için ek alanlar (opsiyonel)
        // NOT: YetkiliAdi / YetkiliSoyadi DB kolon isimleri değişmedi — sadece UI etiketleri
        // "Adı (Unvanın Devamı)" / "Soyadı (Unvanı)" oldu (BDP'de firma ünvanı 2 parçaya bölünüyor).
        public string? VergiDairesiKodu { get; set; }
        public string? YetkiliAdi { get; set; }
        public string? YetkiliSoyadi { get; set; }
        public string? TelefonAlanKodu { get; set; }

        // Bu firmanın KDV beyannamesini hangi SMMM/YMM düzenliyor — FK to Duzenleyenler.
        // ON DELETE SET NULL: düzenleyen silinirse firma kaydı korunur, FK temizlenir.
        public int? DuzenleyenId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
