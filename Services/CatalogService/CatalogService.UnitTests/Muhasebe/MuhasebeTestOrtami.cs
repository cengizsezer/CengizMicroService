using CatalogService.Api.Features.Muhasebe.Domain;
using CatalogService.Api.Infrastructure.Accessor;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.UnitTests.Muhasebe
{
    /// <summary>
    /// Testler için bellek içi <see cref="CatalogContext"/> ve küçük bir MSUGT hesap planı.
    /// Gerçek seed dosyasının kısaltılmış hâli: sınıf → grup → kebir, hepsi sistem hesabı.
    /// </summary>
    public static class MuhasebeTestOrtami
    {
        public const string TenantNo = "201";

        public static CatalogContext YeniContext() => YeniContext(YeniVeritabaniAdi());

        /// <summary>Aynı bellek içi veritabanına birden fazla context açmak için (eşzamanlılık testleri).</summary>
        public static CatalogContext YeniContext(string veritabaniAdi)
        {
            var options = new DbContextOptionsBuilder<CatalogContext>()
                .UseInMemoryDatabase(veritabaniAdi)
                .Options;

            return new CatalogContext(options, new FixedTenantAccessor(TenantNo));
        }

        public static string YeniVeritabaniAdi() => $"muhasebe-{Guid.NewGuid()}";

        /// <summary>Varsayılan maske (3,2,2,4) ve kısaltılmış THP ile dolu bir context döner.</summary>
        public static async Task<CatalogContext> HazirContextAsync(string? veritabaniAdi = null)
        {
            var db = YeniContext(veritabaniAdi ?? YeniVeritabaniAdi());

            db.KodMaskeleri.Add(new KodMaskesi { SegmentUzunluk = "3,2,2,4", Ayrac = "." });
            await db.SaveChangesAsync();

            // Sınıf → grup → kebir; kebirler yaprak olduğu için hareket görür.
            var sinif1 = await EkleAsync(db, null, "1", "DÖNEN VARLIKLAR");
            var grup10 = await EkleAsync(db, sinif1, "0", "Hazır Değerler");
            await EkleAsync(db, grup10, "0", "Kasa");
            await EkleAsync(db, grup10, "2", "Bankalar");

            var sinif7 = await EkleAsync(db, null, "7", "MALİYET HESAPLARI");
            var grup77 = await EkleAsync(db, sinif7, "7", "Genel Yönetim Giderleri");
            await EkleAsync(db, grup77, "0", "Genel Yönetim Giderleri");

            return db;
        }

        public static Task<HesapPlani> HesapAsync(CatalogContext db, string kod)
            => db.HesapPlanlari.FirstAsync(h => h.Kod == kod);

        /// <summary>Bir hesaba kesinleşmiş fiş üzerinden hareket yazar (kural 2 ve 8 testleri için).</summary>
        public static async Task HareketYazAsync(CatalogContext db, int hesapId, decimal borc = 100m)
        {
            var fis = new Fis
            {
                DonemYil = 2026,
                FisNo = $"2026/{Guid.NewGuid().ToString()[..6]}",
                Tarih = new DateTime(2026, 1, 15),
                FisTuru = FisTuru.Mahsup,
                Durum = FisDurum.Kesinlesmis,
                OlusturanId = 1,
                OlusturmaT = new DateTime(2026, 1, 15)
            };
            db.Fisler.Add(fis);
            await db.SaveChangesAsync();

            db.FisSatirlar.Add(new FisSatir
            {
                FisId = fis.Id,
                SiraNo = 1,
                HesapId = hesapId,
                Borc = borc,
                Alacak = 0m
            });
            await db.SaveChangesAsync();
        }

        /// <summary>
        /// Seed ile aynı mantık: sistem hesabı, Yol üst hesabın Id'sinden türer,
        /// karakter kök sınıf rakamından gelir.
        /// </summary>
        private static async Task<HesapPlani> EkleAsync(CatalogContext db, HesapPlani? ust, string segment, string ad)
        {
            var kod = (ust?.Kod ?? string.Empty) + segment;
            var seviye = (byte)((ust?.Seviye ?? 0) + 1);

            var hesap = new HesapPlani
            {
                UstHesapId = ust?.Id,
                Kod = kod,
                KodDuz = kod,
                SegmentKod = segment,
                Ad = ad,
                Seviye = seviye,
                HesapTuru = seviye switch
                {
                    1 => HesapTuru.Sinif,
                    2 => HesapTuru.Grup,
                    _ => HesapTuru.Kebir
                },
                Karakter = kod[0] switch
                {
                    '1' or '2' => HesapKarakter.Aktif,
                    '3' or '4' or '5' => HesapKarakter.Pasif,
                    '6' => HesapKarakter.Gelir,
                    '7' => HesapKarakter.Gider,
                    '8' => HesapKarakter.Maliyet,
                    _ => HesapKarakter.Nazim
                },
                HareketGorur = seviye == 3,
                SistemHesabi = true,
                Yol = ust is null ? "/" : $"{ust.Yol}{ust.Id}/",
                Aktif = true
            };

            if (ust is not null) ust.HareketGorur = false;

            db.HesapPlanlari.Add(hesap);
            await db.SaveChangesAsync();
            return hesap;
        }
    }
}
