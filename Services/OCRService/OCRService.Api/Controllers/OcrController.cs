using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OCRService.Api.Contracts.Dtos;
using OCRService.Api.Services;
using System.ComponentModel.DataAnnotations;

[ApiController]
[Route("api/[controller]")]
public class OcrController : ControllerBase
{
    private readonly OcrProcessor _ocr;
    private readonly OpenAiInterpreter _openai;
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/bmp", "application/pdf"
    };

    public OcrController(OcrProcessor ocr, OpenAiInterpreter openai)
    {
        _ocr = ocr;
        _openai = openai;
    }

    // Form model (Swagger için en sorunsuz yol)
    public sealed class AnalyzeImageRequest
    {
        [Required]
        public IFormFile File { get; set; } = default!;
    }

    [HttpPost("analyze")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(AnalyzeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AnalyzeImage([FromForm] AnalyzeImageRequest request, CancellationToken ct)
    {
        var file = request.File;
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Dosya zorunludur." });

        // Basit içerik tipi kontrolü (isteğe bağlı)
        if (!string.IsNullOrEmpty(file.ContentType) && !AllowedContentTypes.Contains(file.ContentType))
            return BadRequest(new { error = $"Desteklenmeyen içerik türü: {file.ContentType}" });

        string extractedText;
        try
        {
            await using var stream = file.OpenReadStream();
            extractedText = await _ocr.ExtractTextAsync(stream);
        }
        catch (Exception ex)
        {
            // Vision/IO hatası
            return BadRequest(new { error = "OCR sırasında hata oluştu.", detail = ex.Message });
        }

        OcrInterpretationDto? interpreted;
        try
        {
            interpreted = await _openai.InterpretAsync(extractedText, ct);
        }
        catch (Exception ex)
        {
            // Ağ/timeout vs. durumunda yine 200 dönüp boş DTO verebilirsin,
            // ama burada 400 dönmeyi tercih ettim. İstersen 200 + boş DTO’ya çevirebilirsin.
            return BadRequest(new { error = "OpenAI yorumlama sırasında hata oluştu.", detail = ex.Message });
        }

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

        // Debug log (console)
        Console.WriteLine("=== OCR Extracted Text ===");
        Console.WriteLine(extractedText);
        Console.WriteLine("=== Interpreted DTO ===");
        Console.WriteLine(JsonConvert.SerializeObject(interpreted, Formatting.Indented));

        var response = new AnalyzeResponseDto
        {
            ExtractedText = extractedText,
            Interpreted = interpreted
        };

        return Ok(response);
    }
}
