using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Banka adı yönetimi. Alan serbest metin olduğu sürece aynı banka birden fazla yazımla
    /// giriliyordu ("Vakıf Bank Eur", "Vakıfbank Vadeli", "İŞ BANKASI"); bu yalnız görüntü
    /// sorunu değil — "aynı banka önceliği" kuralı <c>BankaAdi</c> üzerinden çalıştığı için
    /// bankalar arası eşleştirme de bozuluyordu.
    ///
    /// Çözüm iki parçalı: ad listesi (ekranda açılır liste) ve birleştirme.
    /// </summary>
    public class BankaAdiYonetimiTests
    {
        private static BankaHesabiService Servis(CatalogContext db)
            => new(db, new EkstreParserSecici(new IEkstreParser[] { new VakifbankVadesizParser() }),
                   BankaEkstreTestOrtami.Kapsam());

        private static BankaHesabiYazDto Yaz(string kod, string banka, bool aktif = true) => new()
        {
            BankaAdi = banka,
            HesapAdi = $"{banka} hesabı",
            OrkaHesapKodu = kod,
            ParserTipi = string.Empty,
            ParaBirimi = "TRY",
            Aktif = aktif
        };

        private static async Task<CatalogContext> UcYazimlaAsync()
        {
            var db = BankaEkstreTestOrtami.YeniContext();
            var servis = Servis(db);

            await servis.CreateAsync(Yaz("102 1 1 01", "Vakıfbank"));
            await servis.CreateAsync(Yaz("102 2 1 01", "Vakıf Bank Eur"));
            await servis.CreateAsync(Yaz("102 2 1 02", "Vakıfbank Vadeli", aktif: false));
            await servis.CreateAsync(Yaz("102 1 5 01", "İş Bankası"));

            return db;
        }

        [Fact]
        public async Task Banka_adlari_hesap_sayilariyla_listelenir()
        {
            using var db = await UcYazimlaAsync();

            var adlar = await Servis(db).BankaAdlariAsync();

            Assert.Equal(4, adlar.Count);
            Assert.All(adlar, a => Assert.Equal(1, a.HesapSayisi));

            // Pasif hesap da sayılır: yanlış yazımların bir kısmı pasif kayıtlarda duruyor
            // ve birleştirme onları da düzeltmeli.
            Assert.Contains(adlar, a => a.Ad == "Vakıfbank Vadeli");
        }

        [Fact]
        public async Task Yazimlar_tek_ada_indirilir()
        {
            using var db = await UcYazimlaAsync();

            var sonuc = await Servis(db).BankaAdiBirlestirAsync(new BankaAdiBirlestirDto
            {
                Kaynaklar = new List<string> { "Vakıf Bank Eur", "Vakıfbank Vadeli" },
                Hedef = "Vakıfbank"
            });

            Assert.Equal(2, sonuc.EtkilenenHesap);
            Assert.Equal("Vakıfbank", sonuc.Hedef);

            // Dört hesap, iki banka adı kaldı.
            Assert.Equal(3, db.EkstreBankaHesaplari.Count(h => h.BankaAdi == "Vakıfbank"));
            Assert.Equal(2, sonuc.BankaAdlari.Count);

            // Yalnız ad değişti: kodlar, hesaplar ve aktiflik olduğu gibi duruyor.
            Assert.Contains(db.EkstreBankaHesaplari, h => h.OrkaHesapKodu == "102 2 1 02" && !h.Aktif);
        }

        [Fact]
        public async Task Hedefin_kendisi_kaynak_listesinde_olsa_da_sayilmaz()
        {
            using var db = await UcYazimlaAsync();

            var sonuc = await Servis(db).BankaAdiBirlestirAsync(new BankaAdiBirlestirDto
            {
                Kaynaklar = new List<string> { "Vakıfbank", "Vakıf Bank Eur" },
                Hedef = "Vakıfbank"
            });

            // Zaten hedefte olan hesap "etkilenen" sayılmaz; kullanıcıya gösterilen sayı
            // gerçekten değişecek hesap sayısı olmalı.
            Assert.Equal(1, sonuc.EtkilenenHesap);
        }

        [Fact]
        public async Task Farkli_buyuk_kucuk_yazim_da_birlestirilir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await Servis(db).CreateAsync(Yaz("102 1 5 01", "İŞ BANKASI"));
            await Servis(db).CreateAsync(Yaz("102 1 5 02", "İş Bankası"));

            var sonuc = await Servis(db).BankaAdiBirlestirAsync(new BankaAdiBirlestirDto
            {
                Kaynaklar = new List<string> { "İŞ BANKASI" },
                Hedef = "İş Bankası"
            });

            Assert.Equal(1, sonuc.EtkilenenHesap);
            Assert.Single(sonuc.BankaAdlari);
        }

        [Fact]
        public async Task Bos_hedef_ve_bos_secim_reddedilir()
        {
            using var db = await UcYazimlaAsync();

            var bosHedef = await Assert.ThrowsAsync<BankaEkstreKuralException>(
                () => Servis(db).BankaAdiBirlestirAsync(new BankaAdiBirlestirDto { Hedef = "  " }));
            Assert.Equal(nameof(BankaAdiBirlestirDto.Hedef), bosHedef.Field);

            var bosSecim = await Assert.ThrowsAsync<BankaEkstreKuralException>(
                () => Servis(db).BankaAdiBirlestirAsync(new BankaAdiBirlestirDto
                {
                    Hedef = "Vakıfbank",
                    Kaynaklar = new List<string> { "Vakıfbank" }
                }));
            Assert.Equal(nameof(BankaAdiBirlestirDto.Kaynaklar), bosSecim.Field);
        }

        [Fact]
        public async Task Birlestirme_baska_firmanin_hesabina_dokunmaz()
        {
            var veritabani = $"banka-adi-{Guid.NewGuid()}";

            using var db = BankaEkstreTestOrtami.YeniContext(veritabani);

            var aday = new BankaHesabiService(db, new EkstreParserSecici(new IEkstreParser[] { new VakifbankVadesizParser() }),
                                              BankaEkstreTestOrtami.Kapsam(201));
            var smmm = new BankaHesabiService(db, new EkstreParserSecici(new IEkstreParser[] { new VakifbankVadesizParser() }),
                                              BankaEkstreTestOrtami.Kapsam(202));

            await aday.CreateAsync(Yaz("102 2 1 01", "Vakıf Bank Eur"));
            await smmm.CreateAsync(Yaz("102 2 1 01", "Vakıf Bank Eur"));

            await aday.BankaAdiBirlestirAsync(new BankaAdiBirlestirDto
            {
                Kaynaklar = new List<string> { "Vakıf Bank Eur" },
                Hedef = "Vakıfbank"
            });

            Assert.Equal("Vakıfbank", db.EkstreBankaHesaplari.Single(h => h.FirmaId == 201).BankaAdi);
            Assert.Equal("Vakıf Bank Eur", db.EkstreBankaHesaplari.Single(h => h.FirmaId == 202).BankaAdi);
        }
    }
}
