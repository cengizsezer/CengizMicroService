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
            // Üretim seed'iyle aynı sabit; açıklamanın sonundaki "… Tahsilatı" satıcı adı.
            Yeni(CatalogService.Api.Features.BankaEkstre.BankaEkstreSeed.TahsilatDeseni, 5),
            Yeni(@"sorgu numaralı (.+?) tarafından", 10),
            Yeni(@"nolu ([A-ZÇĞİÖŞÜ0-9][^/]{4,70}?) hesab", 20),
            Yeni(GidenKarsiTarafDeseni, 25),
            Yeni(@"sorgu no'lu \S+ (.+)$", 30),
            Yeni(@"nolu ([A-ZÇĞİÖŞÜ][A-ZÇĞİÖŞÜ0-9.\s&]{4,60})", 40),
            Yeni(@"^([A-ZÇĞİÖŞÜ0-9][^/]{4,60}?)\s*/\s*[A-ZÇĞİÖŞÜ]", 50),
            Yeni(@"NO'LU ([A-ZÇĞİÖŞÜ][^()]{4,70}?) HESABINA", 55),
            Yeni(@"^(.+?)\s*\(", 60)
        };

        /// <summary>
        /// Giden FAST/EFT gövdesinde banka adından sonraki karşı taraf. Üretimdeki seed ile
        /// birebir aynı metin; kaçışları bozulmasın diye ayrı sabitte durur.
        /// </summary>
        public const string GidenKarsiTarafDeseni =
            @"(?:[Hh]esabından|HESABINDAN)\s+[^()]{0,70}?(?<![A-ZÇĞİÖŞÜa-zçğıöşü])A\.?Ş\.?\s*[-–]?\s*([^()]{3,120}?)\s+(?:[Hh]esabına|HESABINA)";

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
            // Açıklamada geçen ifadeye karşılık gelir, işlem tipine değil: "HESAPLAR ARASI
            // E.F.T. VAKIFBANK/DENİZBANK …" satırının işlem tipi "Gelen EFT Otomatik Yatan".
            new() { ParserTipi = ParserTipi, IslemTipiDeseni = "Hesaplar Arası EFT", Sablon = "Hesaplar Arası Eft - {BANKA}", BankalarArasi = true, EslesmeTuru = EslesmeTuru.Icerir, Sira = 45, Aktif = true },
            new() { ParserTipi = ParserTipi, IslemTipiDeseni = "HGS Bakiye Yükle", Sablon = "Hgs Bakiye Yüklemesi - {PLAKA}", Sira = 50, Aktif = true },
            new() { ParserTipi = ParserTipi, IslemTipiDeseni = "MKK Masrafı", Sablon = "Banka Gideri", Sira = 60, Aktif = true },
            new() { ParserTipi = ParserTipi, IslemTipiDeseni = "Vergi Tahsilatı", Sablon = "Vergi Ödemesi - {VERGI}", Sira = 70, Aktif = true }
        };

        /// <summary>
        /// Üretimdeki seed ile aynı sabit kurallar. Açıklama kapsamlı personel avansı
        /// kuralları önce gelir; sıra üretimdeki ile birebir aynı tutuldu ("Maaş Avansı"
        /// genel "Avans" deseninden önce denenmeli).
        /// </summary>
        public static List<SabitKural> SabitKurallar() => new()
        {
            Avans("İş Avansı", "195", "İş Avansları", 10),
            Avans("İş Avans", "195", "İş Avansları", 15),
            Avans("Masraf Ödemesi", "195", "İş Avansları", 20),
            Avans("Maaş Avansı", "196", "Personel Avansları", 30),
            Avans("Avans", "196", "Personel Avansları", 40),
            Kural("MKK Masrafı", "770", "Genel Yönetim Giderleri", 50),
            Kural("Masraf", "770", "Genel Yönetim Giderleri", 60, EslesmeTuru.Icerir),
            Kural("HGS Bakiye Yükle", "740", "Hizmet Üretim Maliyeti", 70)
        };

        private static SabitKural Kural(string desen, string kod, string ad, int sira,
                                        EslesmeTuru tur = EslesmeTuru.Tam) => new()
        {
            ParserTipi = ParserTipi,
            IslemTipiDeseni = desen,
            Kapsam = KuralKapsami.IslemTipi,
            EslesmeTuru = tur,
            HesapKodu = kod,
            HesapAdi = ad,
            Guven = 0.95m,
            Sira = sira,
            Aktif = true
        };

        private static SabitKural Avans(string desen, string kod, string ad, int sira) => new()
        {
            ParserTipi = ParserTipi,
            IslemTipiDeseni = desen,
            Kapsam = KuralKapsami.Aciklama,
            EslesmeTuru = EslesmeTuru.Icerir,
            HesapKodu = kod,
            HesapAdi = ad,
            Guven = 0.95m,
            UnvanCikarilsin = false,
            AltHesapGerekli = true,
            Sira = sira,
            Aktif = true
        };

        // ---- Gerçek dosya ----

        /// <summary>
        /// Depo kökündeki gerçek Vakıfbank ekstresi. Ad kalıpla aranır: dosya adında Türkçe
        /// karakter var ve kaynakta birebir yazmak kodlama farklarına açık.
        /// </summary>
        public static string GercekEkstreYolu()
        {
            var dizin = new DirectoryInfo(AppContext.BaseDirectory);

            while (dizin is not null)
            {
                var eslesenler = dizin.GetFiles("*Hesap_Ekstresi.xlsx");
                if (eslesenler.Length > 0) return eslesenler[0].FullName;

                dizin = dizin.Parent;
            }

            throw new FileNotFoundException(
                "Gerçek Vakıfbank ekstresi bulunamadı (depo kökünde *Hesap_Ekstresi.xlsx bekleniyor).");
        }

        public static FileStream GercekEkstre() => File.OpenRead(GercekEkstreYolu());

        // ---- Örnek xlsx ----

        /// <summary>
        /// Gerçek dosya yapısını taklit eder: 5 satır künye, 6. satır boş, 7. satır kolon
        /// başlıkları, veri 8'den. Başlık metinleri de gerçek dosyadaki gibi tamamı büyük
        /// harf ve Türkçe karakterli yazılır ("AÇIKLAMA", "İŞLEM TARİHİ") — ayrıştırıcının
        /// karşılaştırmayı sadeleştirerek yaptığı burada sınanır.
        /// </summary>
        public static MemoryStream BasliklıEkstre(params object[][] satirlar)
            => Ekstre(basliklarYazilsin: true, satirlar);

        /// <summary>Kolon başlıkları olmayan dosya: parser sabit indekslere düşmeli.</summary>
        public static MemoryStream BasliksizEkstre(params object[][] satirlar)
            => Ekstre(basliklarYazilsin: false, satirlar);

        /// <summary>
        /// HAREKET TARIH kolonuna yazılan sabit değer. Gerçek dosyada bu kolon saat de
        /// içeriyor ve kullanılmaması gerekiyor; ayrıştırıcı yanlışlıkla buraya dönerse
        /// tarih iddiaları görünür biçimde patlasın diye alakasız bir tarih konur.
        /// </summary>
        private const string HareketTarihiTuzagi = "01.01.2000 23:59";

        /// <summary>
        /// Satır dizisi sırası: tarih, işlem tipi, tutar, kanal, vkn, B/A, açıklama.
        /// Kolon yerleşimi gerçek dosyayla aynıdır (1 tabanlı): 3 HAREKET TARIH,
        /// 4 İŞLEM TARİHİ, 6 İŞLEM, 7 TUTAR, 9 KANAL, 15 VKN, 16 B/A, 17 AÇIKLAMA.
        /// </summary>
        private static MemoryStream Ekstre(bool basliklarYazilsin, object[][] satirlar)
        {
            using var kitap = new XLWorkbook();
            var sayfa = kitap.Worksheets.Add("Hesap Hareketleri");

            sayfa.Cell(1, 1).Value = "HESAP SAHİBİ";
            sayfa.Cell(1, 2).Value = "VAKIFBANK";
            sayfa.Cell(2, 1).Value = "VB MÜŞTERİ NO";
            sayfa.Cell(3, 1).Value = "IBAN";
            sayfa.Cell(3, 2).Value = "TR33 0001 5001 5800 7300 1234 56";
            sayfa.Cell(4, 1).Value = "HESAP TÜRÜ";
            sayfa.Cell(5, 1).Value = "ŞUBE";

            if (basliklarYazilsin)
            {
                sayfa.Cell(7, 1).Value = "HESAP NO";
                sayfa.Cell(7, 2).Value = "FİŞ NO";
                sayfa.Cell(7, 3).Value = "HAREKET TARIH";
                sayfa.Cell(7, 4).Value = "İŞLEM TARİHİ";
                sayfa.Cell(7, 5).Value = "KART NO";
                sayfa.Cell(7, 6).Value = "İŞLEM";
                sayfa.Cell(7, 7).Value = "TUTAR";
                sayfa.Cell(7, 8).Value = "BAKİYE";
                sayfa.Cell(7, 9).Value = "KANAL";
                sayfa.Cell(7, 15).Value = "VKN";
                sayfa.Cell(7, 16).Value = "B/A";
                sayfa.Cell(7, 17).Value = "AÇIKLAMA";
            }

            var satirNo = 8;
            foreach (var satir in satirlar)
            {
                sayfa.Cell(satirNo, 3).Value = HareketTarihiTuzagi;
                sayfa.Cell(satirNo, 4).Value = XLCellValue.FromObject(satir[0]);
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
