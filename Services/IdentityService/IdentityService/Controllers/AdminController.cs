using IdentityService.Application.Models.Admin;
using IdentityService.Application.Models.Tenants;
using IdentityService.Domain.Entities;
using IdentityService.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Controllers;

[Route("api/auth/admin")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IdentityDbContext _db;
    private readonly UserManager<User> _userMgr;

    public AdminController(IdentityDbContext db, UserManager<User> userMgr)
    {
        _db = db;
        _userMgr = userMgr;
    }

    // -------- USERS LIST ---------------------------------------------------
    [HttpGet("users")]
    public async Task<ActionResult<PageDto<UserListItemDto>>> GetUsers(
        [FromQuery] int p = 0, [FromQuery] int ps = 50, [FromQuery] string? q = null)
    {
        var usersQ = _db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            usersQ = usersQ.Where(x =>
                x.UserName!.Contains(q) ||
                (x.Email != null && x.Email.Contains(q)));
        }

        var count = await usersQ.CountAsync();

        var users = await usersQ
            .OrderBy(x => x.UserName)
            .Skip(p * ps).Take(ps)
            .Select(u => new UserListItemDto
            {
                Id = u.Id,
                DisplayName = u.UserName ?? "",
                Email = u.Email ?? ""
            })
            .ToListAsync();

        // Rolleri doldur (quick & simple)
        foreach (var u in users)
        {
            var roles = await (from utr in _db.UserTenantRoles
                               join r in _db.RolesApp on utr.RoleId equals r.Id
                               where utr.UserId == u.Id
                               select r.Name).Distinct().ToListAsync();
            u.Roles = roles.ToArray();

            // İstersen "aktif firma" olarak ilk tenant adını yazalım
            u.FirmName = await (from ut in _db.UserTenants
                                join t in _db.Tenants on ut.TenantId equals t.Id
                                where ut.UserId == u.Id
                                select t.Ad).FirstOrDefaultAsync();
        }

        return Ok(new PageDto<UserListItemDto>
        {
            PageIndex = p,
            PageSize = ps,
            Count = count,
            Data = users
        });
    }

    // -------- USER CRUD ----------------------------------------------------
    [HttpGet("users/{id:int}")]
    public async Task<ActionResult<UserEditDto>> GetUser(int id)
    {
        var u = await _db.Users.FindAsync(id);
        if (u is null) return NotFound();

        return Ok(new UserEditDto
        {
            Id = u.Id,
            DisplayName = u.UserName ?? "",
            Email = u.Email ?? "",
            Phone = u.PhoneNumber
        });
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] UserEditDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.DisplayName) || string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest("Zorunlu alanlar eksik.");

        var user = new User
        {
            UserName = dto.DisplayName,
            Email = dto.Email,
            EmailConfirmed = true,
            PhoneNumber = dto.Phone
        };

        var pwd = string.IsNullOrWhiteSpace(dto.Password) ? "Temp123!" : dto.Password!;
        var res = await _userMgr.CreateAsync(user, pwd);
        if (!res.Succeeded) return BadRequest(string.Join(", ", res.Errors.Select(e => e.Description)));

        return Ok();
    }

    [HttpPut("users/{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UserEditDto dto)
    {
        var u = await _db.Users.FindAsync(id);
        if (u is null) return NotFound();

        u.UserName = dto.DisplayName;
        u.Email = dto.Email;
        u.PhoneNumber = dto.Phone;
        await _db.SaveChangesAsync();

        // Şifre değişimi istenirse
        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            var token = await _userMgr.GeneratePasswordResetTokenAsync(u);
            var ok = await _userMgr.ResetPasswordAsync(u, token, dto.Password);
            if (!ok.Succeeded) return BadRequest(string.Join(", ", ok.Errors.Select(e => e.Description)));
        }

        return Ok();
    }

    [HttpDelete("users/{id:int}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var u = await _db.Users.FindAsync(id);
        if (u is null) return NotFound();
        await _userMgr.DeleteAsync(u);
        return NoContent();
    }

    // -------- ROLES --------------------------------------------------------
    [HttpGet("roles")]
    public async Task<ActionResult<List<RoleDto>>> GetRoles()
        => Ok(await _db.RolesApp.Select(r => new RoleDto { Id = r.Id, Name = r.Name }).ToListAsync());

    [HttpGet("users/{id:int}/roles")]
    public async Task<ActionResult<IList<string>>> GetUserRoles(int id)
    {
        var roles = await (from utr in _db.UserTenantRoles
                           join r in _db.RolesApp on utr.RoleId equals r.Id
                           where utr.UserId == id
                           select r.Name).Distinct().ToListAsync();
        return Ok(roles);
    }

    // Tüm tenantlarda aynı rol seti ver (basit senaryo). İstersen tenant paramı ekleyip daralt.
    [HttpPut("users/{id:int}/roles")]
    public async Task<IActionResult> SetUserRoles(int id, [FromBody] List<string> roleNames)
    {
        var u = await _db.Users.FindAsync(id);
        if (u is null) return NotFound();

        var roleIds = await _db.RolesApp.Where(r => roleNames.Contains(r.Name)).Select(r => r.Id).ToListAsync();
        var userTenants = await _db.UserTenants.Where(x => x.UserId == id).ToListAsync();

        // önce mevcut mappingleri sil
        var existing = _db.UserTenantRoles.Where(x => x.UserId == id);
        _db.UserTenantRoles.RemoveRange(existing);

        // her tenant için yeni roller
        foreach (var ut in userTenants)
        {
            foreach (var rid in roleIds)
            {
                _db.UserTenantRoles.Add(new UserTenantRole { UserId = id, TenantId = ut.TenantId, RoleId = rid });
            }
        }

        await _db.SaveChangesAsync();
        return Ok();
    }

    // -------- FIRMS (TENANTS) ---------------------------------------------
    [HttpGet("firms")]
    public async Task<ActionResult<List<FirmDto>>> GetFirms()
        => Ok(await _db.Tenants.Select(t => new FirmDto { Id = t.Id, Name = t.Ad, FirmaNo = t.FirmaNo }).ToListAsync());

    [HttpGet("users/{id:int}/firm")]
    public async Task<ActionResult<int?>> GetUserFirm(int id)
    {
        var first = await _db.UserTenants.Where(x => x.UserId == id).Select(x => x.TenantId).FirstOrDefaultAsync();
        if (first == 0) return Ok(null);
        return Ok(first);
    }

    // tek firma ata (varsa diğerlerini temizle)
    [HttpPut("users/{id:int}/firm")]
    public async Task<IActionResult> SetUserFirm(int id, [FromBody] dynamic body)
    {
        int? firmId = (int?)body?.firmId;
        if (firmId is null) return BadRequest("firmId zorunlu.");

        var u = await _db.Users.FindAsync(id);
        if (u is null) return NotFound();

        var existsFirm = await _db.Tenants.AnyAsync(t => t.Id == firmId);
        if (!existsFirm) return BadRequest("Firma bulunamadı.");

        var existing = _db.UserTenants.Where(x => x.UserId == id);
        _db.UserTenants.RemoveRange(existing);
        _db.UserTenants.Add(new UserTenant { UserId = id, TenantId = firmId.Value });

        // Roller (opsiyonel): firmayı değiştirince roller kalsın istersen bu kısmı sil
        var roles = await _db.UserTenantRoles.Where(x => x.UserId == id).ToListAsync();
        _db.UserTenantRoles.RemoveRange(roles);

        await _db.SaveChangesAsync();
        return Ok();
    }
}
