using System.Globalization;
using System.Text;
using ExcelDataReader;
using WebApp.Application.Services.Interfaces;

namespace WebApp.Application.Services
{
    public class ExcelMizanParser : IExcelMizanParser
    {
        private static int _encodingRegistered;

        public ExcelMizanParser()
        {
            if (Interlocked.Exchange(ref _encodingRegistered, 1) == 0)
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }
        }

        public Task<MizanParseResult> ParseAsync(Stream excelStream)
        {
            var result = new MizanParseResult();

            try
            {
                using var reader = ExcelReaderFactory.CreateReader(excelStream);

                int rowIndex = 0;
                while (reader.Read())
                {
                    rowIndex++;
                    if (rowIndex == 1) continue; // header

                    if (reader.FieldCount < 1) continue;

                    var rawKod = SafeGetString(reader, 0)?.Trim();
                    if (string.IsNullOrWhiteSpace(rawKod)) continue;

                    var kod = NormalizeKod(rawKod);
                    if (string.IsNullOrEmpty(kod)) continue;

                    var oncekiCell = reader.FieldCount > 1 ? SafeGetCell(reader, 1) : null;
                    var cariCell = reader.FieldCount > 2 ? SafeGetCell(reader, 2) : null;

                    result.Rows.Add(new MizanExcelRow
                    {
                        Kod = kod,
                        OncekiDonem = ParseDecimal(oncekiCell),
                        CariDonem = ParseDecimal(cariCell)
                    });
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Excel okunurken hata: {ex.Message}");
            }

            return Task.FromResult(result);
        }

        private static string NormalizeKod(string raw)
        {
            var trimmed = raw.Trim();
            // Eğer Excel kodu "100.0" gibi geliyorsa virgül/sonraki sıfırları temizle
            if (decimal.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ||
                decimal.TryParse(trimmed, NumberStyles.Any, new CultureInfo("tr-TR"), out d))
            {
                if (d == Math.Truncate(d))
                    return ((long)d).ToString(CultureInfo.InvariantCulture);
            }
            return trimmed;
        }

        private static string? SafeGetString(IExcelDataReader reader, int idx)
        {
            try
            {
                if (reader.IsDBNull(idx)) return null;
                var val = reader.GetValue(idx);
                return val?.ToString();
            }
            catch { return null; }
        }

        private static object? SafeGetCell(IExcelDataReader reader, int idx)
        {
            try
            {
                if (reader.IsDBNull(idx)) return null;
                return reader.GetValue(idx);
            }
            catch { return null; }
        }

        private static decimal? ParseDecimal(object? cell)
        {
            if (cell is null) return null;
            if (cell is decimal d) return d;
            if (cell is double dbl) return (decimal)dbl;
            if (cell is float f) return (decimal)f;
            if (cell is int i) return i;
            if (cell is long l) return l;

            var s = cell.ToString();
            if (string.IsNullOrWhiteSpace(s)) return null;

            // Türkçe formatta "1.234,56" gelirse
            s = s.Trim().Replace(" ", string.Empty);

            if (decimal.TryParse(s, NumberStyles.Any, new CultureInfo("tr-TR"), out var trVal))
                return trVal;

            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var invVal))
                return invVal;

            return null;
        }
    }
}
