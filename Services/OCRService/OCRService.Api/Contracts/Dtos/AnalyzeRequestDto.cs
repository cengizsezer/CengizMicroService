namespace OCRService.Api.Contracts.Dtos
{
    public sealed class AnalyzeRequestDto
    {
        public string Language { get; set; } = string.Empty;  // "tr", "en"...
        public bool UseLayout { get; set; }    // OCR motoru için ek ayar
    }
}
