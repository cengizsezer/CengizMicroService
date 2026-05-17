using System.Globalization;
using ClosedXML.Excel;
using Sovos.InvoiceWorker.Core.DTOs;

namespace Sovos.IncomingInvoiceWorker.Services.Parsing;

public interface IGelenFaturaExcelParser
{
    List<ScrapedInvoice> Parse(Stream excelStream);
}

public class GelenFaturaExcelParser : IGelenFaturaExcelParser
{
    public List<ScrapedInvoice> Parse(Stream excelStream)
    {
        var result = new List<ScrapedInvoice>();

        using var workbook = new XLWorkbook(excelStream);
        var ws = workbook.Worksheets.First();

        if (!ws.RowsUsed().Any())
            return result;

        var headerRow = ws.RowsUsed().First();
        var headerMap = BuildHeaderMap(headerRow);

        int colUnvan       = RequireColumn(headerMap, "Firma Ünvanı", "Firma Unvani");
        int colFaturaNo    = RequireColumn(headerMap, "Fatura No");
        int colVkn         = RequireColumn(headerMap, "Gönderici VKN", "Gonderici VKN");
        int colParaBirimi  = RequireColumn(headerMap, "Para Birimi");
        int colFaturaTutar = RequireColumn(headerMap, "Fatura Tutarı", "Fatura Tutari");
        int colToplamVergi = RequireColumn(headerMap, "Toplam Vergi");
        int colDuzTarih    = RequireColumn(headerMap, "Düzenlenme Tarihi", "Duzenlenme Tarihi");
        int? colStatu      = OptionalColumn(headerMap, "Statü", "Statu", "Status");

        int headerRowNum = headerRow.RowNumber();

        foreach (var row in ws.RowsUsed())
        {
            if (row.RowNumber() <= headerRowNum) continue;

            var faturaNo = row.Cell(colFaturaNo).GetString().Trim();
            if (string.IsNullOrEmpty(faturaNo))
                continue; // boş satır veya TOPLAM satırı (Fatura No null gelir)

            // TOPLAM satırının ikinci işareti: Fatura Tutarı sayı yerine string gelir.
            var tutarCell = row.Cell(colFaturaTutar);
            if (tutarCell.DataType != XLDataType.Number)
                continue;

            string? statu = null;
            if (colStatu is int sc)
            {
                var raw = row.Cell(sc).GetString().Trim();
                if (!string.IsNullOrEmpty(raw))
                    statu = raw.ToUpperInvariant();
            }

            result.Add(new ScrapedInvoice
            {
                FirmaUnvani      = row.Cell(colUnvan).GetString().Trim(),
                FaturaNo         = faturaNo,
                GondericiVkn     = row.Cell(colVkn).GetString().Trim(),
                ParaBirimi       = row.Cell(colParaBirimi).GetString().Trim(),
                FaturaTutari     = ReadDecimal(tutarCell),
                ToplamVergi      = ReadDecimal(row.Cell(colToplamVergi)),
                DuzenlenmeTarihi = ReadDate(row.Cell(colDuzTarih)),
                Statu            = statu
            });
        }

        return result;
    }

    private static Dictionary<string, int> BuildHeaderMap(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var text = cell.GetString().Trim();
            if (string.IsNullOrEmpty(text)) continue;
            if (!map.ContainsKey(text))
                map[text] = cell.Address.ColumnNumber;
        }
        return map;
    }

    private static int RequireColumn(Dictionary<string, int> map, params string[] candidates)
    {
        foreach (var name in candidates)
            if (map.TryGetValue(name, out var col)) return col;
        throw new InvalidOperationException(
            $"Excel'de kolon bulunamadı: '{string.Join(" / ", candidates)}'. " +
            "DP gelen fatura Excel başlık satırını kontrol edin.");
    }

    private static int? OptionalColumn(Dictionary<string, int> map, params string[] candidates)
    {
        foreach (var name in candidates)
            if (map.TryGetValue(name, out var col)) return col;
        return null;
    }

    private static decimal ReadDecimal(IXLCell cell)
    {
        if (cell.IsEmpty()) return 0m;
        if (cell.DataType == XLDataType.Number)
            return (decimal)cell.GetDouble();

        var s = cell.GetString().Trim().Replace(" ", string.Empty);
        if (string.IsNullOrEmpty(s)) return 0m;
        if (decimal.TryParse(s, NumberStyles.Any, new CultureInfo("tr-TR"), out var tr)) return tr;
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var inv)) return inv;
        return 0m;
    }

    private static DateTime? ReadDate(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        if (cell.DataType == XLDataType.DateTime)
            return cell.GetDateTime();

        var s = cell.GetString().Trim();
        if (string.IsNullOrEmpty(s)) return null;
        if (DateTime.TryParseExact(
                s,
                new[] { "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd" },
                new CultureInfo("tr-TR"),
                DateTimeStyles.None,
                out var exact))
            return exact;
        if (DateTime.TryParse(s, new CultureInfo("tr-TR"), DateTimeStyles.None, out var loose))
            return loose;
        return null;
    }
}
