using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Banka Otomasyon firma seçim ekranının dayandığı iki iddia:
    ///
    /// 1. Seçilen firma gerçekten tenant bağlamıdır — Aday seçiliyken yapılan hesap planı
    ///    içe aktarımı Aday'ın kayıtlarına yazılır, SMMM'ninkilere değil.
    /// 2. Seçim ekranı, girilmemiş firmaların sayaçlarını da doğru gösterir.
    ///
    /// İki firma aynı veritabanını paylaşır (<c>YeniContext(ad, tenantNo)</c>); gerçek
    /// kurulumda da tek veritabanı vardır, izolasyon yalnız TenantNo ile sağlanır.
    /// </summary>
    public class FirmaTenantIzolasyonuTests
    {
        private const string Aday = "201";
        private const string Smmm = "106";

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
            var veritabani = $"firma-secim-{Guid.NewGuid()}";

            // SMMM'de önceden bir hesap planı var; Aday'ın aktarımı buna dokunmamalı.
            using (var db = BankaEkstreTestOrtami.YeniContext(veritabani, Smmm))
            {
                using var dosya = HesapPlaniDosyasi(("320 S01", "SMMM SATICI"));
                await new EkstreHesapPlaniService(db).IceAktarAsync(dosya);
            }

            // Ekrandaki seçim Aday: istek Aday tenant'ıyla gider.
            using (var db = BankaEkstreTestOrtami.YeniContext(veritabani, Aday))
            {
                using var dosya = HesapPlaniDosyasi(("120 A01", "ADAY MÜŞTERİ"), ("120 A02", "ADAY MÜŞTERİ 2"));
                var sonuc = await new EkstreHesapPlaniService(db).IceAktarAsync(dosya);

                Assert.Equal(2, sonuc.Eklenen);
            }

            // Kayıtlar Aday'a yazıldı mı, SMMM'ninkiler bozulmadı mı?
            using (var db = BankaEkstreTestOrtami.YeniContext(veritabani, Aday))
            {
                var kodlar = await db.EkstreHesapPlani.AsNoTracking()
                    .Select(h => h.Kod).OrderBy(k => k).ToListAsync();

                Assert.Equal(new[] { "120 A01", "120 A02" }, kodlar);
                Assert.All(await db.EkstreHesapPlani.AsNoTracking().ToListAsync(),
                           h => Assert.Equal(Aday, h.TenantNo));
            }

            using (var db = BankaEkstreTestOrtami.YeniContext(veritabani, Smmm))
            {
                var kodlar = await db.EkstreHesapPlani.AsNoTracking().Select(h => h.Kod).ToListAsync();
                Assert.Equal(new[] { "320 S01" }, kodlar);
            }
        }

        [Fact]
        public async Task Firma_ozeti_her_firmanin_kendi_sayaclarini_dondurur()
        {
            var veritabani = $"firma-ozet-{Guid.NewGuid()}";

            using (var db = BankaEkstreTestOrtami.YeniContext(veritabani, Aday))
            {
                using var dosya = HesapPlaniDosyasi(("120 A01", "ADAY MÜŞTERİ"), ("120 A02", "ADAY MÜŞTERİ 2"));
                await new EkstreHesapPlaniService(db).IceAktarAsync(dosya);

                db.EkstreBankaHesaplari.Add(new BankaHesabi
                {
                    BankaAdi = "Vakıfbank",
                    OrkaHesapKodu = "102 1 1 01",
                    ParserTipi = BankaEkstreTestOrtami.ParserTipi
                });
                db.EkstreBankaHesaplari.Add(new BankaHesabi { BankaAdi = "Ziraat", OrkaHesapKodu = "102 2 1 01" });

                var yukleme = new EkstreYukleme { BankaHesabiId = 1, DosyaAdi = "ekstre.xlsx", SatirSayisi = 3 };
                db.EkstreYuklemeler.Add(yukleme);
                await db.SaveChangesAsync();

                db.EkstreSatirlari.AddRange(
                    Satir(yukleme.Id, SatirDurum.OnayBekliyor),
                    Satir(yukleme.Id, SatirDurum.Cozulemedi),
                    Satir(yukleme.Id, SatirDurum.Otomatik));
                await db.SaveChangesAsync();
            }

            // SMMM'de yalnız bir banka hesabı var, hesap planı hiç yüklenmemiş.
            using (var db = BankaEkstreTestOrtami.YeniContext(veritabani, Smmm))
            {
                db.EkstreBankaHesaplari.Add(new BankaHesabi { BankaAdi = "TEB", OrkaHesapKodu = "102 3 1 01" });
                await db.SaveChangesAsync();
            }

            // Özet, hangi tenant'ın token'ıyla sorulursa sorulsun ikisini de görmeli:
            // ekran firmaya girilmeden önce açılıyor.
            using (var db = BankaEkstreTestOrtami.YeniContext(veritabani, Smmm))
            {
                var ozetler = await new FirmaOzetService(db).OzetlerAsync(new[] { Aday, Smmm, "999" });

                var aday = ozetler.Single(o => o.TenantNo == Aday);
                Assert.Equal(2, aday.HesapPlaniSayisi);
                Assert.Equal(2, aday.BankaHesabiSayisi);
                Assert.Equal(2, aday.OnayBekleyen);   // Otomatik satır sayılmaz

                var smmm = ozetler.Single(o => o.TenantNo == Smmm);
                Assert.Equal(0, smmm.HesapPlaniSayisi);   // "kurulum gerekli"
                Assert.Equal(1, smmm.BankaHesabiSayisi);
                Assert.Equal(0, smmm.OnayBekleyen);

                // Hiç kaydı olmayan firma da satır olarak döner (ekranda boş kalmasın).
                var bos = ozetler.Single(o => o.TenantNo == "999");
                Assert.Equal(0, bos.HesapPlaniSayisi);
            }
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
