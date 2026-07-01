namespace WebApp.Shared.Dto.FirmaKontrol
{
    /// <summary>Firmaya özel kontrol maddesinin metnini güncelleme isteği.</summary>
    public class OzelMaddeUpdateDto
    {
        public string SoruMetni { get; set; } = string.Empty;
        public string? Category { get; set; }
    }
}
