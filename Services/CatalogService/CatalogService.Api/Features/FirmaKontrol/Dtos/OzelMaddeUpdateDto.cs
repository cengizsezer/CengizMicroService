namespace CatalogService.Api.Features.FirmaKontrol.Dtos
{
    /// <summary>
    /// Firmaya özel bir kontrol maddesinin metnini (ve isteğe bağlı kategorisini)
    /// güncelleme isteği. Yalnızca IsCustom=true maddeler için geçerlidir.
    /// </summary>
    public class OzelMaddeUpdateDto
    {
        public string SoruMetni { get; set; } = string.Empty;

        /// <summary>Verilirse kategori de güncellenir; null ise dokunulmaz.</summary>
        public string? Category { get; set; }
    }
}
