using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Infrastructure.Context;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Öğrenilen eşleşmelerin toplu xlsx içe aktarımı: kolon-başlıktan-bul, satır bazlı
    /// doğrulama, <b>mevcut kararın korunması</b> ve firma izolasyonu.
    ///
    /// Eşleştirme mantığı bu dosyada sınanmıyor; yalnız içe aktarılan kaydın sonraki
    /// ekstrede geçmiş onay katmanından çözüldüğü doğrulanıyor.
    /// </summary>
    public class OgrenilenEslesmeIceAktarimServiceTests
    {
        private static readonly string[] VarsayilanBasliklar =
            { "Anahtar Çekirdek", "Hesap Kodu", "Hesap Adı", "Yön", "Kullanım Sayısı", "Son Kullanım" };

        private static OgrenilenEslesmeIceAktarimService Servis(CatalogContext db,
                                                                int firmaId = BankaEkstreTestOrtami.FirmaId)
            => new(db, BankaEkstreTestOrtami.Kapsam(firmaId));

        /// <summary>Kolon sırası <paramref name="basliklar"/> ile değiştirilebilir; satırlar aynı sırayı izler.</summary>
        private static MemoryStream DosyaBasliklarla(string[] basliklar, params string?[][] satirlar)
        {
            using var kitap = new XLWorkbook();
            var sayfa = kitap.Worksheets.Add("Öğrenilen Eşleşmeler");

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

        private static string?[] Satir(string anahtar, string kod, string hesapAdi = "", string yon = "Giren",
                                       string kullanim = "", string sonKullanim = "")
            => new string?[] { anahtar, kod, hesapAdi, yon, kullanim, sonKullanim };

        private static Task PlanaEkleAsync(CatalogContext db, params string[] kodlar)
            => PlanaEkleAsync(db, BankaEkstreTestOrtami.FirmaId, kodlar);

        private static Task PlanaEkleAsync(CatalogContext db, int firmaId, params string[] kodlar)
        {
            foreach (var kod in kodlar)
                db.EkstreHesapPlani.Add(new HesapPlaniKaydi
                {
                    FirmaId = firmaId,
                    Kod = kod,
                    Ad = $"{kod} CARISI",
                    AnaGrup = Normalizasyon.AnaGrup(kod),
                    Aktif = true,
                    SonGuncelleme = DateTime.Now
                });

            return db.SaveChangesAsync();
        }

        /// <summary>Hesap sahibi kimliği banka hesabı satırlarında durur (firma bazlı).</summary>
        private static Task HesapSahibiYazAsync(CatalogContext db, string unvan, string? takmaAdlar = null,
                                                int firmaId = BankaEkstreTestOrtami.FirmaId)
        {
            db.EkstreBankaHesaplari.Add(new BankaHesabi
            {
                FirmaId = firmaId,
                OrkaHesapKodu = "102 1 32 87",
                HesapAdi = "VAKIFBANK VADESIZ TL",
                BankaAdi = "Vakıfbank",
                ParaBirimi = "TRY",
                HesapSahibiUnvani = unvan,
                HesapSahibiTakmaAdlari = takmaAdlar,
                Aktif = true
            });

            return db.SaveChangesAsync();
        }

        private static Task<List<HesapEslesmesi>> KayitlarAsync(CatalogContext db,
                                                                int firmaId = BankaEkstreTestOrtami.FirmaId)
            => db.EkstreHesapEslesmeleri.Where(e => e.FirmaId == firmaId)
                                        .OrderBy(e => e.AnahtarCekirdek).ThenBy(e => e.Yon)
                                        .ToListAsync();

        // ---- Mutlu yol ----

        [Fact]
        public async Task Gecerli_dosya_uc_satiri_ekler()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "120 N15", "320 D22", "120 K08");

            using var dosya = Dosya(
                Satir("NAOS ISTANBUL KOZMETIK", "120 N15"),
                Satir("DAGI GIYIM", "320 D22", yon: "Çıkan"),
                Satir("KEMAL TEKSTIL", "120 K08", kullanim: "7", sonKullanim: "14.03.2026"));

            var sonuc = await Servis(db).IceAktarAsync(dosya);

            Assert.Equal(3, sonuc.Okunan);
            Assert.Equal(3, sonuc.Eklenen);
            Assert.Equal(0, sonuc.Atlanan);
            Assert.Equal(0, sonuc.Hatali);
            Assert.Empty(sonuc.Hatalar);

            var kayitlar = await KayitlarAsync(db);
            Assert.Equal(3, kayitlar.Count);
            Assert.All(kayitlar, k => Assert.Equal(AnahtarTipi.UnvanCekirdek, k.AnahtarTipi));
            Assert.All(kayitlar, k => Assert.Null(k.AyirtEdiciEk));

            var kemal = kayitlar.Single(k => k.AnahtarCekirdek == "KEMAL TEKSTIL");
            Assert.Equal("120 K08", kemal.HesapKodu);
            // Ad dosyadan değil hesap planından okunur.
            Assert.Equal("120 K08 CARISI", kemal.HesapAdi);
            Assert.Equal(7, kemal.KullanimSayisi);
            Assert.Equal(new DateTime(2026, 3, 14), kemal.SonKullanim.Date);

            Assert.Equal(Yon.Cikan, kayitlar.Single(k => k.AnahtarCekirdek == "DAGI GIYIM").Yon);
        }

        [Fact]
        public async Task Anahtar_sistemin_kendi_normalizasyonundan_geciyor()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "120 D22");

            // Dosyada ham unvan: Türkçe karakter, şirket türü ekleri, çift boşluk.
            using var dosya = Dosya(Satir("Dağı  Giyim Sanayi ve Ticaret A.Ş.", "120 D22"));

            var sonuc = await Servis(db).IceAktarAsync(dosya);

            Assert.Equal(1, sonuc.Eklenen);
            Assert.Equal("DAGI GIYIM", (await KayitlarAsync(db)).Single().AnahtarCekirdek);
        }

        [Fact]
        public async Task Kolon_sirasi_degistirilmis_dosya_yine_okunur()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "120 N15");

            // Sıra değişik, başlıklar Türkçe karaktersiz ve küçük harfli.
            using var dosya = DosyaBasliklarla(
                new[] { "yon", "hesap kodu", "kullanim sayisi", "anahtar cekirdek" },
                new string?[] { "Cikan", "120 N15", "4", "NAOS ISTANBUL KOZMETIK" });

            var sonuc = await Servis(db).IceAktarAsync(dosya);

            Assert.Equal(1, sonuc.Eklenen);

            var kayit = (await KayitlarAsync(db)).Single();
            Assert.Equal("NAOS ISTANBUL KOZMETIK", kayit.AnahtarCekirdek);
            Assert.Equal("120 N15", kayit.HesapKodu);
            Assert.Equal(Yon.Cikan, kayit.Yon);
            Assert.Equal(4, kayit.KullanimSayisi);
        }

        [Fact]
        public async Task Yon_bos_birakilirsa_iki_yon_icin_de_kayit_yazilir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "120 N15");

            using var dosya = Dosya(Satir("NAOS ISTANBUL KOZMETIK", "120 N15", yon: string.Empty));

            var sonuc = await Servis(db).IceAktarAsync(dosya);

            // Satır bir tane, kayıt iki: HesapEslesmesi.Yon "farketmez" tutamıyor.
            Assert.Equal(1, sonuc.Eklenen);
            Assert.Equal(2, sonuc.EklenenKayit);

            var kayitlar = await KayitlarAsync(db);
            Assert.Equal(new[] { Yon.Giren, Yon.Cikan }, kayitlar.Select(k => k.Yon).OrderBy(y => y).ToArray());
        }

        // ---- Mevcut kararın korunması ----

        [Fact]
        public async Task Ikinci_kez_ayni_dosya_hicbir_kaydi_ezmez()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "120 N15", "320 D22", "120 K08");

            string?[][] satirlar =
            {
                Satir("NAOS ISTANBUL KOZMETIK", "120 N15"),
                Satir("DAGI GIYIM", "320 D22"),
                Satir("KEMAL TEKSTIL", "120 K08")
            };

            using (var ilk = Dosya(satirlar))
                Assert.Equal(3, (await Servis(db).IceAktarAsync(ilk)).Eklenen);

            using var ikinci = Dosya(satirlar);
            var sonuc = await Servis(db).IceAktarAsync(ikinci);

            Assert.Equal(3, sonuc.Okunan);
            Assert.Equal(0, sonuc.Eklenen);
            Assert.Equal(3, sonuc.Atlanan);
            Assert.Equal(0, sonuc.Hatali);
            Assert.Equal(3, (await KayitlarAsync(db)).Count);
        }

        [Fact]
        public async Task Kullanicinin_onaydan_verdigi_karar_dosyaya_gore_onceliklidir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "120 N15", "120 X99");

            db.EkstreHesapEslesmeleri.Add(new HesapEslesmesi
            {
                FirmaId = BankaEkstreTestOrtami.FirmaId,
                AnahtarTipi = AnahtarTipi.UnvanCekirdek,
                AnahtarCekirdek = "NAOS ISTANBUL KOZMETIK",
                Yon = Yon.Giren,
                HesapKodu = "120 X99",
                KullanimSayisi = 3,
                SonKullanim = DateTime.Now
            });
            await db.SaveChangesAsync();

            using var dosya = Dosya(Satir("NAOS ISTANBUL KOZMETIK", "120 N15"));
            var sonuc = await Servis(db).IceAktarAsync(dosya);

            Assert.Equal(1, sonuc.Atlanan);
            Assert.Equal(0, sonuc.Eklenen);
            Assert.Contains(sonuc.Uyarilar, u => u.SatirNo == 2 && u.Message.Contains("zaten var"));

            var kayit = (await KayitlarAsync(db)).Single();
            Assert.Equal("120 X99", kayit.HesapKodu);
            Assert.Equal(3, kayit.KullanimSayisi);
        }

        [Fact]
        public async Task Farketmez_satirinda_yalniz_bos_yon_yazilir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "120 N15", "120 X99");

            db.EkstreHesapEslesmeleri.Add(new HesapEslesmesi
            {
                FirmaId = BankaEkstreTestOrtami.FirmaId,
                AnahtarTipi = AnahtarTipi.UnvanCekirdek,
                AnahtarCekirdek = "NAOS ISTANBUL KOZMETIK",
                Yon = Yon.Giren,
                HesapKodu = "120 X99",
                SonKullanim = DateTime.Now
            });
            await db.SaveChangesAsync();

            using var dosya = Dosya(Satir("NAOS ISTANBUL KOZMETIK", "120 N15", yon: "Farketmez"));
            var sonuc = await Servis(db).IceAktarAsync(dosya);

            Assert.Equal(1, sonuc.Eklenen);
            Assert.Equal(1, sonuc.EklenenKayit);

            var kayitlar = await KayitlarAsync(db);
            Assert.Equal("120 X99", kayitlar.Single(k => k.Yon == Yon.Giren).HesapKodu);
            Assert.Equal("120 N15", kayitlar.Single(k => k.Yon == Yon.Cikan).HesapKodu);
        }

        // ---- Satır bazlı doğrulama ----

        [Fact]
        public async Task Hesap_planinda_olmayan_kod_atlanir_ve_raporlanir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "120 N15");

            using var dosya = Dosya(
                Satir("NAOS ISTANBUL KOZMETIK", "120 N15"),
                Satir("KEMAL TEKSTIL", "120 YOK"));

            var sonuc = await Servis(db).IceAktarAsync(dosya);

            // Hatalı satır dosyanın kalanını düşürmüyor.
            Assert.Equal(1, sonuc.Eklenen);
            Assert.Equal(1, sonuc.Hatali);

            var hata = Assert.Single(sonuc.Hatalar);
            Assert.Equal(3, hata.SatirNo);
            Assert.Equal(nameof(HesapEslesmesi.HesapKodu), hata.Field);
            Assert.Contains("hesap planında yok", hata.Message);

            Assert.Equal("NAOS ISTANBUL KOZMETIK", (await KayitlarAsync(db)).Single().AnahtarCekirdek);
        }

        [Fact]
        public async Task Hesap_sahibi_cekirdegini_kapsayan_anahtar_reddedilir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "120 N15", "120 A01");
            await HesapSahibiYazAsync(db, "PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ");

            using var dosya = Dosya(
                Satir("NAOS ISTANBUL KOZMETIK", "120 N15"),
                // Hesap sahibinin kendi adının bir yazımı: karşı taraf olarak öğrenilemez.
                Satir("ADAY BAGIMSIZ DENETIM", "120 A01"));

            var sonuc = await Servis(db).IceAktarAsync(dosya);

            Assert.Equal(1, sonuc.Eklenen);
            Assert.Equal(1, sonuc.Hatali);

            var hata = Assert.Single(sonuc.Hatalar);
            Assert.Equal(nameof(HesapEslesmesi.AnahtarCekirdek), hata.Field);
            Assert.Contains("hesap sahibinin", hata.Message, StringComparison.OrdinalIgnoreCase);

            Assert.DoesNotContain(await KayitlarAsync(db), k => k.HesapKodu == "120 A01");
        }

        [Fact]
        public async Task Kisa_anahtar_reddedilir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "120 N15");

            using var dosya = Dosya(Satir("NAOS", "120 N15"));
            var sonuc = await Servis(db).IceAktarAsync(dosya);

            Assert.Equal(1, sonuc.Hatali);
            Assert.Equal(nameof(HesapEslesmesi.AnahtarCekirdek), Assert.Single(sonuc.Hatalar).Field);
            Assert.Empty(await KayitlarAsync(db));
        }

        [Fact]
        public async Task Taninmayan_yon_reddedilir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "120 N15");

            using var dosya = Dosya(Satir("NAOS ISTANBUL KOZMETIK", "120 N15", yon: "Belki"));
            var sonuc = await Servis(db).IceAktarAsync(dosya);

            Assert.Equal(1, sonuc.Hatali);
            Assert.Equal(nameof(HesapEslesmesi.Yon), Assert.Single(sonuc.Hatalar).Field);
            Assert.Empty(await KayitlarAsync(db));
        }

        [Fact]
        public async Task Ayni_anahtar_dosyada_iki_kez_gecerse_ikinci_satir_hatali()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "120 N15", "120 X99");

            using var dosya = Dosya(
                Satir("NAOS ISTANBUL KOZMETIK", "120 N15"),
                Satir("NAOS ISTANBUL KOZMETIK", "120 X99"));

            var sonuc = await Servis(db).IceAktarAsync(dosya);

            Assert.Equal(1, sonuc.Eklenen);
            Assert.Equal(1, sonuc.Hatali);
            Assert.Contains("2 numaralı satırda da geçiyor", Assert.Single(sonuc.Hatalar).Message);

            // İlk satır işlendi; ikincisi belirsiz olduğu için hiç yazılmadı.
            Assert.Equal("120 N15", (await KayitlarAsync(db)).Single().HesapKodu);
        }

        [Fact]
        public async Task Ayni_anahtar_farkli_yonlerde_iki_satir_olabilir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "120 N15", "320 N15");

            using var dosya = Dosya(
                Satir("NAOS ISTANBUL KOZMETIK", "120 N15", yon: "Giren"),
                Satir("NAOS ISTANBUL KOZMETIK", "320 N15", yon: "Çıkan"));

            var sonuc = await Servis(db).IceAktarAsync(dosya);

            Assert.Equal(2, sonuc.Eklenen);
            Assert.Equal(0, sonuc.Hatali);
            Assert.Equal(2, (await KayitlarAsync(db)).Count);
        }

        [Fact]
        public async Task Basliksiz_dosya_hic_islenmez()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            using var dosya = DosyaBasliklarla(new[] { "Bir Şey", "Başka Şey" },
                                               new string?[] { "NAOS ISTANBUL KOZMETIK", "120 N15" });

            await Assert.ThrowsAsync<InvalidDataException>(() => Servis(db).IceAktarAsync(dosya));
        }

        // ---- Kapsam ----

        [Fact]
        public async Task Farkli_firmada_ayni_anahtar_ayri_kayit_olur()
        {
            const int digerFirma = BankaEkstreTestOrtami.FirmaId + 1;
            var veritabani = $"ogrenme-ice-aktarim-{Guid.NewGuid()}";

            using var db = BankaEkstreTestOrtami.YeniContext(veritabani);
            await PlanaEkleAsync(db, "120 N15");
            await PlanaEkleAsync(db, digerFirma, "120 N77");

            using (var dosya = Dosya(Satir("NAOS ISTANBUL KOZMETIK", "120 N15")))
                Assert.Equal(1, (await Servis(db).IceAktarAsync(dosya)).Eklenen);

            // Aynı veritabanı, farklı firma kapsamı: mevcut kayıt görünmez, ayrı kayıt açılır.
            using (var dosya = Dosya(Satir("NAOS ISTANBUL KOZMETIK", "120 N77")))
            {
                var sonuc = await Servis(db, digerFirma).IceAktarAsync(dosya);
                Assert.Equal(1, sonuc.Eklenen);
                Assert.Equal(0, sonuc.Atlanan);
            }

            Assert.Equal("120 N15", (await KayitlarAsync(db)).Single().HesapKodu);
            Assert.Equal("120 N77", (await KayitlarAsync(db, digerFirma)).Single().HesapKodu);
        }

        [Fact]
        public async Task Hesap_sahibi_denetimi_yalniz_kendi_firmasinin_kimligini_kullanir()
        {
            const int digerFirma = BankaEkstreTestOrtami.FirmaId + 1;
            var veritabani = $"ogrenme-ice-aktarim-{Guid.NewGuid()}";

            using var db = BankaEkstreTestOrtami.YeniContext(veritabani);
            await PlanaEkleAsync(db, digerFirma, "120 A01");
            await HesapSahibiYazAsync(db, "PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ");

            // Aday'ın kendi adı, SMMM firması için sıradan bir caridir.
            using var dosya = Dosya(Satir("ADAY BAGIMSIZ DENETIM", "120 A01"));
            var sonuc = await Servis(db, digerFirma).IceAktarAsync(dosya);

            Assert.Equal(1, sonuc.Eklenen);
            Assert.Empty(sonuc.Hatalar);
        }

        // ---- Şablon ----

        [Fact]
        public void Sablon_zorunlu_basliklari_iceriyor()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            var icerik = Servis(db).SablonUret();

            using var kitap = new XLWorkbook(new MemoryStream(icerik));
            var sayfa = kitap.Worksheets.First();

            var basliklar = sayfa.Row(1).CellsUsed().Select(h => h.GetString()).ToList();
            Assert.Contains("Anahtar Çekirdek", basliklar);
            Assert.Contains("Hesap Kodu", basliklar);
            Assert.Contains("Yön", basliklar);
        }

        [Fact]
        public async Task Sablon_kendi_ice_aktarimindan_geciyor()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "120 N15");

            var servis = Servis(db);

            // Şablonun başlıkları içe aktarımın aradığı adlarla birebir olmalı; boş şablon
            // hata vermeden okunmalı (satır yok).
            using var bos = new MemoryStream(servis.SablonUret());
            var sonuc = await servis.IceAktarAsync(bos);

            Assert.Equal(0, sonuc.Okunan);
            Assert.Empty(sonuc.Hatalar);
        }

        // ---- Eşleştirmeye bağlanması ----

        [Fact]
        public async Task Ice_aktarilan_eslesme_gecmis_onay_katmanindan_cozuluyor()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            await PlanaEkleAsync(db, "120 D22");

            using (var dosya = Dosya(Satir("DAGI GIYIM", "120 D22")))
                Assert.Equal(1, (await Servis(db).IceAktarAsync(dosya)).Eklenen);

            var veri = new EslestirmeVerisi
            {
                Eslesmeler = await KayitlarAsync(db)
            };

            // Ekstredeki ham unvan dosyadakinden farklı yazılmış; çekirdek aynı olduğu için tutar.
            var sonuc = new HesapEslestirici().Coz(
                new SatirBaglami
                {
                    IslemTipi = "Gelen EFT Otomatik Yatan",
                    HamAciklama = "0000999 sorgu numaralı DAĞİ GİYİM SANAYİ VE TİCARET ANONİM ŞİRKETİ tarafından",
                    Unvan = "DAĞİ GİYİM SANAYİ VE TİCARET ANONİM ŞİRKETİ",
                    Yon = Yon.Giren
                },
                veri);

            Assert.Equal(KaynakKatman.GecmisOnay, sonuc.Katman);
            Assert.Equal("120 D22", sonuc.HesapKodu);
        }
    }
}
