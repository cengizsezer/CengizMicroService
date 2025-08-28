using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace IdentityService.Persistence
{
    public class IdentityContextSeed
    {
        public async Task SeedAsync(
            IdentityDbContext context,
            IWebHostEnvironment env,
            ILogger<IdentityContextSeed> logger,
            bool migrateDb = false)
        {
            if (migrateDb)
            {
                await context.Database.MigrateAsync();
            }

            // === 0) Idempotent yardımcıları ===
            async Task<Role> GetOrCreateRoleAsync(string name)
            {
                var r = await context.RolesApp.FirstOrDefaultAsync(x => x.Name == name);
                if (r is null)
                {
                    r = new Role { Name = name };
                    context.RolesApp.Add(r);
                    await context.SaveChangesAsync();
                }
                return r;
            }

            async Task<Permission> GetOrCreatePermAsync(string key, string? desc = null)
            {
                var p = await context.Permissions.FirstOrDefaultAsync(x => x.Key == key);
                if (p is null)
                {
                    p = new Permission { Key = key, Description = desc };
                    context.Permissions.Add(p);
                    await context.SaveChangesAsync();
                }
                return p;
            }

            async Task LinkRolePermAsync(Role role, Permission perm)
            {
                bool exists = await context.RolePermissions
                    .AnyAsync(x => x.RoleId == role.Id && x.PermissionId == perm.Id);
                if (!exists)
                {
                    context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = perm.Id
                    });
                    await context.SaveChangesAsync();
                }
            }

            // === 1) Permissions ===
            var pExpenseView = await GetOrCreatePermAsync("Expense.View", "Masraf görüntüleme");
            var pExpenseEdit = await GetOrCreatePermAsync("Expense.Edit", "Masraf düzenleme");
            var pBeyannameView = await GetOrCreatePermAsync("Beyanname.View", "Beyanname sayfasını görme");
            var pIlkyardimView = await GetOrCreatePermAsync("Ilkyardim.View", "İlkyardım sayfasını görme");

            // === 2) Roles (custom) ===
            var rAdmin = await GetOrCreateRoleAsync("Admin");
            var rPersonel = await GetOrCreateRoleAsync("Personel");
            var rMaliIsler = await GetOrCreateRoleAsync("MaliIsler");
            var rIdariIsler = await GetOrCreateRoleAsync("IdariIsler");
            var rIlkyardim = await GetOrCreateRoleAsync("Ilkyardim");

            // Role → Permission eşlemeleri (örnek politika)
            await LinkRolePermAsync(rMaliIsler, pExpenseView);
            await LinkRolePermAsync(rMaliIsler, pExpenseEdit);
            await LinkRolePermAsync(rMaliIsler, pBeyannameView);

            await LinkRolePermAsync(rIlkyardim, pIlkyardimView);

            // Personel temel görüntüleyebilir (istersen ekle/çıkar)
            await LinkRolePermAsync(rPersonel, pExpenseView);

            // Admin her şeyi görsün (örnek: tüm izinleri bağla)
            foreach (var perm in await context.Permissions.ToListAsync())
                await LinkRolePermAsync(rAdmin, perm);

            // === 3) Tenants (Firmalar yerine) ===
            async Task<Tenant> AddTenantIfMissing(string firmaNo, string ad, string? vkn)
            {
                var t = await context.Tenants.FirstOrDefaultAsync(x => x.FirmaNo == firmaNo);
                if (t is null)
                {
                    t = new Tenant { FirmaNo = firmaNo, Ad = ad, Vkn = vkn };
                    context.Tenants.Add(t);
                    await context.SaveChangesAsync();
                }
                return t;
            }

            var t201 = await AddTenantIfMissing("201", "Tez Medikal Sağlık Turizm A.Ş", "8410473137");
            var t106 = await AddTenantIfMissing("106", "Biz Klinik Sağlık Eğitim Danışmanlık Turizm A.Ş", "1781140986");
            var t108 = await AddTenantIfMissing("108", "Tezmed Eğitim Danışmanlık ve İnovatif Çözümler A.Ş", "8410902637");
            var t105 = await AddTenantIfMissing("105", "Tez Filo Kiralama Ve Yönetim Hiz.A.Ş", "8420327005");
            var t107 = await AddTenantIfMissing("107", "Tezmed Holding A.Ş", "8420323654");

            // === 4) Users (ASP.NET Identity kullanıcıları) ===
            var userManager = context.GetService<UserManager<User>>();

            async Task<User> EnsureUser(string username, string email, string password)
            {
                var u = await userManager.FindByNameAsync(username);
                if (u is null)
                {
                    u = new User
                    {
                        UserName = username,
                        Email = email,
                        EmailConfirmed = true
                        // NOT: User.Role (legacy) artık kullanılmıyor; boş bırakabiliriz.
                    };
                    var res = await userManager.CreateAsync(u, password);
                    if (!res.Succeeded)
                    {
                        var msg = string.Join(", ", res.Errors.Select(e => e.Description));
                        logger.LogWarning("Kullanıcı oluşturulamadı: {User} - {Err}", username, msg);
                    }
                    else
                    {
                        logger.LogInformation("Kullanıcı oluşturuldu: {User}", username);
                    }
                }
                return u!;
            }

            var admin = await EnsureUser("admin", "admin@example.com", "Admin123!");
            var perso = await EnsureUser("ismail.perso", "ismail@example.com", "Perso123!");
            var cengiz = await EnsureUser("cengiz.sezer", "cengiz@example.com", "Mali123!");
            var serkan = await EnsureUser("serkan.keser", "serkan@example.com", "Idari123!");

            // === 5) User ↔ Tenant eşlemeleri + Tenant içi Roller (UserTenantRole) ===
            async Task EnsureUserTenantRole(User u, Tenant t, Role role)
            {
                // UserTenant var mı?
                var ut = await context.UserTenants.FirstOrDefaultAsync(x => x.UserId == u.Id && x.TenantId == t.Id);
                if (ut is null)
                {
                    ut = new UserTenant { UserId = u.Id, TenantId = t.Id };
                    context.UserTenants.Add(ut);
                    await context.SaveChangesAsync();
                }

                // UserTenantRole var mı?
                var hasRole = await context.UserTenantRoles
                    .AnyAsync(x => x.UserId == u.Id && x.TenantId == t.Id && x.RoleId == role.Id);

                if (!hasRole)
                {
                    context.UserTenantRoles.Add(new UserTenantRole
                    {
                        UserId = u.Id,
                        TenantId = t.Id,
                        RoleId = role.Id
                    });
                    await context.SaveChangesAsync();
                }
            }

            // Örnek atamalar (önceki senaryona paralel)
            // cengiz.sezer → 201,106,108 (MaliIsler)
            await EnsureUserTenantRole(cengiz, t201, rMaliIsler);
            await EnsureUserTenantRole(cengiz, t106, rMaliIsler);
            await EnsureUserTenantRole(cengiz, t108, rMaliIsler);

            // serkan.keser → 105,107 (IdariIsler)
            await EnsureUserTenantRole(serkan, t105, rIdariIsler);
            await EnsureUserTenantRole(serkan, t107, rIdariIsler);

            // admin → tüm tenantlarda Admin
            foreach (var t in new[] { t201, t106, t108, t105, t107 })
                await EnsureUserTenantRole(admin, t, rAdmin);

            // ismail.perso → 201 Personel
            await EnsureUserTenantRole(perso, t201, rPersonel);

            logger.LogInformation("Identity seed tamamlandı.");
        }
    }
}
