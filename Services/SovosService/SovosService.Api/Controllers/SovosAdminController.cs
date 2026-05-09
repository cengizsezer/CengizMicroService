using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sovos.InvoiceWorker.Core.Entities;
using Sovos.InvoiceWorker.Core.Enums;
using Sovos.InvoiceWorker.Core.Interfaces;
using SovosService.Api.Application.Models;
using SovosService.Api.Persistence;

namespace SovosService.Api.Controllers;

[Route("api/faturakontrol")]
[ApiController]
[Authorize(Roles = "Admin,pkf")]
public class SovosAdminController : ControllerBase
{
    private readonly SovosServiceDbContext _db;
    private readonly ICredentialProtector _protector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SovosAdminController> _logger;

    public SovosAdminController(
        SovosServiceDbContext db,
        ICredentialProtector protector,
        IHttpClientFactory httpClientFactory,
        ILogger<SovosAdminController> logger)
    {
        _db = db;
        _protector = protector;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // GET api/sovos/admin/companies?p=0&ps=50&q=...
    [HttpGet("companies")]
    public async Task<ActionResult<PageDto<SovosCompanyListItemDto>>> GetCompanies(
        [FromQuery] int p = 0, [FromQuery] int ps = 50, [FromQuery] string? q = null)
    {
        _logger.LogInformation("GetCompanies: p={P}, ps={Ps}, q={Q}", p, ps, q);

        var query = _db.Companies.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(c =>
                c.Name.Contains(q) ||
                c.CompanyCode.Contains(q) ||
                c.Username.Contains(q) ||
                c.NotificationEmails.Contains(q));
        }

        var count = await query.CountAsync();

        var items = await query
            .OrderBy(c => c.Name)
            .Skip(p * ps).Take(ps)
            .Select(c => new SovosCompanyListItemDto
            {
                Id = c.Id,
                Name = c.Name,
                CompanyCode = c.CompanyCode,
                Username = c.Username,
                NotificationEmails = c.NotificationEmails,
                IsActive = c.IsActive,
                HasPassword = !string.IsNullOrEmpty(c.EncryptedPassword),
                LastSuccessfulRunAt = c.LastSuccessfulRunAt,
                LastFailedRunAt = c.LastFailedRunAt,
                LastErrorMessage = c.LastErrorMessage,
                InvoiceCountLastRun = null,
                ScheduleMode = c.ScheduleMode,
                ScheduleHour = c.ScheduleHour
            })
            .AsNoTracking()
            .ToListAsync();

        return Ok(new PageDto<SovosCompanyListItemDto>
        {
            PageIndex = p,
            PageSize = ps,
            Count = count,
            Data = items
        });
    }

    // GET api/sovos/admin/companies/{id}
    [HttpGet("companies/{id:int}")]
    public async Task<ActionResult<SovosCompanyDetailDto>> GetCompany(int id)
    {
        _logger.LogInformation("GetCompany: id={Id}", id);

        var c = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        return Ok(new SovosCompanyDetailDto
        {
            Id = c.Id,
            Name = c.Name,
            CompanyCode = c.CompanyCode,
            Username = c.Username,
            NotificationEmails = c.NotificationEmails,
            IsActive = c.IsActive,
            HasPassword = !string.IsNullOrEmpty(c.EncryptedPassword),
            LastSuccessfulRunAt = c.LastSuccessfulRunAt,
            LastFailedRunAt = c.LastFailedRunAt,
            LastErrorMessage = c.LastErrorMessage,
            InvoiceCountLastRun = null,
            ScheduleMode = c.ScheduleMode,
            ScheduleHour = c.ScheduleHour
        });
    }

