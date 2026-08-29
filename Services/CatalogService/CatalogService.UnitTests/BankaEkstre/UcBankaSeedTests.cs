using System.Text.RegularExpressions;
using CatalogService.Api.Features.BankaEkstre;
using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Yeni banka eklemenin sözleşmesi: bir <see cref="IEkstreParser"/> sınıfı, bir DI
    /// kaydı ve seed'e yapılandırma satırları. Bu testler o sözleşmeyi sabitler —
    /// ayrıştırıcısı olup şablonu/deseni olmayan bir banka, satırları okuyup hiçbirine
    /// açıklama üretemez.
    /// </summary>
    public class UcBankaSeedTests
    {
        /// <summary>Üretimdeki DI kayıtlarının aynısı (Program.cs).</summary>
        private static EkstreParserSecici Secici() => new(new IEkstreParser[]
        {
            new VakifbankVadesizParser(),
            new IsBankasiVadesizParser(),
            new AkbankVadesizParser(),
            new ZiraatVadesizParser()
        });

        private static async Task<CatalogContext> SeedliContextAsync()
        {
            var db = BankaEkstreTestOrtami.YeniContext();
            await BankaEkstreSeed.SeedAsync(db);
            return db;
        }

        [Fact]
        public void Secici_dort_ayristiriciyi_de_toplar()
        {
            var secici = Secici();

            Assert.Equal(4, secici.Hepsi.Count);
            Assert.NotNull(secici.Sec(VakifbankVadesizParser.Tip));
            Assert.NotNull(secici.Sec(IsBankasiVadesizParser.Tip));
            Assert.NotNull(secici.Sec(AkbankVadesizParser.Tip));
            Assert.NotNull(secici.Sec(ZiraatVadesizParser.Tip));
        }

        [Fact]
        public async Task Her_ayristirici_icin_sablon_desen_ve_kural_tohumlanir()
        {
            using var db = await SeedliContextAsync();

            foreach (var parser in Secici().Hepsi)
            {
                Assert.True(db.EkstreAciklamaSablonlari.Any(s => s.ParserTipi == parser.ParserTipi),
                    $"{parser.Ad}: açıklama şablonu tohumlanmamış.");
                Assert.True(db.EkstreUnvanDesenleri.Any(d => d.ParserTipi == parser.ParserTipi),
                    $"{parser.Ad}: unvan deseni tohumlanmamış.");
                Assert.True(db.EkstreSabitKurallar.Any(k => k.ParserTipi == parser.ParserTipi),
                    $"{parser.Ad}: sabit kural tohumlanmamış.");
            }
        }

        [Fact]
        public async Task Vakifbank_satirlari_degismedi()
        {
            // Üç yeni banka eklenirken mevcut bankanın yapılandırması aynen kalmalı.
            using var db = await SeedliContextAsync();

            var sablonlar = db.EkstreAciklamaSablonlari.Where(s => s.ParserTipi == VakifbankVadesizParser.Tip).ToList();
            var desenler = db.EkstreUnvanDesenleri.Where(d => d.ParserTipi == VakifbankVadesizParser.Tip).ToList();
            var kurallar = db.EkstreSabitKurallar.Where(k => k.ParserTipi == VakifbankVadesizParser.Tip).ToList();

            Assert.Equal(17, sablonlar.Count);
            Assert.Equal(9, desenler.Count);
            Assert.Equal(13, kurallar.Count);

            // Sıra numaraları da korunmalı: dar avans kuralları genelden önce denenir.
            var genelAvans = kurallar.Single(k => k.IslemTipiDeseni == "Avans");
            Assert.Equal("195, 196", genelAvans.AnaGruplar);
            Assert.True(kurallar.Single(k => k.IslemTipiDeseni == "Maaş Avansı").Sira < genelAvans.Sira);
        }

        [Fact]
        public async Task Yeni_kategoriler_tohumlanir()
        {
            using var db = await SeedliContextAsync();

            Assert.Contains(db.EkstreIslemKategorileri, k => k.Ad == "Menkul kıymet" && k.VarsayilanAnaGrup == "118");
            Assert.Contains(db.EkstreIslemKategorileri, k => k.Ad == "Alınan çekler" && k.VarsayilanAnaGrup == "101");
            Assert.Contains(db.EkstreIslemKategorileri, k => k.Ad == "Finansman gideri" && k.VarsayilanAnaGrup == "780");
            Assert.Contains(db.EkstreIslemKategorileri, k => k.Ad == "SGK" && k.VarsayilanAnaGrup == "361");
        }

        [Fact]
        public async Task Seed_iki_kez_calisinca_satirlar_tekrarlanmaz()
        {
            using var db = await SeedliContextAsync();

            var sablon = db.EkstreAciklamaSablonlari.Count();
            var desen = db.EkstreUnvanDesenleri.Count();
            var kural = db.EkstreSabitKurallar.Count();

            await BankaEkstreSeed.SeedAsync(db);

            Assert.Equal(sablon, db.EkstreAciklamaSablonlari.Count());
            Assert.Equal(desen, db.EkstreUnvanDesenleri.Count());
            Assert.Equal(kural, db.EkstreSabitKurallar.Count());
        }

        [Fact]
        public async Task Tohumlanan_sablonlarda_tanimsiz_yer_tutucu_yok()
        {
            // {YON} ve {KREDI} yer tutucuları bu görevle eklendi; listeye yazılmasalardı
            // şablon metni ORKA'ya "{YON} Eft - …" diye giderdi.
            using var db = await SeedliContextAsync();

            var bilinen = AciklamaUretici.YerTutucular.Select(y => y.Ad).ToHashSet(StringComparer.Ordinal);
            var desen = new Regex(@"\{[^}]*\}", RegexOptions.CultureInvariant);

            foreach (var sablon in db.EkstreAciklamaSablonlari.ToList())
            {
                var tanimsiz = desen.Matches(sablon.Sablon).Select(m => m.Value).Where(y => !bilinen.Contains(y));
                Assert.Empty(tanimsiz);
            }
        }

        [Fact]
        public async Task Tohumlanan_desenler_derlenebiliyor()
        {
            // Bozuk bir desen sessizce atlanıyor (UnvanCikarici derleyemediğini geçer);
            // seed'e yazarken fark edilmezse banka hiç unvan çıkaramaz.
            using var db = await SeedliContextAsync();

            foreach (var desen in db.EkstreUnvanDesenleri.ToList())
            {
                var hata = Record.Exception(() => new Regex(desen.Desen, RegexOptions.CultureInvariant));
                Assert.Null(hata);
            }

            foreach (var kural in db.EkstreSabitKurallar.Where(k => k.EslesmeTuru == EslesmeTuru.Regex).ToList())
            {
                var hata = Record.Exception(() => new Regex(kural.IslemTipiDeseni, RegexOptions.CultureInvariant));
                Assert.Null(hata);
            }
        }

        [Fact]
        public async Task Is_bankasi_kredi_kurali_anapara_bacagini_yakalar()
        {
            using var db = await SeedliContextAsync();

            var kurallar = db.EkstreSabitKurallar
                .Where(k => k.ParserTipi == IsBankasiVadesizParser.Tip && k.Kapsam == KuralKapsami.Aciklama)
                .OrderBy(k => k.Sira)
                .ToList();

            var eslestirici = new HesapEslestirici();
            var veri = new EslestirmeVerisi { SabitKurallar = kurallar };

            var anapara = eslestirici.AciklamaKuraliBul(
                new SatirBaglami { HamAciklama = "KREDİ NO: 10080844268 ANAPARA TAHSİLAT", Yon = Yon.Cikan }, veri);

            var faiz = eslestirici.AciklamaKuraliBul(
                new SatirBaglami { HamAciklama = "KREDİ NO: 10080844268 /ERKN.ODEM FAİZ", Yon = Yon.Cikan }, veri);

            // Anapara krediyi kapatır, faiz bacağı finansman gideridir; iki koşullu kural
            // dar olduğu için sırada önce denenmeli.
            Assert.Equal("300", anapara?.HesapKodu);
            Assert.Equal("780", faiz?.HesapKodu);
        }
    }
}
