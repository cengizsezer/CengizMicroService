namespace WebApp.Shared.Dto.FirmaKontrol
{
    /// <summary>
    /// Bir kontrol maddesinin DB'de saklı durumu (CatalogService'ten gelir).
    /// Şablon maddesi: MaddeKey dolu / SoruMetni null (metin frontend kodunda).
    /// Özel madde: IsCustom=true, SoruMetni dolu, MaddeKey null.
    /// </summary>
    public class FirmaKontrolMaddeDto
    {
        public long Id { get; set; }
        public string? MaddeKey { get; set; }
        public bool IsCustom { get; set; }
        public string Category { get; set; } = string.Empty;
        public string? SoruMetni { get; set; }
        public bool IsChecked { get; set; }
        public int Status { get; set; }
        public string? Not { get; set; }
        public int SiraNo { get; set; }
    }
}
