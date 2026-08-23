using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Banka Otomasyon firma seçim ekranının dayandığı iki iddia:
    ///
    /// 1. Seçilen firma gerçekten veri kapsamıdır — Aday seçiliyken yapılan hesap planı
    ///    içe aktarımı Aday'ın kayıtlarına yazılır, SMMM'ninkilere değil.
    /// 2. Seçim ekranı, girilmemiş firmaların sayaçlarını da doğru gösterir.
    ///
    /// Kapsam artık token'daki tenant değil, isteğin <c>firmaId</c>'si (bkz. KARARLAR §68).
    /// Bu yüzden testler tek context + farklı kapsamla kuruluyor: gerçek kurulumda da tek
    /// veritabanı var ve izolasyon <c>FirmaId</c> ile sağlanıyor.
    /// </summary>
    public class FirmaKapsamiTests
    {
        private const int Aday = 201;
        private const int Smmm = 106;

        private static MemoryStream HesapPlaniDosyasi(params (string Kod, string Ad)[] satirlar)
        {
            using var kitap = new XLWorkbook();
            var sayfa = kitap.Worksheets.Add("Hesap Planı");

            sayfa.Cell(1, 1).Value = "ORKA HESAP PLANI";
            sayfa.Cell(3, 1).Value = "Hesap Kodu";
            sayfa.Cell(3, 2).Value = "Hesap Adı";

            var satirNo = 4;
            foreach (var (kod, ad) in satirlar)
            {
                sayfa.Cell(satirNo, 1).Value = kod;
                sayfa.Cell(satirNo, 2).Value = ad;
                satirNo++;
            }

            var akis = new MemoryStream();
            kitap.SaveAs(akis);
            akis.Position = 0;
            return akis;
        }

        [Fact]
        public async Task Aday_secliyken_ice_aktarim_Adayin_kayitlarina_yazilir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            // SMMM'de önceden bir hesap planı var; Aday'ın aktarımı buna dokunmamalı.
            using (var dosya = HesapPlaniDosyasi(("320 S01", "SMMM SATICI")))
                await new EkstreHesapPlaniService(db, BankaEkstreTestOrtami.Kapsam(Smmm)).IceAktarAsync(dosya);

            // Ekrandaki seçim Aday: istek Aday kapsamıyla gider.
            using (var dosya = HesapPlaniDosyasi(("120 A01", "ADAY MÜŞTERİ"), ("120 A02", "ADAY MÜŞTERİ 2")))
            {
                var sonuc = await new EkstreHesapPlaniService(db, BankaEkstreTestOrtami.Kapsam(Aday))
                    .IceAktarAsync(dosya);

                Assert.Equal(2, sonuc.Eklenen);
            }

            // Kayıtlar Aday'a yazıldı mı, SMMM'ninkiler bozulmadı mı?
            var adayKodlar = await db.EkstreHesapPlani.AsNoTracking()
                .Where(h => h.FirmaId == Aday).Select(h => h.Kod).OrderBy(k => k).ToListAsync();
            Assert.Equal(new[] { "120 A01", "120 A02" }, adayKodlar);

            var smmmKodlar = await db.EkstreHesapPlani.AsNoTracking()
                .Where(h => h.FirmaId == Smmm).Select(h => h.Kod).ToListAsync();
            Assert.Equal(new[] { "320 S01" }, smmmKodlar);
        }

        /// <summary>
        /// Servis kapsamı okurken de yazarken de aynı firmayı kullanır: Aday'ın servisi
        /// SMMM'nin kayıtlarını hiç görmez, dolayısıyla "güncelleme" değil "ekleme" yapar.
        /// </summary>
        [Fact]
        public async Task Ayni_kod_iki_firmada_ayri_kayit_olur()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            using (var dosya = HesapPlaniDosyasi(("120 A01", "ADAY MÜŞTERİ")))
                await new EkstreHesapPlaniService(db, BankaEkstreTestOrtami.Kapsam(Aday)).IceAktarAsync(dosya);

            using (var dosya = HesapPlaniDosyasi(("120 A01", "SMMM MÜŞTERİ")))
            {
                var sonuc = await new EkstreHesapPlaniService(db, BankaEkstreTestOrtami.Kapsam(Smmm))
                    .IceAktarAsync(dosya);

                Assert.Equal(1, sonuc.Eklenen);
                Assert.Equal(0, sonuc.Guncellenen);
            }

            Assert.Equal("ADAY MÜŞTERİ", db.EkstreHesapPlani.Single(h => h.FirmaId == Aday).Ad);
            Assert.Equal("SMMM MÜŞTERİ", db.EkstreHesapPlani.Single(h => h.FirmaId == Smmm).Ad);
        }

        /// <summary>
        /// Kapsamsız yazma engellenir. Modül tenant tarafında tam bu hatayı yaptı: kapsam
        /// belirsizken kayıt yine de bir yere yazılıyordu (bkz. KARARLAR §68).
        /// </summary>
        [Fact]
        public async Task Kapsamsiz_kayit_yazilamaz()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            // FirmaId KASITLI olarak verilmedi: kapsamsız kaydın engellendiği sınanıyor.
            db.EkstreBankaHesaplari.Add(new BankaHesabi
            {
                BankaAdi = "Vakıfbank",
                OrkaHesapKodu = "102 1 1 01",
                ParaBirimi = "TRY"
            });

            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }

        [Fact]
        public async Task Firma_ozeti_her_firmanin_kendi_sayaclarini_dondurur()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            using (var dosya = HesapPlaniDosyasi(("120 A01", "ADAY MÜŞTERİ"), ("120 A02", "ADAY MÜŞTERİ 2")))
                await new EkstreHesapPlaniService(db, BankaEkstreTestOrtami.Kapsam(Aday)).IceAktarAsync(dosya);

            db.EkstreBankaHesaplari.Add(new BankaHesabi
            {
                FirmaId = Aday,
                BankaAdi = "Vakıfbank",
                OrkaHesapKodu = "102 1 1 01",
                ParserTipi = BankaEkstreTestOrtami.ParserTipi
            });
            db.EkstreBankaHesaplari.Add(new BankaHesabi
            {
                FirmaId = Aday,
                BankaAdi = "Ziraat",
                OrkaHesapKodu = "102 2 1 01"
            });

            // SMMM'de yalnız bir banka hesabı var, hesap planı hiç yüklenmemiş.
            db.EkstreBankaHesaplari.Add(new BankaHesabi
            {
                FirmaId = Smmm,
                BankaAdi = "TEB",
                OrkaHesapKodu = "102 3 1 01"
            });

            var yukleme = new EkstreYukleme
            {
                FirmaId = Aday,
                BankaHesabiId = 1,
                DosyaAdi = "ekstre.xlsx",
                SatirSayisi = 3
            };
            db.EkstreYuklemeler.Add(yukleme);
            await db.SaveChangesAsync();

            db.EkstreSatirlari.AddRange(
                Satir(yukleme.Id, SatirDurum.OnayBekliyor),
                Satir(yukleme.Id, SatirDurum.Cozulemedi),
                Satir(yukleme.Id, SatirDurum.Otomatik));
            await db.SaveChangesAsync();

            // Özet birden çok firmayı tek istekte sayar; artık baypas edilecek gizli bir
            // filtre yok, sıradan bir WHERE FirmaId IN (…) sorgusu.
            var ozetler = await new FirmaOzetService(db).OzetlerAsync(new[] { Aday, Smmm, 999 });

            var aday = ozetler.Single(o => o.FirmaId == Aday);
            Assert.Equal(2, aday.HesapPlaniSayisi);
            Assert.Equal(2, aday.BankaHesabiSayisi);
            Assert.Equal(2, aday.OnayBekleyen);   // Otomatik satır sayılmaz

            var smmm = ozetler.Single(o => o.FirmaId == Smmm);
            Assert.Equal(0, smmm.HesapPlaniSayisi);   // "kurulum gerekli"
            Assert.Equal(1, smmm.BankaHesabiSayisi);
            Assert.Equal(0, smmm.OnayBekleyen);

            // Hiç kaydı olmayan firma da satır olarak döner (ekranda boş kalmasın).
            var bos = ozetler.Single(o => o.FirmaId == 999);
            Assert.Equal(0, bos.HesapPlaniSayisi);
        }

        /// <summary>
        /// Temizlik yalnız seçili firmayı siler; diğer firmanın kurulumuna ve global
        /// tablolara dokunmaz.
        /// </summary>
        [Fact]
        public async Task Temizlik_yalniz_secili_firmanin_verisini_siler()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            using (var dosya = HesapPlaniDosyasi(("120 A01", "ADAY MÜŞTERİ")))
                await new EkstreHesapPlaniService(db, BankaEkstreTestOrtami.Kapsam(Aday)).IceAktarAsync(dosya);
            using (var dosya = HesapPlaniDosyasi(("320 S01", "SMMM SATICI")))
                await new EkstreHesapPlaniService(db, BankaEkstreTestOrtami.Kapsam(Smmm)).IceAktarAsync(dosya);

            db.EkstreBankaHesaplari.Add(new BankaHesabi
            {
                FirmaId = Aday,
                BankaAdi = "Vakıfbank",
                OrkaHesapKodu = "102 1 1 01"
            });
            db.EkstreVergiKodlari.Add(new VergiKoduEslemesi { VergiKodu = "0040", HesapKodu = "360 01 004" });
            await db.SaveChangesAsync();

            var servis = new BankaTemizlikService(db, BankaEkstreTestOrtami.Kapsam(Aday));

            var ozet = await servis.OzetAsync();
            Assert.Equal(1, ozet.HesapPlaniKaydi);
            Assert.Equal(1, ozet.BankaHesabi);

            var silinen = await servis.TemizleAsync();
            Assert.Equal(2, silinen.Toplam);

            Assert.Empty(db.EkstreHesapPlani.Where(h => h.FirmaId == Aday).ToList());
            Assert.Empty(db.EkstreBankaHesaplari.Where(h => h.FirmaId == Aday).ToList());

            // Diğer firma ve global tablo yerinde.
            Assert.Single(db.EkstreHesapPlani.Where(h => h.FirmaId == Smmm).ToList());
            Assert.Single(db.EkstreVergiKodlari.ToList());
        }

        private static EkstreSatiri Satir(int yuklemeId, SatirDurum durum) => new()
        {
            EkstreYuklemeId = yuklemeId,
            SiraNo = 1,
            KaynakSatirNo = 8,
            Tarih = new DateTime(2026, 1, 15),
            Yon = Yon.Cikan,
            Tutar = 100m,
            Durum = durum
        };
    }
}
