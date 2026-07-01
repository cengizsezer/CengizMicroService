namespace CatalogService.Api.Features.FirmaKontrol.Dtos
{
    /// <summary>
    /// Bir kontrol maddesinin DB'de saklanan durumu (bir satır). Şablon maddesi ise
    /// <see cref="MaddeKey"/> dolu / <see cref="SoruMetni"/> null; özel madde ise tersi.
    /// Şablon metnini frontend kod tarafından ekler (metin DB'ye girmez).
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
