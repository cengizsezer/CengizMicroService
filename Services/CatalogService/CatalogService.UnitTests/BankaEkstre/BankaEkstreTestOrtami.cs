using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Infrastructure.Accessor;
using CatalogService.Api.Infrastructure.Context;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Banka ekstresi testleri için bellek içi context, örnek yapılandırma satırları ve
    /// gerçek dosya yapısını taklit eden xlsx üretici.
    /// </summary>
    public static class BankaEkstreTestOrtami
    {
        public const string TenantNo = "201";
        public const string ParserTipi = "VAKIFBANK_VADESIZ";

        /// <summary>
        /// Bellek içi context. Aynı <paramref name="veritabaniAdi"/> ile farklı
        /// <paramref name="tenantNo"/> vererek iki firmanın aynı veritabanını paylaştığı
        /// izolasyon senaryosu kurulabilir.
        /// </summary>
        public static CatalogContext YeniContext(string? veritabaniAdi = null, string? tenantNo = null)
        {
            var options = new DbContextOptionsBuilder<CatalogContext>()
                .UseInMemoryDatabase(veritabaniAdi ?? $"banka-ekstre-{Guid.NewGuid()}")
                .Options;

            return new CatalogContext(options, new FixedTenantAccessor(tenantNo ?? TenantNo));
        }

        // ---- Yapılandırma satırları (üretimdeki seed ile aynı içerik) ----

        public static List<UnvanDeseni> Desenler() => new()
        {
            Yeni(@"sorgu numaralı (.+?) tarafından", 10),
            Yeni(@"nolu ([A-ZÇĞİÖŞÜ0-9][^/]{4,70}?) hesab", 20),
            Yeni(@"sorgu no'lu \S+ (.+)$", 30),
            Yeni(@"nolu ([A-ZÇĞİÖŞÜ][A-ZÇĞİÖŞÜ0-9.\s&]{4,60})", 40),
            Yeni(@"^([A-ZÇĞİÖŞÜ0-9][^/]{4,60}?)\s*/\s*[A-ZÇĞİÖŞÜ]", 50),
            Yeni(@"^(.+?)\s*\(", 60)
        };

        private static UnvanDeseni Yeni(string desen, int sira) => new()
        {
            ParserTipi = ParserTipi,
            Desen = desen,
            GrupNo = 1,
            Sira = sira,
            Aktif = true
        };

        public static List<AciklamaSablonu> Sablonlar() => new()
        {
            new() { ParserTipi = ParserTipi, IslemTipiDeseni = "Gelen EFT Otomatik Yatan", Sablon = "Gelen Eft - {UNVAN}", Sira = 10, Aktif = true },
            new() { ParserTipi = ParserTipi, IslemTipiDeseni = "Gönderilen havale", Sablon = "Giden Eft - {UNVAN}", Sira = 20, Aktif = true },
            new() { ParserTipi = ParserTipi, IslemTipiDeseni = "Otomatik Süpürme İşlemleri Virman", Sablon = "Otomatik Süpürme Pkf Aday", BankalarArasi = true, Sira = 30, Aktif = true },
            new() { ParserTipi = ParserTipi, IslemTipiDeseni = "Virman", Sablon = "Hesaplararası Virman - {HESAP}", BankalarArasi = true, Sira = 40, Aktif = true },
            new() { ParserTipi = ParserTipi, IslemTipiDeseni = "HGS Bakiye Yükle", Sablon = "Hgs Bakiye Yüklemesi - {PLAKA}", Sira = 50, Aktif = true },
            new() { ParserTipi = ParserTipi, IslemTipiDeseni = "MKK Masrafı", Sablon = "Banka Gideri", Sira = 60, Aktif = true },
            new() { ParserTipi = ParserTipi, IslemTipiDeseni = "Vergi Tahsilatı", Sablon = "Vergi Ödemesi - {VERGI}", Sira = 70, Aktif = true }
        };

        // ---- Örnek xlsx ----

        /// <summary>Ölçülen dosya yapısını taklit eder: 6 satır başlık bloğu, 7. satır kolon başlıkları, veri 8'den.</summary>
        public static MemoryStream BasliklıEkstre(params object[][] satirlar)
            => Ekstre(basliklarYazilsin: true, satirlar);

        /// <summary>Kolon başlıkları olmayan dosya: parser sabit indekslere düşmeli.</summary>
        public static MemoryStream BasliksizEkstre(params object[][] satirlar)
            => Ekstre(basliklarYazilsin: false, satirlar);

        /// <summary>
        /// Satır dizisi sırası: tarih, işlem tipi, tutar, kanal, vkn, B/A, açıklama.
        /// Kolon yerleşimi ölçülen 0 tabanlı indekslerle aynıdır (2,5,6,8,14,15,16).
        /// </summary>
        private static MemoryStream Ekstre(bool basliklarYazilsin, object[][] satirlar)
        {
            using var kitap = new XLWorkbook();
            var sayfa = kitap.Worksheets.Add("Hesap Hareketleri");

            sayfa.Cell(1, 1).Value = "VAKIFBANK";
            sayfa.Cell(2, 1).Value = "Hesap Hareketleri Raporu";
            sayfa.Cell(3, 1).Value = "IBAN: TR33 0001 5001 5800 7300 1234 56";

            if (basliklarYazilsin)
            {
                sayfa.Cell(7, 3).Value = "Tarih";
                sayfa.Cell(7, 6).Value = "İşlem Tipi";
                sayfa.Cell(7, 7).Value = "Tutar";
                sayfa.Cell(7, 9).Value = "Kanal";
                sayfa.Cell(7, 15).Value = "VKN";
                sayfa.Cell(7, 16).Value = "B/A";
                sayfa.Cell(7, 17).Value = "Açıklama";
            }

            var satirNo = 8;
            foreach (var satir in satirlar)
            {
                sayfa.Cell(satirNo, 3).Value = XLCellValue.FromObject(satir[0]);
                sayfa.Cell(satirNo, 6).Value = XLCellValue.FromObject(satir[1]);
                sayfa.Cell(satirNo, 7).Value = XLCellValue.FromObject(satir[2]);
                sayfa.Cell(satirNo, 9).Value = XLCellValue.FromObject(satir[3]);
                sayfa.Cell(satirNo, 15).Value = XLCellValue.FromObject(satir[4]);
                sayfa.Cell(satirNo, 16).Value = XLCellValue.FromObject(satir[5]);
                sayfa.Cell(satirNo, 17).Value = XLCellValue.FromObject(satir[6]);
                satirNo++;
            }

            var akis = new MemoryStream();
            kitap.SaveAs(akis);
            akis.Position = 0;
            return akis;
        }
    }
}
