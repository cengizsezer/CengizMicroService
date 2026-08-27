using CatalogService.Api.Features.Muhasebe.Domain;
using CatalogService.Api.Features.Muhasebe.Services;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Muhasebe
{
    /// <summary>Tekdüzen plan yükleme denemesinin sonucu.</summary>
    public enum PlanYuklemeSonuc
    {
        /// <summary>Plan yüklendi.</summary>
        Yuklendi = 1,

        /// <summary>Bu tenant'ın planı zaten dolu; hiçbir şey yapılmadı.</summary>
        ZatenDolu = 2,

        /// <summary>Şablon dosyası sunucuda yok. Sessizce geçilmez: loglanır ve bildirilir.</summary>
        SablonYok = 3
    }

    /// <summary>
    /// Firma (tenant) başına MSUGT tekdüzen hesap planını (sınıf/grup/kebir) ve
    /// varsayılan kod maskesini yükler. Idempotent: hesap planı doluysa atlar.
    /// KodDuz/Seviye/Karakter/Yol yükleme anında hesaplanır; JSON'da yazmaz.
    ///
    /// İki çağıran var, ikisi de aynı <see cref="YukleAsync"/>'i kullanır: açılıştaki seed
    /// ve ekrandaki "Tek düzen hesap planını yükle" düğmesi (KARARLAR §84).
    /// </summary>
    public static class MuhasebeSeed
    {
        public static Task<(PlanYuklemeSonuc Sonuc, int Adet)> SeedAsync(
            CatalogContext db, IWebHostEnvironment env, CancellationToken ct = default)
            => YukleAsync(db, new DosyadanTekDuzenPlanKaynagi(env), ct);

        /// <summary>
        /// Geçerli tenant'ın hesap planını şablondan yükler. Plan doluysa dokunmaz;
        /// şablon yoksa <see cref="PlanYuklemeSonuc.SablonYok"/> döner — <b>sessizce
        /// geçmez</b>, çağıran loglar ve kullanıcıya söyler.
        /// </summary>
        public static async Task<(PlanYuklemeSonuc Sonuc, int Adet)> YukleAsync(
            CatalogContext db, ITekDuzenPlanKaynagi kaynak, CancellationToken ct = default)
        {
            await EnsureKodMaskesiAsync(db, ct);

            // Idempotent: bu tenant'ın hesap planı zaten varsa dokunma.
            if (await db.HesapPlanlari.AnyAsync(ct))
                return (PlanYuklemeSonuc.ZatenDolu, 0);

            if (!kaynak.Var)
                return (PlanYuklemeSonuc.SablonYok, 0);

            var nodes = kaynak.Oku();

            // Kısa kodlar (sınıf/grup) üst; üst olan hiçbir kod yaprak değildir.
            var kodlar = nodes.Select(n => n.Kod).ToHashSet();
            bool YaprakMi(string kod) => !kodlar.Any(k => k.Length == kod.Length + 1 && k.StartsWith(kod));

            var eklenen = new Dictionary<string, HesapPlani>(StringComparer.Ordinal);

            // Seviye seviye ekle: Yol üst hesabın (kaydedilmiş) Id'sine bağlı olduğundan
            // her seviye kaydedilip Id'ler alındıktan sonra bir alt seviyeye geçilir.
            foreach (var seviyeUzunluk in nodes.Select(n => n.Kod.Length).Distinct().OrderBy(x => x))
            {
                foreach (var n in nodes.Where(n => n.Kod.Length == seviyeUzunluk).OrderBy(n => n.Kod, StringComparer.Ordinal))
                {
                    HesapPlani? ust = null;
                    if (n.Kod.Length > 1)
                    {
                        var ustKod = n.Kod.Substring(0, n.Kod.Length - 1);
                        eklenen.TryGetValue(ustKod, out ust);
                    }

                    var seviye = (byte)n.Kod.Length;
                    var entity = new HesapPlani
                    {
                        UstHesapId = ust?.Id,
                        Kod = n.Kod,
                        KodDuz = n.Kod,                       // sınıf/grup/kebir düz rakam
                        SegmentKod = ust is null ? n.Kod : n.Kod.Substring(ust.Kod.Length),
                        Ad = n.Ad,
                        Seviye = seviye,
                        HesapTuru = seviye switch
                        {
                            1 => HesapTuru.Sinif,
                            2 => HesapTuru.Grup,
                            _ => HesapTuru.Kebir
                        },
                        Karakter = KarakterBul(n.Kod[0]),
                        HareketGorur = YaprakMi(n.Kod),
                        SistemHesabi = true,
                        Yol = ust is null ? "/" : $"{ust.Yol}{ust.Id}/",
                        Aktif = true
                    };

                    db.HesapPlanlari.Add(entity);
                    eklenen[n.Kod] = entity;
                }

                await db.SaveChangesAsync(ct);   // bu seviyenin Id'leri alt seviye Yol'u için gerekli
            }

            return (PlanYuklemeSonuc.Yuklendi, eklenen.Count);
        }

        private static async Task EnsureKodMaskesiAsync(CatalogContext db, CancellationToken ct)
        {
            if (await db.KodMaskeleri.AnyAsync(ct))
                return;

            db.KodMaskeleri.Add(new KodMaskesi { SegmentUzunluk = "3,2,2,4", Ayrac = "." });
            await db.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Kök sınıf rakamından bakiye karakteri: 1–2 Aktif, 3–5 Pasif,
        /// 6 Gelir, 7 Gider, 8 Maliyet, 9 Nazım.
        /// </summary>
        private static HesapKarakter KarakterBul(char kokRakam) => kokRakam switch
        {
            '1' or '2' => HesapKarakter.Aktif,
            '3' or '4' or '5' => HesapKarakter.Pasif,
            '6' => HesapKarakter.Gelir,
            '7' => HesapKarakter.Gider,
            '8' => HesapKarakter.Maliyet,
            '9' => HesapKarakter.Nazim,
            _ => HesapKarakter.Aktif
        };
    }
}
