using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sovos.InvoiceWorker.Core.Entities;
using Sovos.InvoiceWorker.Core.Interfaces;
using SovosService.Api.Application.Models;
using SovosService.Api.Persistence;

namespace SovosService.Api.Controllers;

[Route("api/faturakontrol")]
[ApiController]
[Authorize(Roles = "Admin,PKF")]
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

        var entity = new Company
        {
            Name = dto.Name,
            CompanyCode = dto.CompanyCode,
            Username = dto.Username,
            EncryptedPassword = _protector.Encrypt(dto.Password),
            NotificationEmails = dto.NotificationEmails,
            IsActive = dto.IsActive,
            ScheduleMode = dto.ScheduleMode,
            ScheduleHour = dto.ScheduleHour
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
        _logger.LogInformation("UpdateCompany: id={Id}", id);

        var c = await _db.Companies.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        c.Name = dto.Name;
        c.CompanyCode = dto.CompanyCode;
        c.Username = dto.Username;
        c.NotificationEmails = dto.NotificationEmails;
        c.IsActive = dto.IsActive;
        c.ScheduleMode = dto.ScheduleMode;
        c.ScheduleHour = dto.ScheduleHour;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Firma güncellendi: {Name} (Id={Id})", c.Name, c.Id);
        return NoContent();
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
