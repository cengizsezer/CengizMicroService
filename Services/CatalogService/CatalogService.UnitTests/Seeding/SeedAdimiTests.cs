using CatalogService.Api.Features.MevzuatGrafigi.Domain;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.Api.Infrastructure.Seeding;
using CatalogService.UnitTests.BankaEkstre;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CatalogService.UnitTests.Seeding
{
    /// <summary>
    /// Açılış seed adımları aynı context'i paylaşır: düşen adımın bekleyen kayıtları sonraki
    /// adımın SaveChanges'iyle yazılmamalı; başarılı adımlarda davranış değişmemeli.
    /// </summary>
    public class SeedAdimiTests
    {
        private static MevzuatKart Kart(string baslik) =>
            new() { Tur = MevzuatKartTuru.Kanun, Baslik = baslik, OlusturmaTarihi = DateTime.Now };

        private sealed class KayitLogger : ILogger
        {
            public readonly List<(LogLevel Seviye, string Mesaj)> Kayitlar = new();
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                                    Func<TState, Exception?, string> formatter)
                => Kayitlar.Add((logLevel, formatter(state, exception)));
        }

        [Fact]
        public async Task Dusen_adimin_bekleyen_kayitlari_sonraki_adimla_yazilmaz()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            var logger = new KayitLogger();

            // Mevzuat Grafiği seed'inin yaşadığı durum: varlık eklenir, kayıt patlar.
            await SeedAdimi.CalistirAsync(logger, "Düşen", db, () =>
            {
                db.MevzuatKartlar.Add(Kart("Yarım kalan"));
                throw new InvalidOperationException("kayıt patladı");
            });

            Assert.Empty(db.ChangeTracker.Entries());

            await SeedAdimi.CalistirAsync(logger, "Sonraki", db, async () =>
            {
                db.MevzuatKartlar.Add(Kart("Sonraki adımın kaydı"));
                await db.SaveChangesAsync();
            });

            Assert.Equal(new[] { "Sonraki adımın kaydı" }, await db.MevzuatKartlar.Select(k => k.Baslik).ToArrayAsync());
            Assert.Contains(logger.Kayitlar, k => k.Seviye == LogLevel.Error && k.Mesaj.Contains("Düşen: seed BAŞARISIZ"));
            Assert.Contains(logger.Kayitlar, k => k.Seviye == LogLevel.Information && k.Mesaj.Contains("Sonraki: seed tamamlandı"));
        }

        [Fact]
        public async Task Dusen_adim_onceki_adimin_kaydettiklerine_dokunmaz()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            var logger = new KayitLogger();

            await SeedAdimi.CalistirAsync(logger, "İlk", db, async () =>
            {
                db.MevzuatKartlar.Add(Kart("Kaydedilen"));
                await db.SaveChangesAsync();
            });
            await SeedAdimi.CalistirAsync(logger, "Düşen", db, () => throw new InvalidOperationException("x"));

            // Clear yalnız izlemeyi bırakır; veritabanındaki kayıt yerinde.
            Assert.Equal(new[] { "Kaydedilen" }, await db.MevzuatKartlar.Select(k => k.Baslik).ToArrayAsync());
        }

        [Fact]
        public async Task Basarili_adimda_context_degismez()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            var logger = new KayitLogger();

            // Başarılı adım context'i temizlemez: izlenen ve bekleyen varlıklar olduğu gibi kalır
            // (önceki davranış; sonraki adım bekleyeni kaydeder).
            MevzuatKart izlenen = null!;
            await SeedAdimi.CalistirAsync(logger, "Kaydeden", db, async () =>
            {
                izlenen = Kart("İzlenen");
                db.MevzuatKartlar.Add(izlenen);
                await db.SaveChangesAsync();
                db.MevzuatKartlar.Add(Kart("Bekleyen"));
                return;
            });

            Assert.Equal(EntityState.Unchanged, db.Entry(izlenen).State);
            Assert.Single(db.ChangeTracker.Entries<MevzuatKart>(), e => e.State == EntityState.Added);

            await SeedAdimi.CalistirAsync(logger, "Sonraki", db, () => db.SaveChangesAsync());

            Assert.Equal(new[] { "Bekleyen", "İzlenen" },
                         (await db.MevzuatKartlar.Select(k => k.Baslik).ToListAsync()).OrderBy(x => x, StringComparer.Ordinal));
            Assert.DoesNotContain(logger.Kayitlar, k => k.Seviye == LogLevel.Error);
        }

        [Fact]
        public async Task Contextsiz_overload_sonucu_doner_ve_hatayi_yutar()
        {
            var logger = new KayitLogger();

            Assert.True(await SeedAdimi.CalistirAsync(logger, "Tenant 1", () => Task.CompletedTask));
            Assert.False(await SeedAdimi.CalistirAsync(logger, "Tenant 2", () => throw new InvalidOperationException("x")));
            Assert.Contains(logger.Kayitlar, k => k.Seviye == LogLevel.Error && k.Mesaj.Contains("Tenant 2: seed BAŞARISIZ"));
        }
    }
}
