using WebApp.Shared.Dto.FirmaPortfoyu;

namespace WebApp.Pages.FirmaPortfoyu
{
    // wwwroot/js/firmaPortfoyu.js'in girdileri (Blazor JS interop'u camelCase yazar). Rakam HESAPLANMAZ: hepsi
    // sunucunun yanıtından (PortfoyFirmasiDto.Seri ve Gostergeler) aynen taşınır; burada yalnız eşleme ve sıralama.

    public sealed record PfAyEtiketi(string Kisa, string Uzun);

    public sealed record PfCizgiSerisi(string Ad, string Renk, List<decimal?> Degerler);

    public sealed record PfCizgiVerisi(string Tur, List<PfAyEtiketi> Aylar, List<PfCizgiSerisi> Seriler,
                                       List<string?> Notlar, int? Secili, int Yukseklik, string Aria);

    public sealed record PfCubukSatiri(int FirmaId, string Ad, decimal Deger);

    public sealed record PfCubukVerisi(string Tur, List<PfCubukSatiri> Satirlar, int? SeciliFirmaId, string Aria);

    /// <summary>Düşen/yükselen listesinin satırı: seçili ayın ve karşılaştırma döneminin AYLIK net satışı.</summary>
    public sealed record PfHareket(PortfoyFirmasiDto Firma, decimal Bu, decimal Onceki, decimal Oran);

    public static class PfGrafikVerisi
    {
        // Taslaktaki seri renkleri (--s1, --s2, --s3).
        public const string RenkNetSatis = "#2a78d6";
        public const string RenkSmm = "#eb6834";
        public const string RenkFaaliyetGideri = "#1baf7a";

        /// <summary>
        /// Firmanın 12 aylık çizgisi: aylık net satış, SMM, faaliyet gideri — sunucunun Seri[12]'si. Değeri olmayan ay
        /// null kalır (JS çizgide boşluk bırakır, sıfır çizmez); ipucunda nedeni (Not) gösterilir.
        /// </summary>
        public static PfCizgiVerisi Cizgi(PortfoyFirmasiDto f, int yukseklik = 260)
        {
            var seri = f.Seri;
            return new PfCizgiVerisi(
                "cizgi",
                seri.Select(a => new PfAyEtiketi($"{PfAy.Kisa(a.Ay)} {a.Yil % 100:D2}", $"{PfAy.Uzun(a.Ay)} {a.Yil}")).ToList(),
                new List<PfCizgiSerisi>
                {
                    new("Net satış", RenkNetSatis, seri.Select(a => a.NetSatis).ToList()),
                    new("SMM", RenkSmm, seri.Select(a => a.Smm).ToList()),
                    new("Faaliyet gideri", RenkFaaliyetGideri, seri.Select(a => a.FaaliyetGiderleri).ToList())
                },
                seri.Select(a => a.Not).ToList(),
                seri.Count > 0 ? seri.Count - 1 : null,
                yukseklik,
                $"{f.KisaAd} aylık net satış, satışların maliyeti ve faaliyet gideri, son 12 ay");
        }

        /// <summary>
        /// Firma bazında seçili ayın AYLIK net satışı, büyükten küçüğe. Değeri olmayan firma (mizan yok, önceki ay eksik)
        /// listeye girmez. Toplam çubuğu yok — firmalar yan yana, toplanmaz.
        /// </summary>
        public static PfCubukVerisi Cubuk(PortfoyDto p, int? seciliFirmaId)
            => new(
                "cubuk",
                p.Firmalar
                    .Where(f => f.Gostergeler.NetSatis.Deger is not null)
                    .Select(f => new PfCubukSatiri(f.FirmaId, f.KisaAd, f.Gostergeler.NetSatis.Deger!.Value))
                    .OrderByDescending(r => r.Deger)
                    .ToList(),
                seciliFirmaId,
                $"Firma bazında aylık net satış, {PfBicim.DonemAdi(p.Donem)}");

        /// <summary>Çubukta yer almayan (aylık net satışı olmayan) firma sayısı.</summary>
        public static int CubuktaOlmayan(PortfoyDto p) => p.Firmalar.Count(f => f.Gostergeler.NetSatis.Deger is null);

        /// <summary>
        /// Net satışta en çok düşen ve yükselen ilk <paramref name="adet"/> firma (değişim oranına göre). Karşılaştırması
        /// olmayan ya da karşılaştırması sıfır olan firma listeye girmez; değişimsiz firma iki listede de yok.
        /// </summary>
        public static (List<PfHareket> Dusenler, List<PfHareket> Yukselenler) Hareketler(PortfoyDto p, int adet = 3)
        {
            var liste = p.Firmalar
                .Where(f => f.Gostergeler.NetSatis is { Deger: not null, Karsilastirma: not null and not 0m })
                .Select(f =>
                {
                    var bu = f.Gostergeler.NetSatis.Deger!.Value;
                    var once = f.Gostergeler.NetSatis.Karsilastirma!.Value;
                    return new PfHareket(f, bu, once, (bu - once) / Math.Abs(once));
                })
                .ToList();

            return (
                liste.Where(h => h.Oran < 0m).OrderBy(h => h.Oran).Take(adet).ToList(),
                liste.Where(h => h.Oran > 0m).OrderByDescending(h => h.Oran).Take(adet).ToList());
        }
    }
}
