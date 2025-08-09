using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OCRService.Api.Contracts;
using OCRService.Api.Contracts.Dtos;
using OCRService.Api.Services;
using System.Globalization;

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
    public async Task<ActionResult<AnalyzeResponseDto>> AnalyzeImage([FromForm] IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "Dosya zorunludur" });

        await using var stream = file.OpenReadStream();
        var extractedText = await _ocr.ExtractTextAsync(stream);
        var interpreted = await _openai.InterpretAsync(extractedText, ct); // <-- OcrInterpretationDto?
        Console.WriteLine("OpenAI raw JSON: " + interpreted);
        if (interpreted == null)
        {
            interpreted = new OcrInterpretationDto
            {
                CompanyName = "",
                InvoiceNumber = "",
                BaseAmount = 0,
                LsVatDetails = new Dictionary<string, VatDetailDto>()
            };
        }


        Console.WriteLine("=== OCR Extracted Text ===");
        Console.WriteLine(extractedText);
        Console.WriteLine("=== Interpreted DTO ===");
        Console.WriteLine(JsonConvert.SerializeObject(interpreted, Formatting.Indented));
        return Ok(new AnalyzeResponseDto
        {
            ExtractedText = extractedText,
            Interpreted = interpreted // null olabilir; UI'da kontrol et
        });
    }

}