    // POST api/sovos/admin/companies
    [HttpPost("companies")]
    public async Task<IActionResult> CreateCompany([FromBody] NewSovosCompanyDto dto)
    {
        _logger.LogInformation("CreateCompany: Name={Name}", dto.Name);

        if (!TryNormalizeSchedule(dto.ScheduleMode, dto.ScheduleHour, out var normalizedHour, out var scheduleErr))
            return BadRequest(scheduleErr);

        var entity = new Company
        {
            Name = dto.Name,
            CompanyCode = dto.CompanyCode,
            Username = dto.Username,
            EncryptedPassword = _protector.Encrypt(dto.Password),
            NotificationEmails = dto.NotificationEmails,
            IsActive = dto.IsActive,
            ScheduleMode = dto.ScheduleMode,
            ScheduleHour = normalizedHour
        };

        _db.Companies.Add(entity);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Yeni firma oluşturuldu: {Name} (Id={Id})", entity.Name, entity.Id);

        return CreatedAtAction(nameof(GetCompany), new { id = entity.Id }, new { id = entity.Id });
    }

    // PUT api/sovos/admin/companies/{id}
    [HttpPut("companies/{id:int}")]
    public async Task<IActionResult> UpdateCompany(int id, [FromBody] SovosCompanyEditDto dto)
    {
        _logger.LogInformation(
            "UpdateCompany: id={Id} mode={Mode} hour={Hour}",
            id, dto.ScheduleMode, dto.ScheduleHour);

        if (!TryNormalizeSchedule(dto.ScheduleMode, dto.ScheduleHour, out var normalizedHour, out var scheduleErr))
            return BadRequest(scheduleErr);

        var c = await _db.Companies.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        c.Name = dto.Name;
        c.CompanyCode = dto.CompanyCode;
        c.Username = dto.Username;
        c.NotificationEmails = dto.NotificationEmails;
        c.IsActive = dto.IsActive;
        c.ScheduleMode = dto.ScheduleMode;
        c.ScheduleHour = normalizedHour;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Firma güncellendi: {Name} (Id={Id}) mode={Mode} hour={Hour}",
            c.Name, c.Id, c.ScheduleMode, c.ScheduleHour);
        return NoContent();
    }

    // Daily ⇒ ScheduleHour 0-23 zorunlu. Hourly/Manual ⇒ ScheduleHour null'a normalize.
    private static bool TryNormalizeSchedule(
        ScheduleMode mode, int? hourIn, out int? hourOut, out string error)
    {
        if (mode == ScheduleMode.Daily)
        {
            if (hourIn is null || hourIn < 0 || hourIn > 23)
            {
                hourOut = null;
                error = "Günlük modda tarama saati 0-23 aralığında zorunludur.";
                return false;
            }
            hourOut = hourIn;
        }
        else
        {
            hourOut = null;
        }
        error = "";
        return true;
    }

    // POST api/sovos/admin/companies/{id}/password
    [HttpPost("companies/{id:int}/password")]
    public async Task<IActionResult> ChangePassword(int id, [FromBody] SovosCompanyPasswordDto dto)
    {
        _logger.LogInformation("ChangePassword: id={Id}", id);

        var c = await _db.Companies.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        c.EncryptedPassword = _protector.Encrypt(dto.NewPassword);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Şifre güncellendi: {Name} (Id={Id})", c.Name, c.Id);
        return NoContent();
    }

    // DELETE api/sovos/admin/companies/{id}
    [HttpDelete("companies/{id:int}")]
    public async Task<IActionResult> DeleteCompany(int id)
    {
        _logger.LogInformation("DeleteCompany: id={Id}", id);

        var c = await _db.Companies.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        _db.Companies.Remove(c);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Firma silindi: {Name} (Id={Id})", c.Name, c.Id);
        return NoContent();
    }

    // -------- STUBS — ADIM B'de Worker'a HTTP çağrı eklendiğinde implement edilecek

    [HttpPost("companies/{id:int}/test-login")]
    public IActionResult TestLogin(int id)
    {
        _logger.LogInformation("TestLogin (stub): id={Id}", id);
        return StatusCode(StatusCodes.Status501NotImplemented,
            "test-login ADIM B'de implement edilecek.");
    }

    // POST api/faturakontrol/companies/run-batch
    // Worker'a toplu manuel tarama isteği gönderir. Senkron — Worker tüm firmaları
    // sequential işler ve sonuç (success/failed) döner.
    [HttpPost("companies/run-batch")]
    public async Task<ActionResult<SovosBatchRunResultDto>> RunBatch(
        [FromBody] SovosBatchRunRequestDto req, CancellationToken ct)
    {
        if (req is null || req.CompanyIds is null || req.CompanyIds.Count == 0)
            return BadRequest(new { message = "En az bir firma seçilmelidir." });

        if (req.FromDate > req.ToDate)
            return BadRequest(new { message = "Başlangıç tarihi bitiş tarihinden büyük olamaz." });

        if (req.ToDate.Date > DateTime.Today)
            return BadRequest(new { message = "Bitiş tarihi bugünden ileri olamaz." });

        if ((req.ToDate.Date - req.FromDate.Date).TotalDays > 90)
            return BadRequest(new { message = "Tarih aralığı 90 günden fazla olamaz." });

        // Sadece var olan firmaları yolla — kullanıcı stale UI ile gönderirse fail listesi şişmesin.
        var existingIds = await _db.Companies
            .Where(c => req.CompanyIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(ct);

        var missingIds = req.CompanyIds.Except(existingIds).ToList();

        var http = _httpClientFactory.CreateClient("Worker");

        // Worker uzun süre çalışabilir (5 firma × ~30sn = 150sn+ olası).
        http.Timeout = TimeSpan.FromMinutes(20);

        try
        {
            var workerReq = new
            {
                companyIds = existingIds,
                fromDate = req.FromDate,
                toDate = req.ToDate
            };

            var resp = await http.PostAsJsonAsync("api/worker/run-batch", workerReq, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "Worker run-batch başarısız: status={Status}, body={Body}",
                    (int)resp.StatusCode, body);
                return StatusCode((int)resp.StatusCode, body);
            }

            var workerResult = await resp.Content.ReadFromJsonAsync<SovosBatchRunResultDto>(
                cancellationToken: ct) ?? new SovosBatchRunResultDto();

            // UI'da görünmesi için "bulunamadı" olanları da fail listesine ekle
            foreach (var id in missingIds)
            {
                workerResult.Failed.Add(new SovosBatchRunItemDto
                {
                    CompanyId = id,
                    CompanyName = $"#{id}",
                    Error = "Firma bulunamadı"
                });
            }

            _logger.LogInformation(
                "RunBatch tamamlandı: {Ok} başarılı, {Fail} başarısız",
                workerResult.Success.Count, workerResult.Failed.Count);

            return Ok(workerResult);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Worker'a ulaşılamadı (run-batch)");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "Worker servisine ulaşılamadı." });
        }
    }

    // POST api/sovos/admin/companies/{id}/run-now
    // Worker'a HTTP ile "şu firmayı şimdi tara" der; Worker fire-and-forget 202 döner.
    [HttpPost("companies/{id:int}/run-now")]
    public async Task<IActionResult> RunNowForCompany(int id, CancellationToken ct)
    {
        var exists = await _db.Companies.AnyAsync(c => c.Id == id, ct);
        if (!exists) return NotFound(new { id, message = "Firma bulunamadı" });

        var http = _httpClientFactory.CreateClient("Worker");
        try
        {
            var resp = await http.PostAsync($"api/worker/run-now/{id}", content: null, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Worker run-now başarısız: id={Id}, status={Status}, body={Body}",
                    id, (int)resp.StatusCode, body);
                return StatusCode((int)resp.StatusCode, body);
            }

            _logger.LogInformation("Worker run-now tetiklendi: id={Id}", id);
            return Accepted(new { id, status = "queued" });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Worker'a ulaşılamadı: id={Id}", id);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { id, message = "Worker servisine ulaşılamadı." });
        }
    }
}
