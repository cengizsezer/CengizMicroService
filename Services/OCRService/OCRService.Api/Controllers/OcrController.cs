using Microsoft.AspNetCore.Mvc;
using OCRService.Api.Services;

namespace OCRService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OcrController : ControllerBase
{
    private readonly OcrProcessor _ocr;
    private readonly OpenAiInterpreter _openai;

    public OcrController(OcrProcessor ocr, OpenAiInterpreter openai)
    {
        _ocr = ocr;
        _openai = openai;
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> AnalyzeImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Dosya zorunludur");

        await using var stream = file.OpenReadStream();

        // ✅ Google Cloud Vision ile OCR metni çıkar
        var extractedText = await _ocr.ExtractTextAsync(stream);

        // ✅ OpenAI ile metni yorumla (firma adı, tutar, kdv vs.)
        var interpreted = await _openai.InterpretAsync(extractedText);

        // ✅ Sonucu dön
        return Ok(new
        {
            extractedText,
            interpreted
        });
    }
}
