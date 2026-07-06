using CatalogService.Api.Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sovos.IncomingInvoiceWorker.Services;
using Sovos.InvoiceWorker.Core.Exceptions;

namespace Sovos.IncomingInvoiceWorker.Controllers;

[ApiController]
[Route("api/kdv-beyanname")]
public class IncomingInvoiceController : ControllerBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IInvoicePdfService _pdfService;
    private readonly ILogger<IncomingInvoiceController> _logger;

    public IncomingInvoiceController(
        IServiceScopeFactory scopeFactory,
        IInvoicePdfService pdfService,
        ILogger<IncomingInvoiceController> logger)
    {
        _scopeFactory = scopeFactory;
        _pdfService = pdfService;
        _logger = logger;
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new
    {
        status = "ok",
        service = "Sovos.IncomingInvoiceWorker"
    });

    /// <summary>
    /// Belirli bir firma için tarih aralığında DP'den gelen faturaları tarar.
    /// Fire-and-forget; KdvBeyannameTarama kaydı üzerinden ilerleme izlenebilir.
    /// </summary>
    [HttpPost("{firmaId:int}/tara")]
    public async Task<IActionResult> Tara(
        int firmaId,
        [FromBody] TaramaRequest req,
        CancellationToken ct)
    {
        if (req is null)
            return BadRequest(new { message = "İstek gövdesi boş olamaz." });

        if (req.BaslangicTarihi > req.BitisTarihi)
            return BadRequest(new { message = "Başlangıç tarihi bitiş tarihinden büyük olamaz." });

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogContext>();
            var exists = await db.Firmalar.AnyAsync(f => f.Id == firmaId, ct);
            if (!exists)
                return NotFound(new { firmaId, message = "Firma bulunamadı." });
        }

        _ = Task.Run(async () =>
        {
            using var bgScope = _scopeFactory.CreateScope();
            var orchestrator = bgScope.ServiceProvider
                .GetRequiredService<IIncomingInvoiceOrchestrator>();
            try
            {
                _logger.LogInformation(
                    "Tara başladı (FirmaId={Id}, {From:yyyy-MM-dd} - {To:yyyy-MM-dd})",
                    firmaId, req.BaslangicTarihi, req.BitisTarihi);

                await orchestrator.RunForFirmaAsync(
                    firmaId, req.BaslangicTarihi, req.BitisTarihi, CancellationToken.None);

                _logger.LogInformation("Tara bitti (FirmaId={Id})", firmaId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tara hatası (FirmaId={Id})", firmaId);
            }
        });

        return Accepted(new
        {
            firmaId,
            status = "queued",
            mesaj = "Tarama başlatıldı. Sonucu /api/kdv-beyanname/{firmaId}/tarama-durumu ile sorgulayabilirsiniz."
        });
    }

    /// <summary>
    /// Tek bir faturanın PDF'ini DP portalından canlı çeker (ya da daha önce
    /// çekildiyse cache'ten döner), FileApiService'e kaydeder ve FileId döndürür.
    /// SENKRON: scrape 10-25 sn sürebilir; çağıran timeout'u buna göre ayarlamalı.
    /// </summary>
    [HttpPost("{firmaId:int}/fatura-pdf")]
    public async Task<IActionResult> FaturaPdf(
        int firmaId,
        [FromQuery] string faturaNo,
        [FromQuery] int yil,
        [FromQuery] int ay,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(faturaNo))
            return BadRequest(new { message = "faturaNo zorunlu." });
        if (yil < 2000 || yil > 2100 || ay < 1 || ay > 12)
            return BadRequest(new { message = "Geçersiz yil/ay." });

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogContext>();
            var exists = await db.Firmalar.AnyAsync(f => f.Id == firmaId, ct);
            if (!exists)
                return NotFound(new { firmaId, message = "Firma bulunamadı." });
        }

        try
        {
            var result = await _pdfService.GetOrFetchAsync(firmaId, faturaNo, yil, ay, ct);
            return Ok(new
            {
                faturaNo = result.FaturaNo,
                fileId = result.FileId,
                fileName = result.FileName,
                cached = result.Cached
            });
        }
        catch (SovosCaptchaActiveException)
        {
            return StatusCode(StatusCodes.Status409Conflict,
                new { faturaNo, message = "DP portalında captcha aktif; manuel müdahale gerekli." });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Fatura PDF çekme hatası (FirmaId={Id}, FaturaNo={No})", firmaId, faturaNo);
            return StatusCode(StatusCodes.Status502BadGateway, new { faturaNo, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatura PDF beklenmeyen hata (FirmaId={Id}, FaturaNo={No})", firmaId, faturaNo);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { faturaNo, message = "Beklenmeyen hata: " + ex.Message });
        }
    }
}

public class TaramaRequest
{
    public DateTime BaslangicTarihi { get; set; }
    public DateTime BitisTarihi { get; set; }
}
