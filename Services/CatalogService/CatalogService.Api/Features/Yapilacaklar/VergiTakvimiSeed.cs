using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Services;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Yapilacaklar
{
    /// <summary>
    /// Vergi takviminin <b>ilk</b> doldurulması: 2026 ve 2027 dönemleri.
    ///
    /// Buradaki kurallar yalnız başlangıç değerini üretir; asıl kaynak tablodur. Yasal
    /// süreler tebliğle değişiyor ve uzatılıyor — gerçek tarih Yönetim → Vergi Takvimi
    /// ekranından düzeltilir, seed mevcut satırın üzerine <b>asla</b> yazmaz. Bu yüzden
    /// seed'in ürettiği tarihler KULLANICI DOĞRULAMASI bekler (yürürlükteki takvimle).
    ///
    /// Hafta sonuna düşen son gün pazartesiye kaydırılır. Resmî tatil / bayram kaydırması
    /// YAPILMAZ (tatil takvimi yıla göre değişiyor); o satırlar elle düzeltilmeli.
    ///
    /// Son günü seed anında geçmiş olan satırlar <b>pasif</b> yazılır: geçmiş dönemlerin
    /// tamamlama kaydı olmadığından hepsi "gecikti" görünür, şerit hiç kalkmazdı.
    /// </summary>
    public static class VergiTakvimiSeed
    {
        public static readonly int[] Yillar = { 2026, 2027 };

        /// <summary>(Kod, ad, tekrar, dönem sayısı, son gün kuralı).</summary>
        private static readonly (string Kod, string Ad, IsTekrari Tekrar, int[] Donemler, Func<IsDonemi, DateTime> SonGun)[] Kurallar =
        {
            // KDV-1: izleyen ayın 28'i.
            ("0015", "KDV beyannamesi (KDV-1)", IsTekrari.Aylik, Enumerable.Range(1, 12).ToArray(),
                d => IzleyenAyinGunu(d, 1, 28)),

            // Muhtasar ve prim hizmet: izleyen ayın 26'sı.
            ("0003", "Muhtasar ve prim hizmet beyannamesi", IsTekrari.Aylik, Enumerable.Range(1, 12).ToArray(),
                d => IzleyenAyinGunu(d, 1, 26)),

            // Damga vergisi (sürekli mükellef): izleyen ayın 26'sı.
            ("0040", "Damga vergisi beyannamesi", IsTekrari.Aylik, Enumerable.Range(1, 12).ToArray(),
                d => IzleyenAyinGunu(d, 1, 26)),

            // Geçici vergi: çeyrek bitimini izleyen ikinci ayın 17'si. 4. dönem beyanı
            // kaldırıldığı için yalnız 1–3.
            ("0033", "Geçici vergi beyannamesi", IsTekrari.UcAylik, new[] { 1, 2, 3 },
                d => IzleyenAyinGunu(d, 2, 17)),

            // Kurumlar vergisi: izleyen yılın nisan ayı sonu.
            ("0010", "Kurumlar vergisi beyannamesi", IsTekrari.Yillik, new[] { 1 },
                d => new DateTime(d.Yil + 1, 4, 30))
        };

        /// <summary>Dönem sonunu izleyen <paramref name="ay"/>'ncı ayın <paramref name="gun"/>'ü.</summary>
        private static DateTime IzleyenAyinGunu(IsDonemi d, int ay, int gun)
        {
            var hedef = d.Bit.AddDays(1).AddMonths(ay - 1);
            return IsTakvimHesabi.AyinGunu(hedef.Year, hedef.Month, gun);
        }

        public static DateTime HaftaSonuKaydir(DateTime t) => t.DayOfWeek switch
        {
            DayOfWeek.Saturday => t.AddDays(2),
            DayOfWeek.Sunday => t.AddDays(1),
            _ => t
        };

        /// <summary>Seed'in üreteceği satırlar (kaydetmeden). Aktiflik <paramref name="bugun"/>'e göre.</summary>
        public static List<VergiTakvimi> Uret(DateTime bugun)
        {
            var liste = new List<VergiTakvimi>();

            foreach (var yil in Yillar)
            foreach (var (kod, ad, tekrar, donemler, sonGunKurali) in Kurallar)
            foreach (var no in donemler)
            {
                var donem = new IsDonemi(tekrar, yil, no);
                var sonGun = HaftaSonuKaydir(sonGunKurali(donem));

                liste.Add(new VergiTakvimi
                {
                    MukellefiyetKodu = kod,
                    Ad = ad,
                    Tekrar = tekrar,
                    Yil = yil,
                    DonemNo = no,
                    DonemBas = donem.Bas,
                    DonemBit = donem.Bit,
                    SonGun = sonGun,
                    Aktif = sonGun >= bugun.Date
                });
            }

            return liste;
        }

        /// <summary>Eksik satırları ekler; mevcut satıra (elle düzeltilmiş olabilir) dokunmaz.</summary>
        /// <returns>Eklenen ve toplam satır sayısı.</returns>
        public static async Task<(int Eklenen, int Toplam)> SeedAsync(CatalogContext db, DateTime bugun,
                                                                      CancellationToken ct = default)
        {
            var mevcut = await db.VergiTakvimi
                .Select(t => new { t.MukellefiyetKodu, t.Tekrar, t.Yil, t.DonemNo })
                .ToListAsync(ct);
            var kayitli = mevcut.Select(m => (m.MukellefiyetKodu, m.Tekrar, m.Yil, m.DonemNo)).ToHashSet();

            var eklenen = 0;
            foreach (var satir in Uret(bugun))
            {
                if (!kayitli.Add((satir.MukellefiyetKodu, satir.Tekrar, satir.Yil, satir.DonemNo))) continue;
                db.VergiTakvimi.Add(satir);
                eklenen++;
            }

            if (eklenen > 0) await db.SaveChangesAsync(ct);
            return (eklenen, mevcut.Count + eklenen);
        }

        public static async Task SeedVeLoglaAsync(CatalogContext db, DateTime bugun, ILogger logger)
        {
            var (eklenen, toplam) = await SeedAsync(db, bugun);
            if (eklenen > 0)
                logger.LogWarning("Vergi takvimi: {Eklenen} satır eklendi (toplam {Toplam}). Tarihler seed kuralından " +
                                  "üretildi — yürürlükteki takvimle doğrulanmalı (Yönetim → Vergi Takvimi).", eklenen, toplam);
            else
                logger.LogInformation("Vergi takvimi: eksik satır yok (toplam {Toplam}).", toplam);
        }
    }
}
