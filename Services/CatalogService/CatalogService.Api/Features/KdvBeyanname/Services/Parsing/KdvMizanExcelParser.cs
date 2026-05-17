using System.Globalization;
using ClosedXML.Excel;

namespace CatalogService.Api.Features.KdvBeyanname.Services.Parsing
{
    public class KdvMizanParsedRow
    {
        public string HesapKodu { get; set; } = string.Empty;
        public string HesapAdi { get; set; } = string.Empty;
        public decimal BorcToplam { get; set; }
        public decimal AlacakToplam { get; set; }
        public decimal BorcKalan { get; set; }
        public decimal AlacakKalan { get; set; }
    }

    public class KdvMizanParseResult
    {
        public List<KdvMizanParsedRow> Rows { get; } = new();
        public int OkunanSatir { get; set; }
        public int AtlananSatir { get; set; }
        public List<string> Uyarilar { get; } = new();
    }

    public interface IKdvMizanExcelParser
    {
        KdvMizanParseResult Parse(Stream excelStream);
    }

    public class KdvMizanExcelParser : IKdvMizanExcelParser
    {
        // Geçerli hesap kodu kuralı:
        //   - İlk segment (ana hesap) tam 3 haneli olmalı (örn: "100".."799").
        //   - Sonraki segmentler 1+ haneli sayısal alt kırılım.
        //   - Örn: "191", "191 1", "191 1 20"  (5 segment'e kadar).
        // Bu kural mizan genel toplam satırı (kod="10" gibi) ve sayfa numaralarını eler.
        private static bool IsValidHesapKodu(string kod)
        {
            if (string.IsNullOrWhiteSpace(kod)) return false;
            var parts = kod.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts.Length > 5) return false;
            if (parts[0].Length != 3) return false;
            foreach (var p in parts)
                if (!p.All(char.IsDigit)) return false;
            return true;
        }

        public KdvMizanParseResult Parse(Stream excelStream)
        {
            var result = new KdvMizanParseResult();

            using var workbook = new XLWorkbook(excelStream);

            // "Mizan" sheet'i öncelikli; bulunamazsa içerikçe en uygun ilk worksheet.
            var ws = workbook.Worksheets.FirstOrDefault(w =>
                         string.Equals(w.Name.Trim(), "Mizan", StringComparison.OrdinalIgnoreCase))
                     ?? workbook.Worksheets.First();

            if (!ws.RowsUsed().Any())
                throw new InvalidOperationException(
                    $"Excel'de '{ws.Name}' sayfası boş; mizan verisi bulunamadı.");

            var headerRow = ws.RowsUsed().First();
            var headerMap = HeaderMap.Build(headerRow);

            // Beklenen başlıklar (varyantlar dahil).
            int colKod          = HeaderMap.RequireColumn(headerMap, "Hesap Kodu");
            int colAd           = HeaderMap.RequireColumn(headerMap, "Hesap Adı", "Hesap Adi");
            int colBorcToplam   = HeaderMap.RequireColumn(headerMap, "TL Borç Toplam", "TL Borc Toplam", "Borç Toplam");
            int colAlacakToplam = HeaderMap.RequireColumn(headerMap, "TL Alacak Toplam", "Alacak Toplam");
            int colBorcBakiye   = HeaderMap.RequireColumn(headerMap, "TL Borç Bakiye", "TL Borc Bakiye", "Borç Bakiye");
            int colAlacakBakiye = HeaderMap.RequireColumn(headerMap, "TL Alacak Bakiye", "Alacak Bakiye");

            int headerRowNum = headerRow.RowNumber();

            foreach (var row in ws.RowsUsed())
            {
                if (row.RowNumber() <= headerRowNum) continue;

                var rawKod = row.Cell(colKod).GetString().Trim();
                if (string.IsNullOrWhiteSpace(rawKod))
                {
                    // boş satır — sessiz atla
                    continue;
                }

                result.OkunanSatir++;

                var kod = NormalizeKod(rawKod);

                if (!IsValidHesapKodu(kod))
                {
                    result.AtlananSatir++;
                    result.Uyarilar.Add(
                        $"Satır {row.RowNumber()}: Hesap kodu formatı geçersiz ('{rawKod}'). Atlandı.");
                    continue;
                }

                result.Rows.Add(new KdvMizanParsedRow
                {
                    HesapKodu    = kod,
                    HesapAdi     = row.Cell(colAd).GetString().Trim(),
                    BorcToplam   = ReadDecimal(row.Cell(colBorcToplam)),
                    AlacakToplam = ReadDecimal(row.Cell(colAlacakToplam)),
                    BorcKalan    = ReadDecimal(row.Cell(colBorcBakiye)),
                    AlacakKalan  = ReadDecimal(row.Cell(colAlacakBakiye))
                });
            }

            if (result.Rows.Count == 0)
                throw new InvalidOperationException(
                    "Mizan dosyasında geçerli bir hesap kodu satırı bulunamadı.");

            return result;
        }

        // Excel kodu sayı olarak okuyabilir (191 → 191.0). Trim + ondalık kuyruğu temizle.
        private static string NormalizeKod(string raw)
        {
            var s = raw.Trim();
            // Excel "191.0" gibi okursa kuyruğu temizle.
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ||
                decimal.TryParse(s, NumberStyles.Any, new CultureInfo("tr-TR"), out d))
            {
                if (d == Math.Truncate(d))
                    return ((long)d).ToString(CultureInfo.InvariantCulture);
            }
            return s;
        }

        private static decimal ReadDecimal(IXLCell cell)
        {
            if (cell.IsEmpty()) return 0m;
            try
            {
                if (cell.DataType == XLDataType.Number)
                    return (decimal)cell.GetDouble();

                var s = cell.GetString().Trim();
                if (string.IsNullOrEmpty(s)) return 0m;
                s = s.Replace(" ", string.Empty);

                if (decimal.TryParse(s, NumberStyles.Any, new CultureInfo("tr-TR"), out var tr))
                    return tr;
                if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var inv))
                    return inv;

                return 0m;
            }
            catch
            {
                return 0m;
            }
        }
    }
}
