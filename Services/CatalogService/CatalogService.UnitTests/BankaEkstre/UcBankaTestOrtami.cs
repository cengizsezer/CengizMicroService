using System.IO.Compression;
using System.Text;
using CatalogService.Api.Features.BankaEkstre;
using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using ClosedXML.Excel;
using NPOI.HSSF.UserModel;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// İş Bankası, Akbank ve Ziraat ayrıştırıcılarının test ortamı: üç bankanın gerçek
    /// dosya yapısını (başlık satırı, kolon yerleşimi, dosya biçimi) taklit eden üretici
    /// ve üretimdeki seed ile aynı yapılandırma satırları.
    ///
    /// <b>Ham açıklamalar gerçek 7 aylık ekstrelerden birebir alınmıştır</b> — kalıpları
    /// uyduran bir test, deseni de uydurulmuş olana göre doğrular.
    ///
    /// Dosyaların kendileri depoda değil (Vakıfbank ekstresinin aksine); bu yüzden ölçülen
    /// satır sayıları (İş Bankası 418, Akbank 186, Ziraat 356) burada doğrulanamıyor.
    /// Doğrulanan şey yapının kendisi: başlığın isimle bulunması, kolon yerleşimi, dosya
    /// biçimi ve veri olmayan satırların atlanması.
    /// </summary>
    public static class UcBankaTestOrtami
    {
        public const string HesapSahibi = "PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ";

        // ---- İş Bankası (.xls) ----

        /// <summary>
        /// Gerçek yapı: 1–15. satırlar hesap künyesi, <b>16. satır başlıklar</b>, veri 17'den.
        /// Kolonlar (1 tabanlı): 1 Tarih/Saat, 2 Valör, 3 Kanal/Şube, 4 İşlem Tutarı,
        /// 5 Bakiye, 7 İşlem, 8 İşlem Tipi, 9 Açıklama, 15 Referans.
        ///
        /// Dosya <b>eski .xls</b> biçiminde üretilir (NPOI/HSSF): ayrıştırıcının OLE2 kabını
        /// gerçekten açtığı ancak gerçek bir .xls ile sınanabilir.
        /// </summary>
        /// <param name="satirlar">tarih/saat, kanal, tutar (işaretli), işlem tipi, açıklama, referans</param>
        public static MemoryStream IsBankasiEkstresi(params object?[][] satirlar)
            => IsBankasiEkstresi(basliklarYazilsin: true, satirlar);

        /// <summary>Başlıksız dosya: ayrıştırıcı ölçülen sabit indekslere düşmeli.</summary>
        public static MemoryStream IsBankasiBasliksizEkstre(params object?[][] satirlar)
            => IsBankasiEkstresi(basliklarYazilsin: false, satirlar);

        private static MemoryStream IsBankasiEkstresi(bool basliklarYazilsin, object?[][] satirlar)
        {
            var kitap = new HSSFWorkbook();
            var sayfa = kitap.CreateSheet("Hesap Hareketleri");

            // Künye: gerçek dosyada 15 satır sürüyor.
            sayfa.CreateRow(0).CreateCell(0).SetCellValue("TÜRKİYE İŞ BANKASI A.Ş.");
            sayfa.CreateRow(2).CreateCell(0).SetCellValue("HESAP SAHİBİ");
            sayfa.GetRow(2).CreateCell(1).SetCellValue(HesapSahibi);
            sayfa.CreateRow(4).CreateCell(0).SetCellValue("IBAN");
            sayfa.GetRow(4).CreateCell(1).SetCellValue("TR31 0006 4000 0011 0083 3996 63");

            if (basliklarYazilsin)
            {
                var baslik = sayfa.CreateRow(15);
                baslik.CreateCell(0).SetCellValue("Tarih/Saat");
                baslik.CreateCell(1).SetCellValue("Valör");
                baslik.CreateCell(2).SetCellValue("Kanal/Şube");
                baslik.CreateCell(3).SetCellValue("İşlem Tutarı");
                baslik.CreateCell(4).SetCellValue("Bakiye");
                baslik.CreateCell(6).SetCellValue("İşlem");
                baslik.CreateCell(7).SetCellValue("İşlem Tipi");
                baslik.CreateCell(8).SetCellValue("Açıklama");
                baslik.CreateCell(14).SetCellValue("Referans");
            }

            var satirNo = 16;
            foreach (var satir in satirlar)
            {
                var veri = sayfa.CreateRow(satirNo++);
                Yaz(veri, 0, satir[0]);
                veri.CreateCell(1).SetCellValue("26/08/2026");
                Yaz(veri, 2, satir[1]);
                Yaz(veri, 3, satir[2]);
                veri.CreateCell(6).SetCellValue("E9");
                Yaz(veri, 7, satir[3]);
                Yaz(veri, 8, satir[4]);
                Yaz(veri, 14, satir[5]);
            }

            var akis = new MemoryStream();
            kitap.Write(akis, leaveOpen: true);
            akis.Position = 0;
            return akis;
        }

        private static void Yaz(NPOI.SS.UserModel.IRow satir, int kolon, object? deger)
        {
            if (deger is null) return;

            var hucre = satir.CreateCell(kolon);
            switch (deger)
            {
                case decimal d: hucre.SetCellValue((double)d); break;
                case double d: hucre.SetCellValue(d); break;
                case int i: hucre.SetCellValue(i); break;
                default: hucre.SetCellValue(deger.ToString()); break;
            }
        }

        // ---- Akbank (.xlsx) ----

        /// <summary>
        /// Gerçek yapı: <b>10. satır başlıklar</b>, veri 11'den. Kolonlar (1 tabanlı):
        /// 1 Tarih, 2 Saat, 3 Tutar, 4 Bakiye, 5 Borç/Alacak, 6 Açıklama, 7 Fiş/Dekont No.
        /// </summary>
        /// <param name="satirlar">tarih, tutar (işaretli), borç/alacak, açıklama, fiş no</param>
        public static MemoryStream AkbankEkstresi(params object?[][] satirlar)
        {
            using var kitap = new XLWorkbook();
            var sayfa = kitap.Worksheets.Add("Hesap Hareketleri");

            sayfa.Cell(1, 1).Value = "AKBANK T.A.Ş.";
            sayfa.Cell(3, 1).Value = "HESAP SAHİBİ";
            sayfa.Cell(3, 2).Value = HesapSahibi;
            sayfa.Cell(5, 1).Value = "IBAN";

            sayfa.Cell(10, 1).Value = "Tarih";
            sayfa.Cell(10, 2).Value = "Saat";
            sayfa.Cell(10, 3).Value = "Tutar";
            sayfa.Cell(10, 4).Value = "Bakiye";
            sayfa.Cell(10, 5).Value = "Borç/Alacak";
            sayfa.Cell(10, 6).Value = "Açıklama";
            sayfa.Cell(10, 7).Value = "Fiş/Dekont No";

            var satirNo = 11;
            foreach (var satir in satirlar)
            {
                sayfa.Cell(satirNo, 1).Value = XLCellValue.FromObject(satir[0]);
                sayfa.Cell(satirNo, 2).Value = "10:21";
                sayfa.Cell(satirNo, 3).Value = XLCellValue.FromObject(satir[1]);
                sayfa.Cell(satirNo, 5).Value = XLCellValue.FromObject(satir[2]);
                sayfa.Cell(satirNo, 6).Value = XLCellValue.FromObject(satir[3]);
                sayfa.Cell(satirNo, 7).Value = XLCellValue.FromObject(satir[4]);
                satirNo++;
            }

            var akis = new MemoryStream();
            kitap.SaveAs(akis);
            akis.Position = 0;
            return akis;
        }

        // ---- Ziraat (.xlsx) ----

        /// <summary>
        /// Gerçek yapı: <b>12. satır başlıklar</b>, veri 13'ten. Kolonlar (1 tabanlı):
        /// 1 Tarih, 2 Fiş No, 3 Açıklama, 4 İşlem Tutarı, 5 Bakiye.
        /// </summary>
        /// <param name="satirlar">tarih, fiş no, açıklama, tutar (işaretli)</param>
        public static MemoryStream ZiraatEkstresi(params object?[][] satirlar)
        {
            using var kitap = new XLWorkbook();
            var sayfa = kitap.Worksheets.Add("Hesap Hareketleri");

            sayfa.Cell(1, 1).Value = "T.C. ZİRAAT BANKASI A.Ş.";
            sayfa.Cell(3, 1).Value = "HESAP SAHİBİ";
            sayfa.Cell(3, 2).Value = HesapSahibi;

            sayfa.Cell(12, 1).Value = "Tarih";
            sayfa.Cell(12, 2).Value = "Fiş No";
            sayfa.Cell(12, 3).Value = "Açıklama";
            sayfa.Cell(12, 4).Value = "İşlem Tutarı";
            sayfa.Cell(12, 5).Value = "Bakiye";

            var satirNo = 13;
            foreach (var satir in satirlar)
            {
                sayfa.Cell(satirNo, 1).Value = XLCellValue.FromObject(satir[0]);
                sayfa.Cell(satirNo, 2).Value = XLCellValue.FromObject(satir[1]);
                sayfa.Cell(satirNo, 3).Value = XLCellValue.FromObject(satir[2]);
                sayfa.Cell(satirNo, 4).Value = XLCellValue.FromObject(satir[3]);
                satirNo++;
            }

            var akis = new MemoryStream();
            kitap.SaveAs(akis);
            akis.Position = 0;
            return akis;
        }

        /// <summary>
        /// Ziraat dosyasının hastalığını taklit eder: <c>xl/styles.xml</c> okunamaz hâle
        /// getirilir (kapanmamış etiket). Hücre değerleri sağlam kalır — gerçek dosyada da
        /// bozuk olan yalnız stil tablosu.
        /// </summary>
        public static MemoryStream StilTablosuBozuk(MemoryStream xlsx)
        {
            var kopya = new MemoryStream();
            xlsx.Position = 0;
            xlsx.CopyTo(kopya);

            using (var arsiv = new ZipArchive(kopya, ZipArchiveMode.Update, leaveOpen: true))
            {
                arsiv.GetEntry("xl/styles.xml")?.Delete();

                var girdi = arsiv.CreateEntry("xl/styles.xml");
                using var akis = girdi.Open();
                var bozuk = Encoding.UTF8.GetBytes(
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?><styleSheet><fills><fill><patternFill");
                akis.Write(bozuk, 0, bozuk.Length);
            }

            kopya.Position = 0;
            return kopya;
        }

        // ---- Yapılandırma satırları (üretimdeki seed ile aynı içerik) ----

        public static List<UnvanDeseni> IsBankasiDesenleri() => new()
        {
            Desen(IsBankasiVadesizParser.Tip, BankaEkstreSeed.IsBankasiBastakiUnvan, 10),
            Desen(IsBankasiVadesizParser.Tip, BankaEkstreSeed.IsBankasiSondakiUnvan, 20),
            Desen(IsBankasiVadesizParser.Tip, BankaEkstreSeed.IsBankasiBastakiAlan, 30)
        };

        public static List<UnvanDeseni> AkbankDesenleri() => new()
        {
            Desen(AkbankVadesizParser.Tip, BankaEkstreSeed.AkbankBankalarArasi, 10),
            Desen(AkbankVadesizParser.Tip, BankaEkstreSeed.AkbankMobilUnvan, 20),
            Desen(AkbankVadesizParser.Tip, BankaEkstreSeed.AkbankDekontUnvan, 30)
        };

        public static List<UnvanDeseni> ZiraatDesenleri() => new()
        {
            Desen(ZiraatVadesizParser.Tip, BankaEkstreSeed.ZiraatBankalarArasi, 10),
            Desen(ZiraatVadesizParser.Tip, BankaEkstreSeed.ZiraatIbanSonrasiUnvan, 20),
            Desen(ZiraatVadesizParser.Tip, BankaEkstreSeed.ZiraatSondakiUnvan, 30)
        };

        private static UnvanDeseni Desen(string parser, string desen, int sira) => new()
        {
            ParserTipi = parser,
            Desen = desen,
            GrupNo = 1,
            Sira = sira,
            Aktif = true
        };

        /// <summary>Akbank şablonları; üretimdeki seed ile aynı sıra ve eşleşme türü.</summary>
        public static List<AciklamaSablonu> AkbankSablonlari() => new()
        {
            Sablon(AkbankVadesizParser.Tip, "HESAPLAR ARASI EFT", "Hesaplar Arası Eft - {BANKA}", 10, bankalarArasi: true),
            Sablon(AkbankVadesizParser.Tip, "VADELİ HESABA TRANSFER", "Vadeli Hesaba Transfer", 20, bankalarArasi: true),
            Sablon(AkbankVadesizParser.Tip, "HESAP AÇILIŞI", "Vadeli Hesap Açılışı", 30, bankalarArasi: true),
            Sablon(AkbankVadesizParser.Tip, "KISMİ ÖDEME", "Hesaplararası Virman - {HESAP}", 40, bankalarArasi: true),
            Sablon(AkbankVadesizParser.Tip, "VİRMAN", "Hesaplararası Virman - {HESAP}", 50, bankalarArasi: true),
            Sablon(AkbankVadesizParser.Tip, "Kredi Kartı Ödemesi", "Kredi Kartı Borç Ödemesi", 60),
            Sablon(AkbankVadesizParser.Tip, "DBS ODM", "Dbs Ödemesi - {UNVAN}", 70),
            Sablon(AkbankVadesizParser.Tip, "FATURA ÖDEME", "Fatura Ödemesi - {UNVAN}", 80),
            Sablon(AkbankVadesizParser.Tip, "Artı Para Faizi", "Finansman Gideri", 90)
        };

        /// <summary>İş Bankası şablonları; işlem tipi kolonuna TAM eşleşmeyle bağlı.</summary>
        public static List<AciklamaSablonu> IsBankasiSablonlari() => new()
        {
            Sablon(IsBankasiVadesizParser.Tip, "EFT", "{YON} Eft - {UNVAN}", 10, tur: EslesmeTuru.Tam),
            Sablon(IsBankasiVadesizParser.Tip, "FAST", "{YON} Eft - {UNVAN}", 20, tur: EslesmeTuru.Tam),
            Sablon(IsBankasiVadesizParser.Tip, "Havale", "{YON} Eft - {UNVAN}", 30, tur: EslesmeTuru.Tam),
            Sablon(IsBankasiVadesizParser.Tip, "Kredi", "Kredi No: {KREDI}", 40, tur: EslesmeTuru.Tam),
            Sablon(IsBankasiVadesizParser.Tip, "Ücret", "Banka Gideri", 50, tur: EslesmeTuru.Tam)
        };

        private static AciklamaSablonu Sablon(string parser, string desen, string sablon, int sira,
                                              bool bankalarArasi = false, EslesmeTuru tur = EslesmeTuru.Icerir) => new()
        {
            ParserTipi = parser,
            IslemTipiDeseni = desen,
            EslesmeTuru = tur,
            Sablon = sablon,
            BankalarArasi = bankalarArasi,
            Sira = sira,
            Aktif = true
        };

        /// <summary>Akbank sabit kuralları; hepsi açıklama kapsamlı (işlem tipi kolonu yok).</summary>
        public static List<SabitKural> AkbankKurallari() => new()
        {
            Kural(AkbankVadesizParser.Tip, "DBS ODM", "329", "Satıcılar", 10, altHesapGerekli: true),
            Kural(AkbankVadesizParser.Tip, "FATURA ÖDEME", "329", "Satıcılar", 20, altHesapGerekli: true),
            Kural(AkbankVadesizParser.Tip, "Artı Para Faizi", "780", "Finansman Giderleri", 30),
            Kural(AkbankVadesizParser.Tip, "Kredi Kartı Ödemesi", "309", "Kredi Kartları", 40)
        };

        private static SabitKural Kural(string parser, string desen, string kod, string ad, int sira,
                                        bool altHesapGerekli = false) => new()
        {
            ParserTipi = parser,
            IslemTipiDeseni = desen,
            Kapsam = KuralKapsami.Aciklama,
            EslesmeTuru = EslesmeTuru.Icerir,
            HesapKodu = kod,
            HesapAdi = ad,
            Guven = 0.95m,
            AltHesapGerekli = altHesapGerekli,
            Sira = sira,
            Aktif = true
        };
    }
}
