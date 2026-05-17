namespace WebApp.Shared.Dto.Yonetim
{
    public class FirmaDto
    {
        public int Id { get; set; }
        public string VergiKimlikNo { get; set; } = string.Empty;
        public string Unvan { get; set; } = string.Empty;
        public string KisaAd { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefon { get; set; } = string.Empty;
        public string TicaretSicilNo { get; set; } = string.Empty;
        public string VergiDairesi { get; set; } = string.Empty;
        public bool Aktif { get; set; }

        // KDV Beyannamesi (BDP) için ek alanlar — opsiyonel.
        public string? VergiDairesiKodu { get; set; }
        public string? YetkiliAdi { get; set; }
        public string? YetkiliSoyadi { get; set; }
        public string? TelefonAlanKodu { get; set; }

        // KDV beyannamesini düzenleyen SMMM/YMM.
        public int? DuzenleyenId { get; set; }
        public string? DuzenleyenKisaltma { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
