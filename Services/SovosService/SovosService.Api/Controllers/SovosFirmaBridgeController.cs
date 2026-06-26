using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sovos.InvoiceWorker.Core.Entities;
using Sovos.InvoiceWorker.Core.Enums;
using Sovos.InvoiceWorker.Core.Interfaces;
using SovosService.Api.Application.Models;
using SovosService.Api.Persistence;

namespace SovosService.Api.Controllers;

/// <summary>
/// Yönetim → Firmalarım sayfası için KÖPRÜ controller'ı.
/// SovosCompanies tablosunu firma bazında (FirmaId) okur/yazar; tüketiciler
/// (InvoiceOrchestrator, IncomingInvoiceOrchestrator, scraper'lar) aynı tabloyu
/// okumaya HİÇ değişmeden devam eder. Mevcut SovosAdminController (Fatura Kontrol)
/// da olduğu gibi kalır — burası sadece firma-anahtarlı ince bir cephe.
///
/// GÜVENLİK: SovosAdminController ile aynı rol seti; şifre asla geri dönmez
/// (sadece HasPassword). Şifre değişimi var olan companies/{id}/password endpoint'i
/// üzerinden yapılır (GET burada CompanyId döner).
/// </summary>
[Route("api/faturakontrol/firmalar")]
[ApiController]
[Authorize(Roles = "Admin,pkf")]
public class SovosFirmaBridgeController : ControllerBase
{
    private readonly SovosServiceDbContext _db;
    private readonly ICredentialProtector _protector;
    private readonly ILogger<SovosFirmaBridgeController> _logger;

    public SovosFirmaBridgeController(
        SovosServiceDbContext db,
        ICredentialProtector protector,
        ILogger<SovosFirmaBridgeController> logger)
    {
        _db = db;
        _protector = protector;
        _logger = logger;
    }

    // GET api/faturakontrol/firmalar/{firmaId}/sovos
    // Hesap yoksa 200 + HasAccount=false döner (404 değil) — UI "tanımla" formunu açar.
    [HttpGet("{firmaId:int}/sovos")]
    public async Task<ActionResult<SovosFirmaCredentialDto>> GetCredential(int firmaId, CancellationToken ct)
    {
        var c = await _db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.FirmaId == firmaId, ct);

        if (c is null)
        {
            return Ok(new SovosFirmaCredentialDto { FirmaId = firmaId, HasAccount = false });
        }

        return Ok(new SovosFirmaCredentialDto
        {
            FirmaId = firmaId,
            HasAccount = true,
            CompanyId = c.Id,
            CompanyCode = c.CompanyCode,
            Username = c.Username,
            HasPassword = !string.IsNullOrEmpty(c.EncryptedPassword),
            IsActive = c.IsActive
        });
    }

    // PUT api/faturakontrol/firmalar/{firmaId}/sovos
    // Upsert: kayıt yoksa oluşturur (Password zorunlu), varsa kullanıcı adı/kod/aktiflik
    // günceller. Password yalnızca doluysa değiştirilir (şifre değişimi tercihen ayrı akış).
    [HttpPut("{firmaId:int}/sovos")]
    public async Task<ActionResult<SovosFirmaCredentialDto>> Upsert(
        int firmaId, [FromBody] SovosFirmaCredentialUpsertDto dto, CancellationToken ct)
    {
        var companyCode = dto.CompanyCode?.Trim() ?? "";
        var username = dto.Username?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(companyCode) || string.IsNullOrWhiteSpace(username))
            return BadRequest(new { message = "Şirket kısa kodu ve kullanıcı adı zorunludur." });

        var c = await _db.Companies.FirstOrDefaultAsync(x => x.FirmaId == firmaId, ct);

        if (c is null)
        {
            // Yeni kayıt — şifre zorunlu.
            if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 4)
                return BadRequest(new { message = "Yeni entegratör hesabı için en az 4 karakterlik şifre zorunludur." });

            c = new Company
            {
                FirmaId = firmaId,
                // Name DB'de zorunlu; firma adı yoksa kısa kodu kullan.
                Name = string.IsNullOrWhiteSpace(dto.FirmaName) ? companyCode : dto.FirmaName.Trim(),
                CompanyCode = companyCode,
                Username = username,
                EncryptedPassword = _protector.Encrypt(dto.Password),
                NotificationEmails = "",
                IsActive = dto.IsActive,
                // Köprüden eklenen hesap otomatik zamanlanmış taramaya girmez; istenirse
                // Fatura Kontrol'den Daily/Hourly yapılır. KDV/Gelen on-demand zaten çalışır.
                ScheduleMode = ScheduleMode.Manual,
                ScheduleHour = null
            };
            _db.Companies.Add(c);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Köprü: firma {FirmaId} için yeni entegratör hesabı oluşturuldu (CompanyId={Id})",
                firmaId, c.Id);
        }
        else
        {
            c.CompanyCode = companyCode;
            c.Username = username;
            c.IsActive = dto.IsActive;
            if (!string.IsNullOrWhiteSpace(dto.FirmaName))
                c.Name = dto.FirmaName.Trim();

            // Şifre yalnızca açıkça gönderildiyse değiştirilir.
            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                if (dto.Password.Length < 4)
                    return BadRequest(new { message = "Şifre en az 4 karakter olmalıdır." });
                c.EncryptedPassword = _protector.Encrypt(dto.Password);
            }

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Köprü: firma {FirmaId} entegratör hesabı güncellendi (CompanyId={Id})",
                firmaId, c.Id);
        }

        return Ok(new SovosFirmaCredentialDto
        {
            FirmaId = firmaId,
            HasAccount = true,
            CompanyId = c.Id,
            CompanyCode = c.CompanyCode,
            Username = c.Username,
            HasPassword = !string.IsNullOrEmpty(c.EncryptedPassword),
            IsActive = c.IsActive
        });
    }
}
