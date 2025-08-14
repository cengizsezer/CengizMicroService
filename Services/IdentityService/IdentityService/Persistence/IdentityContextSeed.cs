using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityService.Persistence
{
    public class IdentityContextSeed
    {
        public async Task SeedAsync(IdentityDbContext context, IWebHostEnvironment env, ILogger<IdentityContextSeed> logger, bool migrateDb = false)
        {
            if (migrateDb)
            {
                context.Database.Migrate();
            }

            // === 1. Roller ===
            var roleManager = context.GetService<RoleManager<IdentityRole<int>>>();
            var userManager = context.GetService<UserManager<User>>();

            string[] roles = new[] { "Admin", "Personel", "MaliIsler", "IdariIsler" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole<int>(role));
                }
            }

            // === 2. Kullanıcılar ===
            var users = new List<(string UserName, string Email, string Password, string Role, string FullName)>
            {
                ("admin", "admin@example.com", "Admin123!", "Admin", "Admin Kullanıcı"),
                ("ismail.perso", "ismail@example.com", "Perso123!", "Personel", "İsmail Perso"),
                ("cengiz.sezer", "cengiz@example.com", "Mali123!", "MaliIsler", "Cengiz Sezer"),
                ("serkan.keser", "serkan@example.com", "Idari123!", "IdariIsler", "Serkan Keser")
            };

            foreach (var (UserName, Email, Password, Role, FullName) in users)
            {
                if (await userManager.FindByNameAsync(UserName) == null)
                {
                    var user = new User
                    {
                        UserName = UserName,
                        Email = Email,
                        EmailConfirmed = true,
                        Role = Role
                    };

                    var result = await userManager.CreateAsync(user, Password);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, Role);
                        logger.LogInformation($"Kullanıcı oluşturuldu: {FullName} ({Role})");
                    }
                    else
                    {
                        logger.LogWarning($"Kullanıcı eklenemedi: {UserName} - {string.Join(",", result.Errors.Select(e => e.Description))}");
                    }
                }
            }

            // === 3. Firmalar ===
            if (!context.Firms.Any())
            {
                var f201 = new Firm { Ad = "Tez Medikal Sağlık Turizm A.Ş", Vkn = "8410473137", FirmaNo = "201" };
                var f106 = new Firm { Ad = "Biz Klinik Sağlık Eğitim Danışmanlık Turizm A.Ş", Vkn = "1781140986", FirmaNo = "106" };
                var f108 = new Firm { Ad = "Tezmed Eğitim Danışmanlık ve İnovatif Çözümler A.Ş", Vkn = "8410902637", FirmaNo = "108" };
                var f105 = new Firm { Ad = "Tez Filo Kiralama Ve Yönetim Hiz.A.Ş", Vkn = "8420327005", FirmaNo = "105" };
                var f107 = new Firm { Ad = "Tezmed Holding A.Ş", Vkn = "8420323654", FirmaNo = "107" };

                context.Firms.AddRange(f201, f106, f108, f105, f107);
                await context.SaveChangesAsync();
            }

            // === 4. Kullanıcı-Firma eşleşmeleri ===
            var maliUser = await userManager.FindByNameAsync("cengiz.sezer");
            var idariUser = await userManager.FindByNameAsync("serkan.keser");

            if (maliUser != null && !context.UserFirms.Any(uf => uf.UserId == maliUser.Id))
            {
                context.UserFirms.AddRange(
                    new UserFirm { UserId = maliUser.Id, FirmaId = context.Firms.First(f => f.FirmaNo == "201").Id },
                    new UserFirm { UserId = maliUser.Id, FirmaId = context.Firms.First(f => f.FirmaNo == "106").Id },
                    new UserFirm { UserId = maliUser.Id, FirmaId = context.Firms.First(f => f.FirmaNo == "108").Id }

                );
            }

            if (idariUser != null && !context.UserFirms.Any(uf => uf.UserId == idariUser.Id))
            {
                context.UserFirms.AddRange(
                    new UserFirm { UserId = idariUser.Id, FirmaId = context.Firms.First(f => f.FirmaNo == "105").Id },
                    new UserFirm { UserId = idariUser.Id, FirmaId = context.Firms.First(f => f.FirmaNo == "107").Id }
                );
            }

            await context.SaveChangesAsync();
        }
    }
}
