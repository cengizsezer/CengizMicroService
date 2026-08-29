using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;

namespace CatalogService.Api.Features.BankaEkstre.Services.Parsing
{
    /// <summary>
    /// Ekstre dosyasını okuyucudan bağımsız bir tabloya çevirir.
    ///
    /// Tek bir kütüphane üç bankayı da okuyamıyor:
    /// <list type="bullet">
    /// <item>İş Bankası dosyası <b>eski .xls</b> (OLE2 kabı); ClosedXML bu biçimi hiç açmaz,
    /// NPOI/HSSF gerekir.</item>
    /// <item>Akbank .xlsx'i bazı okuyucularda hatasız açılıp <b>tek hücre</b> görünüyor;
    /// hata fırlatmadığı için "başarılı" sayılıp sessizce boş ekstre üretme riski var.</item>
    /// <item>Ziraat .xlsx'inin <c>styles.xml</c>'i bozuk; biçim tablosunu okuyan her
    /// kütüphane patlıyor, değerler ise sağlam (bkz. <see cref="HamXlsxOkuyucu"/>).</item>
    /// </list>
    ///
    /// Bu yüzden dosyanın <b>imzasına</b> bakılır ve xlsx tarafında okuyucular sırayla
    /// denenir. Yedek yola düşüldüyse <see cref="EkstreParseSonuc.Uyarilar"/>'a hangi
    /// okuyucunun neden başarısız olduğu yazılır — sessizce düşülmez.
    /// </summary>
    public static class EkstreTabloOkuyucu
    {
        /// <summary>OLE2 bileşik belge imzası: eski .xls (BIFF8).</summary>
        private static readonly byte[] Ole2Imzasi = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };

        /// <summary>Zip imzası: xlsx (ve xlsm).</summary>
        private static readonly byte[] ZipImzasi = { 0x50, 0x4B, 0x03, 0x04 };

