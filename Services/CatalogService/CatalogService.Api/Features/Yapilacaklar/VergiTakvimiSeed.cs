using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Services;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Yapilacaklar
{
    /// <summary>
    /// Vergi takviminin doldurulması: 2026 ve 2027 dönemleri.
    ///
    /// Buradaki kurallar yalnız başlangıç değerini üretir; asıl kaynak tablodur. Yasal
    /// süreler tebliğle değişiyor ve uzatılıyor — gerçek tarih Yönetim → Vergi Takvimi
    /// ekranından düzeltilir; elle düzenlenmiş satıra (<see cref="VergiTakvimi.ElleDuzenlendi"/>)
    /// seed <b>asla</b> dokunmaz. Seed'in ürettiği tarihler KULLANICI DOĞRULAMASI bekler.
    ///
    /// <b>Son gün NOMİNAL kuraldır, kaydırma YOKTUR (Prompt 7B).</b> Önceki sürüm hafta sonuna düşen
    /// son günü pazartesiye kaydırıyordu; gerçek tahakkuk fişleri nominal tarihi yazıyor (Muhtasar
    /// 08/2026: 26.09 cumartesi, Geçici vergi 2026/1: 17.05 pazar). Kaydırma uygulamada GERÇEKTEN GEÇ
    /// bir tarih gösteriyordu — kullanıcı güvenirse beyannameyi geç verir. Yarım çalışan bir kaydırma
    /// kuralı hiç olmamasından kötüdür: bayram kaydırması zaten yapılmıyordu, hafta sonu kaydırması da
    /// yanlış yönde çalışıyordu. Nominal tarih güvenli taraftır. Hafta sonu, resmî tatil ve bayram
    /// kaydırması OTOMATİK YAPILMAZ; gerekiyorsa satır elle düzeltilir.
    ///
    /// Eski kaydırmayla yazılmış satırlar yeniden üretilir: seed'in kendi değeri (aynı ad, eski
    /// kaydırmalı tarih) nominale çekilir; ikisinden de farklı tarih elle düzeltilmiş sayılır ve korunur.
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

        /// <summary>
        /// Prompt 7B'den ÖNCEKİ seed'in yazdığı değer — yalnız eski satırı TANIMAK için (yeni tarih
        /// üretmek için kullanılmaz). Bu değerde duran satır seed'indir, nominale çekilebilir.
        /// </summary>
        private static DateTime EskiKaydirmaliTarih(DateTime t) => t.DayOfWeek switch
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
                var sonGun = sonGunKurali(donem);   // nominal; kaydırma yok

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

        /// <summary>Seed sonucu.</summary>
        /// <param name="Yenilenen">Eski kaydırmalı tarihten nominale çekilen seed satırı.</param>
        /// <param name="ElleSayilan">Ne nominal ne eski seed değeri: elle düzeltilmiş sayıldı, işaretlendi, korundu.</param>
        public sealed record Sonuc(int Eklenen, int Yenilenen, int ElleSayilan, int Toplam);

        /// <summary>
        /// İdempotent: eksik satırı ekler; seed'in kendi satırını nominal tarihe çeker; elle düzenlenmiş
        /// satıra dokunmaz. Aktiflik ve tamamlamalar korunur (tamamlama satır Id'si + dönem anahtarına
        /// bağlıdır, tarihe değil — Dönem panosu ve Yapılacaklar yeni tarihi kendiliğinden okur).
        /// </summary>
        public static async Task<Sonuc> SeedAsync(CatalogContext db, DateTime bugun, CancellationToken ct = default)
        {
            var mevcut = (await db.VergiTakvimi.ToListAsync(ct))
                .GroupBy(t => (t.MukellefiyetKodu, t.Tekrar, t.Yil, t.DonemNo))
                .ToDictionary(g => g.Key, g => g.First());

            int eklenen = 0, yenilenen = 0, elle = 0;
            foreach (var satir in Uret(bugun))
            {
                var anahtar = (satir.MukellefiyetKodu, satir.Tekrar, satir.Yil, satir.DonemNo);
                if (!mevcut.TryGetValue(anahtar, out var mevcutSatir))
                {
                    db.VergiTakvimi.Add(satir);
                    mevcut[anahtar] = satir;
                    eklenen++;
                    continue;
                }

                if (mevcutSatir.ElleDuzenlendi || mevcutSatir.SonGun.Date == satir.SonGun) continue;

                if (mevcutSatir.Ad == satir.Ad && mevcutSatir.SonGun.Date == EskiKaydirmaliTarih(satir.SonGun))
                {
                    mevcutSatir.SonGun = satir.SonGun;   // seed'in eski kaydırmalı değeri → nominal
                    yenilenen++;
                }
                else
                {
                    mevcutSatir.ElleDuzenlendi = true;   // seed bu değeri üretmedi: kullanıcınındır, korunur
                    elle++;
                }
            }

            if (eklenen + yenilenen + elle > 0) await db.SaveChangesAsync(ct);
            return new Sonuc(eklenen, yenilenen, elle, mevcut.Count);
        }

        public static async Task SeedVeLoglaAsync(CatalogContext db, DateTime bugun, ILogger logger)
        {
            var s = await SeedAsync(db, bugun);
            if (s.Eklenen > 0)
                logger.LogWarning("Vergi takvimi: {Eklenen} satır eklendi (toplam {Toplam}). Tarihler nominal kuraldan " +
                                  "üretildi — yürürlükteki takvimle doğrulanmalı (Yönetim → Vergi Takvimi).", s.Eklenen, s.Toplam);
            if (s.Yenilenen > 0)
                logger.LogWarning("Vergi takvimi: {Yenilenen} satır hafta sonu kaydırmasından nominal tarihe çekildi " +
                                  "(kaydırma kaldırıldı — Prompt 7B).", s.Yenilenen);
            if (s.ElleSayilan > 0)
                logger.LogInformation("Vergi takvimi: {Elle} satır seed değerinden farklı; elle düzeltilmiş sayıldı ve korundu.", s.ElleSayilan);
            if (s.Eklenen + s.Yenilenen + s.ElleSayilan == 0)
                logger.LogInformation("Vergi takvimi: değişiklik yok (toplam {Toplam}).", s.Toplam);
        }
    }
}
