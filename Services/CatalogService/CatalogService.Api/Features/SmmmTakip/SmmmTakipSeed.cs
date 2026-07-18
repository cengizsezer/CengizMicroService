using CatalogService.Api.Features.SmmmTakip.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.SmmmTakip
{
    /// <summary>
    /// SMMM Takip için başlangıç içeriği. Satır bazında idempotenttir:
    /// var olan konu (Slug), had (Kod) veya had değeri (Kod+Yıl) yeniden eklenmez.
    /// İçerik ortak referans olduğundan tenant'tan bağımsız tek sefer çalışır.
    /// </summary>
    public static class SmmmTakipSeed
    {
        public static async Task SeedAsync(CatalogContext db, CancellationToken ct = default)
        {
            // --- Konu ağacı: SMMM Takip > Gelir Unsurları > (alt konular) ---
            var kok = await EnsureKonuAsync(db, "smmm-takip", "SMMM Takip", null, 1, null, ct);
            var gelir = await EnsureKonuAsync(db, "gelir-unsurlari", "Gelir Unsurları", kok.Id, 1, null, ct);

            await EnsureKonuAsync(db, "ticari-kazanc", "Ticari Kazanç", gelir.Id, 1, TicariKazancIcerik, ct);
            await EnsureKonuAsync(db, "zirai-kazanc", "Zirai Kazanç", gelir.Id, 2, null, ct);
            var basitUsul = await EnsureKonuAsync(db, "basit-usul", "Basit Usul", gelir.Id, 3, BasitUsulIcerik, ct);
            await EnsureKonuAsync(db, "serbest-meslek", "Serbest Meslek", gelir.Id, 4, null, ct);
            await EnsureKonuAsync(db, "ucret", "Ücret", gelir.Id, 5, null, ct);

            // --- Basit Usul hadleri ---
            const string teblig = "332 Seri No'lu GVK GT";

            await EnsureHadAsync(db, basitUsul.Id, "BU_KIRA_BUYUKSEHIR", "İşyeri kira – Büyükşehir", "GVK 47/2", 1,
                new[] { (2025, 79000m), (2026, 99000m) }, teblig, ct);
            await EnsureHadAsync(db, basitUsul.Id, "BU_KIRA_DIGER", "İşyeri kira – Diğer", "GVK 47/2", 2,
                new[] { (2025, 48000m), (2026, 60000m) }, teblig, ct);
            await EnsureHadAsync(db, basitUsul.Id, "BU_ALIS", "Alım-satım – Alış", "GVK 48/1", 3,
                new[] { (2026, 1200000m) }, teblig, ct);
            await EnsureHadAsync(db, basitUsul.Id, "BU_SATIS", "Alım-satım – Satış", "GVK 48/1", 4,
                new[] { (2026, 1900000m) }, teblig, ct);
            await EnsureHadAsync(db, basitUsul.Id, "BU_HIZMET", "Hizmet hasılatı", "GVK 48/2", 5,
                new[] { (2026, 600000m) }, teblig, ct);
        }

        private static async Task<SmmmKonu> EnsureKonuAsync(
            CatalogContext db, string slug, string baslik, int? ustId, int sira, string? icerik, CancellationToken ct)
        {
            var konu = await db.SmmmKonular.FirstOrDefaultAsync(k => k.Slug == slug, ct);
            if (konu is not null) return konu;

            konu = new SmmmKonu
            {
                Slug = slug,
                Baslik = baslik,
                UstKonuId = ustId,
                Sira = sira,
                Aktif = true,
                IcerikMd = icerik,
                GuncellenmeTarihi = DateTime.UtcNow
            };
            db.SmmmKonular.Add(konu);
            await db.SaveChangesAsync(ct);
            return konu;
        }

        private static async Task EnsureHadAsync(
            CatalogContext db, int konuId, string kod, string ad, string? dayanak, int sira,
            (int Yil, decimal Deger)[] degerler, string teblig, CancellationToken ct)
        {
            var had = await db.SmmmHadler.FirstOrDefaultAsync(h => h.Kod == kod, ct);
            if (had is null)
            {
                had = new SmmmHad
                {
                    KonuId = konuId,
                    Kod = kod,
                    Ad = ad,
                    Birim = "TL",
                    Dayanak = dayanak,
                    Sira = sira
                };
                db.SmmmHadler.Add(had);
                await db.SaveChangesAsync(ct);
            }

            foreach (var (yil, deger) in degerler)
            {
                var exists = await db.SmmmHadDegerleri.AnyAsync(d => d.HadId == had.Id && d.Yil == yil, ct);
                if (exists) continue;

                db.SmmmHadDegerleri.Add(new SmmmHadDegeri
                {
                    HadId = had.Id,
                    Yil = yil,
                    Deger = deger,
                    Teblig = teblig,
                    GuncellenmeTarihi = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync(ct);
        }

        // [[dal]] node-şeması örneği — kök kutudan dallara yatay ezber şeması çizer.
        private const string TicariKazancIcerik =
@"### Ticari Kazancın Tespit Usulleri

Ticari kazanç iki usulden biriyle tespit edilir; gerçek usul de kendi içinde ikiye ayrılır.

[[dal]] Ticari Kazanç
- Basit Usul
- Gerçek Usul

[[dal]] Gerçek Usul
- Bilanço Esası
- İşletme Hesabı Esası

**Not:** Basit usul şartları ihlal edilirse takip eden takvim yılından itibaren gerçek usule geçilir.";

        // Tablosuz markdown — hand-rolled RenderMarkdown helper'ı tablo desteklemez.
        private const string BasitUsulIcerik =
@"### Genel şartlar (Md. 47)
1. Kendi işinde bilfiil çalışmak veya bulunmak
2. İşyeri kira haddi kanuni haddi aşmamak
3. Gerçek usulde gelir vergisi mükellefi olmamak

### Özel şartlar (Md. 48)
- 48/1 — Alım-satım: alış ve satış hadleri
- 48/2 — Hizmet: gayrisafi hasılat haddi
- 48/3 — İkisi birlikte yapılıyorsa

**Not:** Md. 47 ve 48 birlikte sağlanmalı. Biri ihlal → takip eden takvim yılı başından itibaren gerçek usul.";
    }
}