        static EkstreTabloOkuyucu()
        {
            // Eski .xls dosyaları metni Windows-1254 gibi kod sayfalarıyla saklıyor; .NET 8
            // bu kodlamaları varsayılan olarak tanımıyor ve NPOI okuma anında patlıyor.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public static EkstreTablosu Oku(Stream dosya, EkstreParseSonuc sonuc)
        {
            var icerik = Baytlar(dosya);

            if (ImzaUyuyorMu(icerik, Ole2Imzasi))
                return HssfOku(icerik);

            if (!ImzaUyuyorMu(icerik, ZipImzasi))
                throw new InvalidDataException(
                    "Dosya biçimi tanınmadı: ne eski Excel (.xls) ne de xlsx imzası var. " +
                    "Ekstre bankadan Excel olarak indirilmiş mi?");

            // xlsx: en zengin okuyucudan en dayanıklıya. ClosedXML biçim bilgisini de okur
            // (tarih hücreleri hazır gelir), ham XML yolu yalnız değerleri alır.
            var denemeler = new (string Ad, Func<byte[], EkstreTablosu> Oku)[]
            {
                ("ClosedXML", ClosedXmlOku),
                ("NPOI XSSF", XssfOku),
                (HamXlsxOkuyucu.OkuyucuAdi, HamOku)
            };

            var hatalar = new List<string>();

            foreach (var (ad, oku) in denemeler)
            {
                try
                {
                    var tablo = oku(icerik);

                    if (!tablo.Kullanilabilir)
                    {
                        hatalar.Add($"{ad}: dosya açıldı ama satırlarda birden fazla dolu hücre yok.");
                        continue;
                    }

                    if (hatalar.Count > 0)
                        sonuc.Uyarilar.Add(
                            $"Dosya '{ad}' ile okundu; önceki okuyucular başarısız oldu: " +
                            string.Join(" ", hatalar));

                    return tablo;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    hatalar.Add($"{ad}: {ex.GetType().Name} — {Kisalt(ex.Message)}");
                }
            }

            throw new InvalidDataException(
                "xlsx dosyası hiçbir okuyucuyla okunamadı. Denenenler: " + string.Join(" ", hatalar));
        }

        // ---- Okuyucular ----

        private static EkstreTablosu ClosedXmlOku(byte[] icerik)
        {
            using var akis = new MemoryStream(icerik, writable: false);
            using var kitap = new XLWorkbook(akis);

            var sayfa = kitap.Worksheets.FirstOrDefault()
                        ?? throw new InvalidDataException("Excel dosyasında sayfa bulunamadı.");

            var sonSatir = sayfa.LastRowUsed()?.RowNumber() ?? 0;
            var sonKolon = sayfa.LastColumnUsed()?.ColumnNumber() ?? 0;

            var satirlar = new List<TabloSatiri>(sonSatir);

            for (var satirNo = 1; satirNo <= sonSatir; satirNo++)
            {
                var satir = sayfa.Row(satirNo);
                var hucreler = new List<TabloHucresi>(sonKolon);

                for (var kolon = 1; kolon <= sonKolon; kolon++)
                {
                    var hucre = satir.Cell(kolon);

                    double? sayi = hucre.DataType == XLDataType.Number && hucre.TryGetValue<double>(out var d) ? d : null;
                    DateTime? tarih = hucre.DataType == XLDataType.DateTime && hucre.TryGetValue<DateTime>(out var t) ? t : null;

                    hucreler.Add(new TabloHucresi(hucre.GetString(), sayi, tarih));
                }

                satirlar.Add(new TabloSatiri(satirNo, hucreler));
            }

            return new EkstreTablosu(satirlar, "ClosedXML");
        }

        private static EkstreTablosu HssfOku(byte[] icerik)
        {
            using var akis = new MemoryStream(icerik, writable: false);
            using var kitap = new HSSFWorkbook(akis);
            return NpoiOku(kitap, "NPOI HSSF");
        }

        private static EkstreTablosu XssfOku(byte[] icerik)
        {
            using var akis = new MemoryStream(icerik, writable: false);
            using var kitap = new NPOI.XSSF.UserModel.XSSFWorkbook(akis);
            return NpoiOku(kitap, "NPOI XSSF");
        }

        private static EkstreTablosu HamOku(byte[] icerik)
        {
            using var akis = new MemoryStream(icerik, writable: false);
            return HamXlsxOkuyucu.Oku(akis);
        }

        private static EkstreTablosu NpoiOku(IWorkbook kitap, string okuyucu)
        {
            if (kitap.NumberOfSheets == 0)
                throw new InvalidDataException("Excel dosyasında sayfa bulunamadı.");

            var sayfa = kitap.GetSheetAt(0);
            var satirlar = new List<TabloSatiri>(sayfa.LastRowNum + 1);

            // NPOI satır/kolon numaraları 0 tabanlı; tablo modeli 1 tabanlı Excel numarası tutar.
            for (var i = 0; i <= sayfa.LastRowNum; i++)
            {
                var satir = sayfa.GetRow(i);
                if (satir is null)
                {
                    satirlar.Add(new TabloSatiri(i + 1, Array.Empty<TabloHucresi>()));
                    continue;
                }

                // LastCellNum short ve boş satırda -1 dönebiliyor.
                int sonHucre = satir.LastCellNum;
                var hucreler = new List<TabloHucresi>(Math.Max(sonHucre, 0));
                for (var k = 0; k < sonHucre; k++)
                    hucreler.Add(HucreOku(satir.GetCell(k)));

                satirlar.Add(new TabloSatiri(i + 1, hucreler));
            }

            return new EkstreTablosu(satirlar, okuyucu);
        }

        private static TabloHucresi HucreOku(ICell? hucre)
        {
            if (hucre is null) return TabloHucresi.Bos;

            var tip = hucre.CellType == CellType.Formula ? hucre.CachedFormulaResultType : hucre.CellType;

            switch (tip)
            {
                case CellType.Numeric:
                    if (DateUtil.IsCellDateFormatted(hucre))
                    {
                        var tarih = hucre.DateCellValue;
                        return new TabloHucresi(tarih?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), null, tarih);
                    }

                    var sayi = hucre.NumericCellValue;
                    return new TabloHucresi(sayi.ToString(CultureInfo.InvariantCulture), sayi, null);

                case CellType.String:
                    return new TabloHucresi(hucre.StringCellValue, null, null);

                case CellType.Boolean:
                    return new TabloHucresi(hucre.BooleanCellValue ? "TRUE" : "FALSE", null, null);

                default:
                    return TabloHucresi.Bos;
            }
        }

        // ---- Yardımcılar ----

        private static byte[] Baytlar(Stream dosya)
        {
            if (dosya is MemoryStream bellek && bellek.TryGetBuffer(out var arabellek))
                return arabellek.AsSpan(0, (int)bellek.Length).ToArray();

            if (dosya.CanSeek) dosya.Position = 0;

            using var kopya = new MemoryStream();
            dosya.CopyTo(kopya);
            return kopya.ToArray();
        }

        private static bool ImzaUyuyorMu(byte[] icerik, byte[] imza)
        {
            if (icerik.Length < imza.Length) return false;

            for (var i = 0; i < imza.Length; i++)
                if (icerik[i] != imza[i]) return false;

            return true;
        }

        private static string Kisalt(string? mesaj)
        {
            if (string.IsNullOrWhiteSpace(mesaj)) return "(mesaj yok)";
            var tek = mesaj.Replace(Environment.NewLine, " ").Replace('\n', ' ').Trim();
            return tek.Length <= 160 ? tek : tek[..160] + "…";
        }
    }
}
