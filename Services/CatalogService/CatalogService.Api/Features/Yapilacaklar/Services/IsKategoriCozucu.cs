using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Yapilacaklar.Services
{
    /// <summary>Bir işin kart bölümü. <see cref="KategoriId"/> boş = "Diğer".</summary>
    public sealed record KategoriBolumu(string Anahtar, string Ad, int Sira, int? KategoriId);

    /// <summary>
    /// Kategori çözümünün girdileri — bir kez okunur, satır satır sorgu atılmaz. Saf: veritabanını yalnız
    /// <see cref="YukleAsync"/> okur; testler sözlüklerle kurar.
    /// </summary>
    public sealed class IsKategoriBaglami
    {
        /// <param name="kategoriler">Bütün kategoriler, pasifler dahil (pasif çözümde atlanır).</param>
        /// <param name="mukellefiyetler">Mükellefiyet kodu → kategori Id.</param>
        /// <param name="ozelIsler">Özel iş (FirmaIsi) Id → kendi kategorisi ve tarifinin kategorisi.</param>
        public IsKategoriBaglami(IEnumerable<IsKategorisi> kategoriler,
                                 IReadOnlyDictionary<string, int> mukellefiyetler,
                                 IReadOnlyDictionary<int, (int? Kendi, int? Tarif)> ozelIsler)
        {
            Kategoriler = kategoriler.ToDictionary(k => k.Id);
            Mukellefiyetler = mukellefiyetler;
            OzelIsler = ozelIsler;
            // Ad değil kolon: "Beyanname"nin adı değişse de yasal işler yerinde kalır.
            YasalVarsayilan = Kategoriler.Values.Where(k => k.Aktif && k.VarsayilanYasal).OrderBy(k => k.Id).FirstOrDefault();
        }

        public IReadOnlyDictionary<int, IsKategorisi> Kategoriler { get; }
        public IReadOnlyDictionary<string, int> Mukellefiyetler { get; }
        public IReadOnlyDictionary<int, (int? Kendi, int? Tarif)> OzelIsler { get; }

        /// <summary>
        /// Yasal işin varsayılanı (<see cref="IsKategorisi.VarsayilanYasal"/>). Yoksa ya da pasifse yasal iş "Diğer"e
        /// düşer — servis buna izin vermez, çözüm yine de güvenli.
        /// </summary>
        public IsKategorisi? YasalVarsayilan { get; }

        public static IsKategoriBaglami Bos { get; } =
            new(Array.Empty<IsKategorisi>(), new Dictionary<string, int>(), new Dictionary<int, (int?, int?)>());

        public static async Task<IsKategoriBaglami> YukleAsync(CatalogContext db, CancellationToken ct)
        {
            var kategoriler = await db.IsKategorileri.AsNoTracking().ToListAsync(ct);
            var mukellefiyetler = await db.MukellefiyetKategorileri.AsNoTracking()
                .ToDictionaryAsync(m => m.MukellefiyetKodu, m => m.KategoriId, ct);
            var tarifler = await db.IsTarifleri.AsNoTracking().Where(t => t.KategoriId != null)
                .ToDictionaryAsync(t => t.Id, t => t.KategoriId, ct);
            var isler = await db.FirmaIsleri.AsNoTracking().Where(i => i.YasalMukellefiyetKodu == null)
                .Select(i => new { i.Id, i.KategoriId, i.IsTarifiId })
                .ToListAsync(ct);

            return new IsKategoriBaglami(kategoriler, mukellefiyetler,
                isler.ToDictionary(i => i.Id,
                                   i => (i.KategoriId, i.IsTarifiId is { } t ? tarifler.GetValueOrDefault(t) : null)));
        }
    }

    /// <summary>
    /// İşin kategorisi — TEK YER (Firma Panosu Aşama 2). Kart bölümleri, kategorisiz iş listesi ve
    /// testler buradan okur.
    /// <list type="bullet">
    /// <item>Özel iş: <c>FirmaIsi.KategoriId ?? IsTarifi.KategoriId ?? Diğer</c>.</item>
    /// <item>Yasal iş: <c>MukellefiyetKategorisi ?? yasal varsayılan</c> (<see cref="IsKategorisi.VarsayilanYasal"/>, seed'de Beyanname).</item>
    /// <item>Pasif kategori yokmuş gibi atlanır, zincirde sonrakine geçilir (varsayılan da yoksa "Diğer").</item>
    /// <item>"Diğer" tabloda kayıt değil; sabit son bölüm.</item>
    /// </list>
    /// </summary>
    public static class IsKategoriCozucu
    {
        public const string DigerAnahtari = "diger";
        public const string DigerAdi = "Diğer";

        public static KategoriBolumu Diger { get; } = new(DigerAnahtari, DigerAdi, int.MaxValue, null);

        public static KategoriBolumu Coz(IsSatiriDto s, IsKategoriBaglami b)
            => Coz(s.KaynakTip, s.KaynakId, s.MukellefiyetKodu, b);

        public static KategoriBolumu Coz(IsKaynagi tip, int kaynakId, string? mukellefiyetKodu, IsKategoriBaglami b)
        {
            if (tip == IsKaynagi.Yasal)
            {
                var eslenen = mukellefiyetKodu is not null && b.Mukellefiyetler.TryGetValue(mukellefiyetKodu, out var id) ? Aktif(id, b) : null;
                return Bolum(eslenen ?? b.YasalVarsayilan);
            }

            var (kendi, tarif) = b.OzelIsler.GetValueOrDefault(kaynakId);
            return Bolum(Aktif(kendi, b) ?? Aktif(tarif, b));
        }

        /// <summary>Özel işin çözülen kategorisi yoksa (= "Diğer") true — kategorisiz iş listesi.</summary>
        public static bool OzelKategorisiz(int? kendi, int? tarif, IsKategoriBaglami b)
            => Aktif(kendi, b) is null && Aktif(tarif, b) is null;

        private static IsKategorisi? Aktif(int? id, IsKategoriBaglami b)
            => id is { } i && b.Kategoriler.TryGetValue(i, out var k) && k.Aktif ? k : null;

        private static KategoriBolumu Bolum(IsKategorisi? k)
            => k is null ? Diger : new KategoriBolumu($"k{k.Id}", k.Ad, k.Sira, k.Id);
    }
}
