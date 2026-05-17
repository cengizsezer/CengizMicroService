namespace CatalogService.Api.Features.KdvBeyanname.Dtos
{
    // ── Mizan upload sonucu ────────────────────────────────────────────────

    public class MizanUploadResultDto
    {
        public int OkunanSatir { get; set; }
        public int KaydedilenSatir { get; set; }
        public int AtlananSatir { get; set; }
        public List<string> Uyarilar { get; set; } = new();
        public List<string> Hatalar { get; set; } = new();
    }

    public class MizanSatirDto
    {
        public long Id { get; set; }
        public string HesapKodu { get; set; } = string.Empty;
        public string HesapAdi { get; set; } = string.Empty;
        public decimal BorcToplam { get; set; }
        public decimal AlacakToplam { get; set; }
        public decimal BorcKalan { get; set; }
        public decimal AlacakKalan { get; set; }
    }

    // ── Yevmiye upload sonucu ──────────────────────────────────────────────

    public class YevmiyeUploadResultDto
    {
        public int OkunanSatir { get; set; }
        public int KaydedilenSatir { get; set; }
        public int AtlananSatir { get; set; }
        public List<string> Uyarilar { get; set; } = new();
        public List<string> Hatalar { get; set; } = new();
    }

    public class YevmiyeSatirDto
    {
        public long Id { get; set; }
        public DateTime? Tarih { get; set; }
        public string HesapKodu { get; set; } = string.Empty;
        public string HesapAdi { get; set; } = string.Empty;
        public string? BelgeTipi { get; set; }
        public string? FaturaNo { get; set; }
        public decimal Borc { get; set; }
        public decimal Alacak { get; set; }
        public string? Aciklama { get; set; }
        public string? FisNo { get; set; }
    }
}
