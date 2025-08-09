namespace OCRService.Api.Contracts.Dtos
{
    public class AnalyzeResponseDto
    {
        public string ExtractedText { get; set; } = string.Empty;
        public OcrInterpretationDto Interpreted { get; set; } = new();
    }
}
