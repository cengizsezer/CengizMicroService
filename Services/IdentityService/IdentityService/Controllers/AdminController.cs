using IdentityService.Application.Models;
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


    // -------- USER CRUD ----------------------------------------------------
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
                Email = u.Email ?? "",

                // İlk tenant adı (aktif firma gibi göstermek için)
                FirmName = (
                    from ut in _db.UserTenants
                    join t in _db.Tenants on ut.TenantId equals t.Id
                    where ut.UserId == u.Id
                    select t.Ad
                ).FirstOrDefault(),

                // Kullanıcının tüm rolleri (tenant fark etmeksizin benzersiz)
                Roles = (
                    from utr in _db.UserTenantRoles
                    join r in _db.RolesApp on utr.RoleId equals r.Id
                    where utr.UserId == u.Id
                    select r.Name
                ).Distinct().ToArray()
            })
            .AsNoTracking()
            .ToListAsync();

        return Ok(new PageDto<UserListItemDto>
        {
            PageIndex = p,
            PageSize = ps,
            Count = count,
            Data = users
        });
    }

    [HttpGet("users/{id:int}")]
    public async Task<ActionResult<UserEditDto>> GetUser(int id)
    {
        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return NotFound();

        var firmId = await _db.UserTenants
            .Where(x => x.UserId == id)
            .Select(x => x.TenantId)
            .FirstOrDefaultAsync();

        var roles = await (from utr in _db.UserTenantRoles
                           join r in _db.RolesApp on utr.RoleId equals r.Id
                           where utr.UserId == id
                           select r.Name).Distinct().ToListAsync();

        var dto = new UserEditDto
        {
            Id = u.Id,
            UserName = u.UserName ?? "",
            Email = u.Email ?? "",
            Phone = u.PhoneNumber ?? ""
           
        };

        return Ok(dto);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] NewUserDto dto)
    {
        // ✅ Artık UserName ve Email zorunlu
        if (string.IsNullOrWhiteSpace(dto.UserName) || string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest("UserName ve Email zorunlu.");

        var user = new User
        {
            UserName = dto.UserName,    // ✅ UserName kullan
            Email = dto.Email,
            EmailConfirmed = true,
            PhoneNumber = dto.Phone
            // DisplayName için ayrı bir alan yoksa şimdilik claim ya da ayrı tabloya yazabilirsin (opsiyonel)
        };

        var pwd = string.IsNullOrWhiteSpace(dto.Password) ? "Temp123!" : dto.Password!;
        var res = await _userMgr.CreateAsync(user, pwd);
        if (!res.Succeeded) return BadRequest(string.Join(", ", res.Errors.Select(e => e.Description)));

        return Ok();
    }

    [HttpPost("users/{id:int}/password")]
    public async Task<IActionResult> AdminChangePassword(int id, [FromBody] UserChangePasswordDto dto)
    {
        var u = await _userMgr.FindByIdAsync(id.ToString());
        if (u is null) return NotFound();

        if (!_userMgr.SupportsUserPassword)
            return StatusCode(501, "Password store is not supported.");

        // Token’a gerek yok: remove + add
        await _userMgr.RemovePasswordAsync(u);
        var r = await _userMgr.AddPasswordAsync(u, dto.NewPassword);
        if (!r.Succeeded) return BadRequest(string.Join(", ", r.Errors.Select(e => e.Description)));

        return NoContent();
    }


    [HttpPut("users/{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UserEditDto dto)
    {
        var u = await _userMgr.FindByIdAsync(id.ToString());
        if (u is null) return NotFound();

        // Normalize/unique için UserManager kullan
        if (dto.UserName != u.UserName)
        {
            var r1 = await _userMgr.SetUserNameAsync(u, dto.UserName);
            if (!r1.Succeeded) return BadRequest(string.Join(", ", r1.Errors.Select(e => e.Description)));
        }
        if (dto.Email != u.Email)
        {
            var r2 = await _userMgr.SetEmailAsync(u, dto.Email);
            if (!r2.Succeeded) return BadRequest(string.Join(", ", r2.Errors.Select(e => e.Description)));
        }

        u.PhoneNumber = dto.Phone;
        var r = await _userMgr.UpdateAsync(u);
        if (!r.Succeeded) return BadRequest(string.Join(", ", r.Errors.Select(e => e.Description)));

        return NoContent();
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
    [HttpGet("users/{id:int}/firms")]
    public async Task<ActionResult<List<UserFirmDto>>> GetUserFirms(int id)
    {
        var q =
            from ut in _db.UserTenants
            join t in _db.Tenants on ut.TenantId equals t.Id
            where ut.UserId == id
            select new UserFirmDto
            {
                TenantId = t.Id,
                TenantName = t.Ad,
                Roles = (
                    from utr in _db.UserTenantRoles
                    join r in _db.RolesApp on utr.RoleId equals r.Id
                    where utr.UserId == id && utr.TenantId == t.Id
                    select r.Name
                ).ToList()
            };

        return Ok(await q.AsNoTracking().ToListAsync());
    }

    [HttpGet("firms")]
    public async Task<ActionResult<List<FirmDto>>> GetFirms()
    => Ok(await _db.Tenants
        .Select(t => new FirmDto
        {
            Id = t.Id,
            Name = t.Ad,
            FirmaNo = t.FirmaNo
        })
        .AsNoTracking()
        .ToListAsync());

    [HttpPost("users/{id:int}/firms")]
    public async Task<IActionResult> AddUserFirm(int id, [FromBody] UserFirmAssignDto dto)
    {
        var okTenant = await _db.Tenants.AnyAsync(t => t.Id == dto.TenantId);
        if (!okTenant) return BadRequest("Firma yok.");

        var ut = await _db.UserTenants.SingleOrDefaultAsync(x => x.UserId == id && x.TenantId == dto.TenantId);
        if (ut is null)
            _db.UserTenants.Add(new UserTenant { UserId = id, TenantId = dto.TenantId });

        if (dto.Roles is not null && dto.Roles.Count > 0)
        {
            var roleMap = await _db.RolesApp
                .Where(r => dto.Roles.Contains(r.Name))
                .ToDictionaryAsync(r => r.Name, r => r.Id);

            var existing = await _db.UserTenantRoles
                .Where(x => x.UserId == id && x.TenantId == dto.TenantId)
                .ToListAsync();

            // merge: mevcut olmayanları ekle
            var existingNames = (
                from e in existing
                join r in _db.RolesApp on e.RoleId equals r.Id
                select r.Name
            ).ToHashSet();

            foreach (var rn in dto.Roles.Distinct())
                if (!existingNames.Contains(rn) && roleMap.TryGetValue(rn, out var rid))
                    _db.UserTenantRoles.Add(new UserTenantRole { UserId = id, TenantId = dto.TenantId, RoleId = rid });
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("users/{id:int}/firms/{tenantId:int}")]
    public async Task<IActionResult> RemoveUserFirm(int id, int tenantId)
    {
        var ut = await _db.UserTenants.SingleOrDefaultAsync(x => x.UserId == id && x.TenantId == tenantId);
        if (ut is null) return NoContent();

        _db.UserTenants.Remove(ut);
        var utr = _db.UserTenantRoles.Where(x => x.UserId == id && x.TenantId == tenantId);
        _db.UserTenantRoles.RemoveRange(utr);

        await _db.SaveChangesAsync();
        return NoContent();
    }
    [HttpPut("users/{id:int}/firms")]
    public async Task<IActionResult> SetUserFirms(int id, [FromBody] SetUserFirmsRequest req)
    {
        if (req?.Firms is null) return BadRequest("Firms gereklidir.");

        var tenantIds = req.Firms.Select(f => f.TenantId).Distinct().ToList();
        var existsAll = await _db.Tenants.CountAsync(t => tenantIds.Contains(t.Id)) == tenantIds.Count;
        if (!existsAll) return BadRequest("Geçersiz firmalar var.");

        // ---- 1) UserTenants: gelen set’e eşitle ----
        var currentUT = await _db.UserTenants.Where(x => x.UserId == id).ToListAsync();
        var currentTenantIds = currentUT.Select(x => x.TenantId).ToHashSet();

        var toRemoveUT = currentUT.Where(x => !tenantIds.Contains(x.TenantId)).ToList();
        _db.UserTenants.RemoveRange(toRemoveUT);

        var toAddTenantIds = tenantIds.Where(tid => !currentTenantIds.Contains(tid)).ToList();
        foreach (var tid in toAddTenantIds)
            _db.UserTenants.Add(new UserTenant { UserId = id, TenantId = tid });

        await _db.SaveChangesAsync();

        // ---- 2) UserTenantRoles: SADECE Roles alanı GÖNDERİLMİŞ firmalarda değiştir ----
        var firmsWithRoles = req.Firms.Where(f => f.Roles != null).ToList();
        if (firmsWithRoles.Count > 0)
        {
            var roleNames = firmsWithRoles.SelectMany(f => f.Roles!).Distinct().ToList();
            var roleMap = await _db.RolesApp
                .Where(r => roleNames.Contains(r.Name))
                .ToDictionaryAsync(r => r.Name, r => r.Id);

            // İlgili tenantlar için mevcut rolleri temizle
            var targetTenantIds = firmsWithRoles.Select(f => f.TenantId).Distinct().ToList();
            var currentRoles = await _db.UserTenantRoles
                .Where(x => x.UserId == id && targetTenantIds.Contains(x.TenantId))
                .ToListAsync();
            _db.UserTenantRoles.RemoveRange(currentRoles);

            // Gönderilen rolleri ekle
            foreach (var firm in firmsWithRoles)
            {
                foreach (var rn in firm.Roles!.Distinct())
                {
                    if (!roleMap.TryGetValue(rn, out var rid)) return BadRequest($"Geçersiz rol: {rn}");
                    _db.UserTenantRoles.Add(new UserTenantRole
                    {
                        UserId = id,
                        TenantId = firm.TenantId,
                        RoleId = rid
                    });
                }
            }

            await _db.SaveChangesAsync();
        }

        return NoContent();
    }


}
