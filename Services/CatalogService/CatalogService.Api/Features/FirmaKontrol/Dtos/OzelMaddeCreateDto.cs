namespace CatalogService.Api.Features.FirmaKontrol.Dtos
{
    /// <summary>Firmaya özel yeni kontrol maddesi ekleme isteği.</summary>
    public class OzelMaddeCreateDto
    {
        public string Category { get; set; } = string.Empty;
        public string SoruMetni { get; set; } = string.Empty;
    }
}
