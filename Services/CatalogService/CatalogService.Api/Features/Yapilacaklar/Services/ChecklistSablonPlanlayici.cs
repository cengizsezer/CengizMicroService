using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;

namespace CatalogService.Api.Features.Yapilacaklar.Services
{
    /// <summary>
    /// Şablondan iş oluşturmanın planı (Firma Panosu Aşama 3) — saf, veritabanı yok. ÖNİZLEME ve GERÇEK YAZMA bu
    /// fonksiyonu çağırır (<see cref="ChecklistSablonService"/>); ikisi ayrı kural taşımaz.
    ///
    /// <list type="bullet">
    /// <item>Atlama: firmada aynı başlık + tekrar varsa (<see cref="DonemPanosuKurucu.OzelKolonAnahtari"/>, pasif işler
    /// dahil) madde ATLANIR. Israr seçeneği yok — şablon bir firmada aynı kolona ikinci iş açmaz.</item>
    /// <item>Aynı istekte eklenen madde de sayılır (şablon kaydı bunu zaten engeller; eski veriye karşı güvenlik).</item>
    /// <item>Pasif kategori taşınmaz: işin kategorisi boş kalır (tarifin kategorisi ya da "Diğer" çözülür).</item>
    /// </list>
    /// </summary>
    public static class ChecklistSablonPlanlayici
    {
        public const string AyniIsSebebi = "bu firmada aynı başlık ve tekrarla iş var";

        /// <param name="MevcutAnahtarlar">Firmanın özel işlerinin (prosedür satırı hariç) kolon anahtarları.</param>
        public sealed record FirmaGirdisi(int FirmaId, string FirmaAdi, IReadOnlySet<string> MevcutAnahtarlar);

        /// <summary>Bir firmaya yazılacak madde ve işin alacağı kategori (pasifse null).</summary>
        public sealed record Eklenecek(ChecklistSablonMaddesi Madde, int? KategoriId);

        public sealed record FirmaPlani(int FirmaId, List<Eklenecek> Eklenecekler, SablonFirmaSonucuDto Sonuc);

        public sealed record Plan(List<FirmaPlani> Firmalar, SablondanSonucDto Sonuc);

        /// <param name="maddeler">Şablonun maddeleri (sıra burada belirlenir: Sira, Id).</param>
        /// <param name="kategoriler">Bütün kategoriler, pasifler dahil.</param>
        /// <param name="tarifKategorileri">Tarif Id → tarifin kategorisi.</param>
        public static Plan Planla(ChecklistSablonu sablon, IEnumerable<ChecklistSablonMaddesi> maddeler,
                                  IEnumerable<FirmaGirdisi> firmalar,
                                  IReadOnlyDictionary<int, IsKategorisi> kategoriler,
                                  IReadOnlyDictionary<int, int?> tarifKategorileri,
                                  string? ilkDonem)
        {
            var sirali = maddeler.OrderBy(m => m.Sira).ThenBy(m => m.Id).ToList();

            var sonuc = new SablondanSonucDto
            {
                SablonId = sablon.Id,
                SablonAdi = sablon.Ad,
                IlkDonem = ilkDonem,
                Kategoriler = KategoriSayilari(sirali, kategoriler, tarifKategorileri)
            };

            var planlar = new List<FirmaPlani>();
            foreach (var f in firmalar)
            {
                var anahtarlar = new HashSet<string>(f.MevcutAnahtarlar, StringComparer.Ordinal);
                var firmaSonucu = new SablonFirmaSonucuDto { FirmaId = f.FirmaId, FirmaAdi = f.FirmaAdi };
                var eklenecekler = new List<Eklenecek>();

                foreach (var m in sirali)
                {
                    if (!anahtarlar.Add(DonemPanosuKurucu.OzelKolonAnahtari(m.Baslik, m.Tekrar)))
                    {
                        firmaSonucu.Atlananlar.Add(new SablonAtlananDto { Baslik = m.Baslik, Tekrar = m.Tekrar, Sebep = AyniIsSebebi });
                        continue;
                    }

                    eklenecekler.Add(new Eklenecek(m, AktifKategori(m.KategoriId, kategoriler)?.Id));
                    firmaSonucu.Eklenenler.Add(m.Baslik);
                }

                planlar.Add(new FirmaPlani(f.FirmaId, eklenecekler, firmaSonucu));
                sonuc.Firmalar.Add(firmaSonucu);
            }

            return new Plan(planlar, sonuc);
        }

        /// <summary>
        /// Maddelerin ÇÖZÜLEN kategorisine göre sayısı — oluşacak işin kartta düşeceği bölüm (IsKategoriCozucu'nun
        /// özel iş kuralı: kendi ?? tarif ?? Diğer, pasif atlanır). Sıra kategorinin Sira'sı, "Diğer" en sonda.
        /// </summary>
        public static List<SablonKategoriSayisiDto> KategoriSayilari(IEnumerable<ChecklistSablonMaddesi> maddeler,
                                                                     IReadOnlyDictionary<int, IsKategorisi> kategoriler,
                                                                     IReadOnlyDictionary<int, int?> tarifKategorileri)
            => maddeler
                .Select(m => AktifKategori(m.KategoriId, kategoriler)
                             ?? AktifKategori(m.IsTarifiId is { } t ? tarifKategorileri.GetValueOrDefault(t) : null, kategoriler))
                .GroupBy(k => k?.Id)
                .OrderBy(g => g.First()?.Sira ?? int.MaxValue)
                .ThenBy(g => g.Key)
                .Select(g => new SablonKategoriSayisiDto { Ad = g.First()?.Ad ?? IsKategoriCozucu.DigerAdi, Sayi = g.Count() })
                .ToList();

        private static IsKategorisi? AktifKategori(int? id, IReadOnlyDictionary<int, IsKategorisi> kategoriler)
            => id is { } i && kategoriler.TryGetValue(i, out var k) && k.Aktif ? k : null;
    }
}
