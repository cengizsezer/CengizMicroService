namespace WebApp.Shared.Dto
{
    public class AnalyzeResponseDto
    {
        public string ExtractedText { get; set; } = string.Empty;
        public OcrInterpretationDto Interpreted { get; set; } = new();
    }
}
