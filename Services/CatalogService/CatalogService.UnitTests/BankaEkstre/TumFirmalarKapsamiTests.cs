using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Kapsam;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// "Tüm firmalar" kapsamı (KARARLAR §99).
    ///
    /// Firma artık bir oturum bağlamı değil verinin bir boyutu: listeler bütün firmaların
    /// kayıtlarını firma kolonuyla gösterebiliyor. Bu testlerin koruduğu şey, o esnekliğin
    /// <b>izolasyonu bozmaması</b>:
    ///
    /// <list type="number">
    /// <item>Kapsam tek firmaysa sorgu yalnız o firmayı görür — eski davranış aynen duruyor.</item>
    /// <item>Kapsam belirtilmemişse bütün firmalar gelir ve her satır kendi firmasını taşır;
    /// kayıtlar birbirine karışmaz, yalnız birlikte listelenir.</item>
    /// <item>Yazma tarafı etkilenmez: kapsamsız yazma <c>SaveChangesAsync</c>'te reddedilir.</item>
    /// </list>
    /// </summary>
    public class TumFirmalarKapsamiTests
    {
        private const int Aday = 201;
        private const int Smmm = 106;

        private static IBankaFirmaKapsami TumFirmalar => new SabitBankaFirmaKapsami(0);

        /// <summary>İki firmanın hesaplarını taşıyan tek veritabanı — üretimdeki durum.</summary>
        private static CatalogContext IkiFirmaliContext()
        {
            var db = BankaEkstreTestOrtami.YeniContext();

            db.Firmalar.AddRange(
                new Firma { Id = Aday, Unvan = "PKF ADAY BAĞIMSIZ DENETİM A.Ş.", KisaAd = "PKF ADAY" },
                new Firma { Id = Smmm, Unvan = string.Empty, KisaAd = "PKF SMMM" });

            db.EkstreBankaHesaplari.AddRange(
                new BankaHesabi { FirmaId = Aday, BankaAdi = "VAKIFBANK", OrkaHesapKodu = "102 01", Aktif = true },
                new BankaHesabi { FirmaId = Aday, BankaAdi = "İŞ BANKASI", OrkaHesapKodu = "102 02", Aktif = true },
                new BankaHesabi { FirmaId = Smmm, BankaAdi = "GARANTİ", OrkaHesapKodu = "102 03", Aktif = true });

            db.SaveChanges();
            return db;
        }

        private static BankaHesabiService Servis(CatalogContext db, IBankaFirmaKapsami kapsam)
            => new(db,
                   new EkstreParserSecici(new IEkstreParser[] { new VakifbankVadesizParser() }),
                   kapsam);

        [Fact]
        public async Task Tek_firma_kapsaminda_yalniz_o_firmanin_hesaplari_gelir()
        {
            using var db = IkiFirmaliContext();

            var aday = await Servis(db, BankaEkstreTestOrtami.Kapsam(Aday)).GetHepsiAsync(pasifDahil: true);
            var smmm = await Servis(db, BankaEkstreTestOrtami.Kapsam(Smmm)).GetHepsiAsync(pasifDahil: true);

            Assert.Equal(2, aday.Count);
            Assert.All(aday, h => Assert.Equal(Aday, h.FirmaId));

            Assert.Single(smmm);
            Assert.All(smmm, h => Assert.Equal(Smmm, h.FirmaId));
        }

        [Fact]
        public async Task Kapsamsiz_okumada_butun_firmalarin_hesaplari_gelir()
        {
            using var db = IkiFirmaliContext();

            var hepsi = await Servis(db, TumFirmalar).GetHepsiAsync(pasifDahil: true);

            Assert.Equal(3, hepsi.Count);
            Assert.Contains(hepsi, h => h.FirmaId == Aday);
            Assert.Contains(hepsi, h => h.FirmaId == Smmm);
        }

        /// <summary>
        /// Kayıtlar birlikte listelenirken kendi firmalarını taşır. Bu olmadan çok firmalı
        /// liste okunamaz: kullanıcı hangi satırın hangi firmaya ait olduğunu göremez ve
        /// yanlış satır üzerinden işlem yapar.
        /// </summary>
        [Fact]
        public async Task Her_satir_kendi_firmasini_tasir()
        {
            using var db = IkiFirmaliContext();

            var hepsi = await Servis(db, TumFirmalar).GetHepsiAsync(pasifDahil: true);

            var garanti = Assert.Single(hepsi.Where(h => h.BankaAdi == "GARANTİ"));
            Assert.Equal(Smmm, garanti.FirmaId);

            var vakif = Assert.Single(hepsi.Where(h => h.BankaAdi == "VAKIFBANK"));
            Assert.Equal(Aday, vakif.FirmaId);
        }

        /// <summary>
        /// Firma adı ayrı bir tablodan geliyor; unvan boşsa kısa ada düşer. Aynı kural
        /// istemcideki <c>FirmaSecenekleri</c>'nde de var — iki yerde farklı ad gösterilirse
        /// kullanıcı aynı firmayı iki firma sanır.
        /// </summary>
        [Fact]
        public async Task Firma_adi_unvandan_yoksa_kisa_addan_cozulur()
        {
            using var db = IkiFirmaliContext();

            var adlar = await new FirmaAdlari(db).HepsiAsync();

            Assert.Equal("PKF ADAY BAĞIMSIZ DENETİM A.Ş.", adlar[Aday]);
            Assert.Equal("PKF SMMM", adlar[Smmm]);
        }

        /// <summary>
        /// Okuma tarafındaki gevşeme yazma tarafına sızmamalı: kapsamsız bir kayıt
        /// veritabanına ulaşamaz. Bu, uç noktadaki 400'den bağımsız ikinci savunma.
        /// </summary>
        [Fact]
        public async Task Kapsamsiz_yazma_hala_reddedilir()
        {
            using var db = IkiFirmaliContext();

            db.EkstreBankaHesaplari.Add(new BankaHesabi
            {
                FirmaId = 0,
                BankaAdi = "KAPSAMSIZ",
                OrkaHesapKodu = "102 99",
                Aktif = true
            });

            var hata = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            Assert.Contains("FirmaId", hata.Message);
        }

        /// <summary>
        /// Silme tek firmanın kaydına iner. "Tüm firmalar" görünümünde bile silinen satır
        /// kendi firmasının kaydıdır; komşu firmanın verisi durur.
        /// </summary>
        [Fact]
        public async Task Silme_yalniz_kendi_firmasinin_kaydini_dusurur()
        {
            using var db = IkiFirmaliContext();

            var garanti = await db.EkstreBankaHesaplari.SingleAsync(h => h.BankaAdi == "GARANTİ");

            Assert.True(await Servis(db, BankaEkstreTestOrtami.Kapsam(Smmm)).DeleteAsync(garanti.Id));

            var kalan = await Servis(db, TumFirmalar).GetHepsiAsync(pasifDahil: true);
            Assert.Equal(2, kalan.Count);
            Assert.All(kalan, h => Assert.Equal(Aday, h.FirmaId));
        }
    }
}
