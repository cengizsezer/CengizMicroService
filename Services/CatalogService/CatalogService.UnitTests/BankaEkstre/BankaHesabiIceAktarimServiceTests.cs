using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;
using ClosedXML.Excel;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Banka hesaplarının toplu xlsx içe aktarımı: kolon-başlıktan-bul, satır bazlı
    /// doğrulama, upsert ve firma izolasyonu.
    /// </summary>
    public class BankaHesabiIceAktarimServiceTests
    {
        private static readonly string[] VarsayilanBasliklar =
            { "Orka Hesap Kodu", "Hesap Adı", "Banka Adı", "Hesap Tipi", "Para Birimi", "Parser Tipi", "IBAN" };

        private static readonly string[] AnahtarliBasliklar =
            { "Orka Hesap Kodu", "Hesap Adı", "Banka Adı", "Hesap Tipi", "Para Birimi", "Parser Tipi", "IBAN",
              "Eşleştirme Anahtarları" };

        private static BankaHesabiIceAktarimService Servis(CatalogContext db,
                                                           int firmaId = BankaEkstreTestOrtami.FirmaId)
            => new(db, new EkstreParserSecici(new IEkstreParser[] { new VakifbankVadesizParser() }),
                   BankaEkstreTestOrtami.Kapsam(firmaId));

        /// <summary>Kolon sırası <paramref name="basliklar"/> ile değiştirilebilir; satırlar aynı sırayı izler.</summary>
        private static MemoryStream DosyaBasliklarla(string[] basliklar, params string?[][] satirlar)
        {
            using var kitap = new XLWorkbook();
            var sayfa = kitap.Worksheets.Add("Banka Hesapları");

            for (var i = 0; i < basliklar.Length; i++)
                sayfa.Cell(1, i + 1).Value = basliklar[i];

            var satirNo = 2;
            foreach (var satir in satirlar)
            {
                for (var i = 0; i < satir.Length; i++)
                    sayfa.Cell(satirNo, i + 1).Value = satir[i] ?? string.Empty;
                satirNo++;
            }

            var akis = new MemoryStream();
            kitap.SaveAs(akis);
            akis.Position = 0;
            return akis;
        }

        private static MemoryStream Dosya(params string?[][] satirlar) => DosyaBasliklarla(VarsayilanBasliklar, satirlar);

        private static string?[] Satir(string kod, string hesapAdi = "VAKIFBANK VADESIZ TL", string banka = "Vakıfbank",
                                       string tip = "Vadesiz", string para = "TL",
                                       string parser = BankaEkstreTestOrtami.ParserTipi, string iban = "")
            => new string?[] { kod, hesapAdi, banka, tip, para, parser, iban };

        /// <summary>
        /// Kodlar hesap planında olmadan satır atlanır; testler için planı doldurur.
        /// TenantNo yalnız <c>SaveChangesAsync</c> içinde dolduruluyor, bu yüzden async.
        /// </summary>
        private static Task PlanaEkleAsync(CatalogContext db, params string[] kodlar)
            => PlanaEkleAsync(db, BankaEkstreTestOrtami.FirmaId, kodlar);

        private static Task PlanaEkleAsync(CatalogContext db, int firmaId, params string[] kodlar)
        {
            foreach (var kod in kodlar)
                db.EkstreHesapPlani.Add(new HesapPlaniKaydi
                {
                    FirmaId = firmaId,
                    Kod = kod,
                    Ad = $"{kod} HESABI",
                    AnaGrup = "102",
                    Aktif = true,
                    SonGuncelleme = DateTime.Now
                });

            return db.SaveChangesAsync();
        }

        [Fact]
        public async Task Gecerli_dosya_uc_satiri_ekler()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "102 1 32 87", "102 1 33 01", "102 2 01 05");

            using var dosya = Dosya(
                Satir("102 1 32 87"),
                Satir("102 1 33 01", banka: "Ziraat", parser: string.Empty),
                Satir("102 2 01 05", banka: "TEB", tip: "Vadeli", para: "USD", iban: "TR33 0006 7010 0000 0012 3456 78"));

            var sonuc = await Servis(db).IceAktarAsync(dosya);

            Assert.Equal(3, sonuc.Okunan);
            Assert.Equal(3, sonuc.Eklenen);
            Assert.Equal(0, sonuc.Guncellenen);
            Assert.Equal(0, sonuc.Atlanan);
            Assert.Empty(sonuc.Hatalar);
            Assert.Equal(3, db.EkstreBankaHesaplari.Count());

            // Kod boşluklu saklanır, TL ISO koduna çevrilir, IBAN boşluksuz.
            var hesap = db.EkstreBankaHesaplari.Single(h => h.OrkaHesapKodu == "102 2 01 05");
            Assert.Equal("TEB", hesap.BankaAdi);
            Assert.Equal(HesapTipi.Vadeli, hesap.HesapTipi);
            Assert.Equal("USD", hesap.ParaBirimi);
            Assert.Equal("TR330006701000000012345678", hesap.Iban);
            Assert.Equal("TRY", db.EkstreBankaHesaplari.Single(h => h.OrkaHesapKodu == "102 1 32 87").ParaBirimi);

            // Parser boş bırakılan hesap tanımlanır ama uyarı verilir: ekstresi yüklenemez.
            Assert.Null(db.EkstreBankaHesaplari.Single(h => h.OrkaHesapKodu == "102 1 33 01").ParserTipi);
            Assert.Contains(sonuc.Uyarilar, u => u.Field == nameof(BankaHesabi.ParserTipi));
        }

        [Fact]
        public async Task Eslestirme_anahtarlari_kolonu_okunur()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "102 1 1 01", "102 1 1 04");

            using var dosya = DosyaBasliklarla(AnahtarliBasliklar,
                new string?[] { "102 1 1 01", "VAKIFBANK VADESIZ TL", "Vakıfbank", "Vadesiz", "TL", "", "", "" },
                new string?[] { "102 1 1 04", "VAKIFBANK VADELİ TL", "Vakıfbank", "Vadeli", "TL", "", "",
                                " Otomatik   Süpürme , Süpürme " });

            var sonuc = await Servis(db).IceAktarAsync(dosya);

            Assert.Equal(2, sonuc.Eklenen);
            Assert.Equal(0, sonuc.Atlanan);
            Assert.Equal("Otomatik Süpürme, Süpürme",
                db.EkstreBankaHesaplari.Single(h => h.OrkaHesapKodu == "102 1 1 04").EslestirmeAnahtarlari);
            Assert.Null(db.EkstreBankaHesaplari.Single(h => h.OrkaHesapKodu == "102 1 1 01").EslestirmeAnahtarlari);
        }

        [Fact]
        public async Task Bos_anahtar_hucresi_mevcut_anahtari_silmez()
        {
            var veritabani = $"hesap-anahtar-{Guid.NewGuid()}";

            using (var db = BankaEkstreTestOrtami.YeniContext(veritabani))
            {
                await PlanaEkleAsync(db, "102 1 1 04");
                using var ilk = DosyaBasliklarla(AnahtarliBasliklar,
                    new string?[] { "102 1 1 04", "VAKIFBANK VADELİ TL", "Vakıfbank", "Vadeli", "TL", "", "",
                                    "Otomatik Süpürme" });
                await Servis(db).IceAktarAsync(ilk);
            }

            using (var db = BankaEkstreTestOrtami.YeniContext(veritabani))
            {
                // Anahtar kolonu olmayan dosya, elle girilmiş anahtarı silmemeli.
                using var ikinci = Dosya(Satir("102 1 1 04", hesapAdi: "VAKIFBANK VADELİ TL", tip: "Vadeli", parser: ""));
                await Servis(db).IceAktarAsync(ikinci);

                Assert.Equal("Otomatik Süpürme", db.EkstreBankaHesaplari.Single().EslestirmeAnahtarlari);
            }
        }

        [Fact]
        public async Task Ayni_dosya_ikinci_kez_guncelleme_sayar()
        {
            var veritabani = $"hesap-ice-aktarim-{Guid.NewGuid()}";

            using (var db = BankaEkstreTestOrtami.YeniContext(veritabani))
            {
                await PlanaEkleAsync(db, "102 1 32 87", "102 1 33 01", "102 2 01 05");
                using var ilk = Dosya(Satir("102 1 32 87"), Satir("102 1 33 01"), Satir("102 2 01 05"));
                await Servis(db).IceAktarAsync(ilk);
            }

            using (var db = BankaEkstreTestOrtami.YeniContext(veritabani))
            {
                using var ikinci = Dosya(
                    Satir("102 1 32 87", hesapAdi: "VAKIFBANK VADESIZ TL - MERKEZ"),
                    Satir("102 1 33 01"),
                    Satir("102 2 01 05"));

                var sonuc = await Servis(db).IceAktarAsync(ikinci);

                Assert.Equal(3, sonuc.Guncellenen);
                Assert.Equal(0, sonuc.Eklenen);
                Assert.Equal(3, db.EkstreBankaHesaplari.Count());
                Assert.Equal("VAKIFBANK VADESIZ TL - MERKEZ",
                             db.EkstreBankaHesaplari.Single(h => h.OrkaHesapKodu == "102 1 32 87").HesapAdi);
            }
        }

        [Fact]
        public async Task Gecersiz_hesap_tipi_satiri_atlanir_digerleri_islenir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "102 1 32 87", "102 1 33 01");

            using var dosya = Dosya(
                Satir("102 1 32 87", tip: "Vadesizz"),
                Satir("102 1 33 01"));

            var sonuc = await Servis(db).IceAktarAsync(dosya);

            Assert.Equal(2, sonuc.Okunan);
            Assert.Equal(1, sonuc.Eklenen);
            Assert.Equal(1, sonuc.Atlanan);
            Assert.Single(db.EkstreBankaHesaplari);

            var hata = Assert.Single(sonuc.Hatalar);
            Assert.Equal(2, hata.SatirNo);
            Assert.Equal(nameof(BankaHesabi.HesapTipi), hata.Field);
            Assert.Contains("Vadesiz, Vadeli", hata.Message);
        }

        [Fact]
        public async Task Hesap_planinda_olmayan_kod_atlanir_ve_raporlanir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "102 1 32 87");

            using var dosya = Dosya(Satir("102 1 32 87"), Satir("102 9 99 99"));
            var sonuc = await Servis(db).IceAktarAsync(dosya);

            Assert.Equal(1, sonuc.Eklenen);
            Assert.Equal(1, sonuc.Atlanan);

            var hata = Assert.Single(sonuc.Hatalar);
            Assert.Equal(3, hata.SatirNo);
            Assert.Contains("hesap planında yok", hata.Message);
        }

        [Fact]
        public async Task Kolon_sirasi_degisse_de_okunur()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "102 1 32 87");

            // Sıra tersine çevrilmiş ve başlıklar Türkçe karaktersiz/küçük harfli yazılmış.
            var basliklar = new[] { "iban", "parser tipi", "para birimi", "hesap tipi", "banka adi", "hesap adi", "ORKA HESAP KODU" };
            using var dosya = DosyaBasliklarla(basliklar,
                new string?[] { "", BankaEkstreTestOrtami.ParserTipi, "TL", "Vadesiz", "Vakıfbank", "VAKIFBANK VADESIZ TL", "102 1 32 87" });

            var sonuc = await Servis(db).IceAktarAsync(dosya);

            Assert.Equal(1, sonuc.Eklenen);
            Assert.Empty(sonuc.Hatalar);

            var hesap = db.EkstreBankaHesaplari.Single();
            Assert.Equal("102 1 32 87", hesap.OrkaHesapKodu);
            Assert.Equal("Vakıfbank", hesap.BankaAdi);
            Assert.Equal(BankaEkstreTestOrtami.ParserTipi, hesap.ParserTipi);
        }

        [Fact]
        public async Task Ayni_kod_dosyada_iki_kez_gecerse_ikincisi_hata()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "102 1 32 87");

            using var dosya = Dosya(Satir("102 1 32 87"), Satir("102 1 32 87", banka: "Ziraat"));
            var sonuc = await Servis(db).IceAktarAsync(dosya);

            Assert.Equal(1, sonuc.Eklenen);
            Assert.Equal(1, sonuc.Atlanan);
            Assert.Single(db.EkstreBankaHesaplari);

            var hata = Assert.Single(sonuc.Hatalar);
            Assert.Equal(3, hata.SatirNo);
            Assert.Contains("birden fazla", hata.Message);
            Assert.Equal("Vakıfbank", db.EkstreBankaHesaplari.Single().BankaAdi);
        }

        [Fact]
        public async Task Bilinmeyen_parser_hata_verir_ve_gecerli_tipleri_listeler()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "102 1 32 87");

            using var dosya = Dosya(Satir("102 1 32 87", parser: "ZIRAAT_VADESIZ"));
            var sonuc = await Servis(db).IceAktarAsync(dosya);

            Assert.Equal(1, sonuc.Atlanan);
            Assert.Empty(db.EkstreBankaHesaplari);

            var hata = Assert.Single(sonuc.Hatalar);
            Assert.Equal(nameof(BankaHesabi.ParserTipi), hata.Field);
            Assert.Contains(BankaEkstreTestOrtami.ParserTipi, hata.Message);
        }

        [Fact]
        public async Task Yuz_iki_ile_baslamayan_kod_uyari_verir_ama_eklenir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "103 1 01 01");

            using var dosya = Dosya(Satir("103 1 01 01"));
            var sonuc = await Servis(db).IceAktarAsync(dosya);

            Assert.Equal(1, sonuc.Eklenen);
            Assert.Equal(0, sonuc.Atlanan);
            Assert.Empty(sonuc.Hatalar);
            Assert.Contains(sonuc.Uyarilar, u => u.Message.Contains("102 ile başlamıyor"));
        }

        /// <summary>
        /// Aynı ORKA kodu iki firmada ayrı kayıttır. Tek veritabanı, iki kapsam: içe
        /// aktarım kendi firmasının dışını görmediği için "güncelleme" değil "ekleme" yapar.
        /// </summary>
        [Fact]
        public async Task Farkli_firmada_ayni_kod_ayri_kayit_olur()
        {
            const int aday = 201;
            const int smmm = 106;

            using var db = BankaEkstreTestOrtami.YeniContext();

            await PlanaEkleAsync(db, aday, "102 1 32 87");
            using (var dosya = Dosya(Satir("102 1 32 87")))
                Assert.Equal(1, (await Servis(db, aday).IceAktarAsync(dosya)).Eklenen);

            await PlanaEkleAsync(db, smmm, "102 1 32 87");
            using (var dosya = Dosya(Satir("102 1 32 87", banka: "Ziraat")))
            {
                var sonuc = await Servis(db, smmm).IceAktarAsync(dosya);

                Assert.Equal(1, sonuc.Eklenen);
                Assert.Equal(0, sonuc.Guncellenen);
            }

            Assert.Equal("Ziraat", db.EkstreBankaHesaplari.Single(h => h.FirmaId == smmm).BankaAdi);
            Assert.Equal("Vakıfbank", db.EkstreBankaHesaplari.Single(h => h.FirmaId == aday).BankaAdi);
        }

        [Fact]
        public async Task Dosyada_olmayan_mevcut_hesaba_dokunulmaz()
        {
            var veritabani = $"hesap-dokunma-{Guid.NewGuid()}";

            using (var db = BankaEkstreTestOrtami.YeniContext(veritabani))
            {
                await PlanaEkleAsync(db, "102 1 32 87", "102 1 33 01");
                using var ilk = Dosya(Satir("102 1 32 87"), Satir("102 1 33 01", banka: "Ziraat"));
                await Servis(db).IceAktarAsync(ilk);
            }

            using (var db = BankaEkstreTestOrtami.YeniContext(veritabani))
            {
                using var ikinci = Dosya(Satir("102 1 32 87"));
                var sonuc = await Servis(db).IceAktarAsync(ikinci);

                Assert.Equal(1, sonuc.Guncellenen);

                // Hesap planı içe aktarımının aksine pasife çekilmez: kullanıcı bankayı
                // bilerek dosya dışında bırakmış olabilir.
                var disarida = db.EkstreBankaHesaplari.Single(h => h.OrkaHesapKodu == "102 1 33 01");
                Assert.True(disarida.Aktif);
                Assert.Equal("Ziraat", disarida.BankaAdi);
            }
        }

        [Fact]
        public async Task Zorunlu_kolon_yoksa_dosya_hic_islenmez()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "102 1 32 87");

            var basliklar = new[] { "Orka Hesap Kodu", "Hesap Adı", "Banka Adı" };
            using var dosya = DosyaBasliklarla(basliklar, new string?[] { "102 1 32 87", "VAKIFBANK", "Vakıfbank" });

            var hata = await Assert.ThrowsAsync<InvalidDataException>(() => Servis(db).IceAktarAsync(dosya));
            Assert.Contains("Hesap Tipi", hata.Message);
            Assert.Empty(db.EkstreBankaHesaplari);
        }

        [Fact]
        public void Sablon_dogru_basliklarla_uretilir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            var baytlar = Servis(db).SablonUret();

            using var akis = new MemoryStream(baytlar);
            using var kitap = new XLWorkbook(akis);
            var sayfa = kitap.Worksheets.First();

            for (var i = 0; i < VarsayilanBasliklar.Length; i++)
                Assert.Equal(VarsayilanBasliklar[i], sayfa.Cell(1, i + 1).GetString());

            // Şablon boş: başlık dışında veri satırı yok.
            Assert.Equal(1, sayfa.LastRowUsed()!.RowNumber());
        }
    }
}
