using System.Globalization;
using ClosedXML.Excel;

namespace CatalogService.Api.Features.KdvBeyanname.Services.Parsing
{
    public class KdvYevmiyeParsedRow
    {
        public DateTime? FisTarihi { get; set; }
        public string? FisNo { get; set; }
        public string? FisAciklamasi { get; set; }
        public string HesapKodu { get; set; } = string.Empty;
        public string HesapAdi { get; set; } = string.Empty;
        public string? BelgeTipi { get; set; }
        public DateTime? BelgeTarihi { get; set; }
        public string? Aciklama { get; set; }

        /// <summary>
        /// "S. No" kolonu (J) — fatura no prefix'i. Excel'de sayı olarak gelse bile
        /// string olarak tutulur (örn. "INT" veya "0").
        /// </summary>
        public string? SNo { get; set; }

        /// <summary>
        /// "Belge No" kolonu (K) — 13+ haneli olabilir. Float'a düşmemesi için
        /// kesinlikle string olarak okunur.
        /// </summary>
        public string? BelgeNo { get; set; }

        /// <summary>
        /// SNo + BelgeNo concat. Bu, eşleştirme anahtarıdır. Her ikisi de doluysa
        /// non-null olur; ikisinden biri boş ise null kalır ve satır karşılaştırma
        /// dışında tutulur.
        /// </summary>
        public string? FaturaNo { get; set; }

        public decimal Borc { get; set; }
        public decimal Alacak { get; set; }
    }

    public class KdvYevmiyeParseResult
    {
        public List<KdvYevmiyeParsedRow> Rows { get; } = new();
        public int OkunanSatir { get; set; }
        public int AtlananSatir { get; set; }
        public List<string> Uyarilar { get; } = new();
    }

    public interface IKdvYevmiyeExcelParser
    {
        KdvYevmiyeParseResult Parse(Stream excelStream);
    }

    public class KdvYevmiyeExcelParser : IKdvYevmiyeExcelParser
    {
        public KdvYevmiyeParseResult Parse(Stream excelStream)
        {
            var result = new KdvYevmiyeParseResult();

            using var workbook = new XLWorkbook(excelStream);
            var ws = workbook.Worksheets.First(); // genelde "Sayfa1"

            if (!ws.RowsUsed().Any())
                throw new InvalidOperationException(
                    $"Excel'de '{ws.Name}' sayfası boş; yevmiye verisi bulunamadı.");

            var headerRow = ws.RowsUsed().First();
            var headerMap = HeaderMap.Build(headerRow);

            // Zorunlu kolonlar
            int colHesapKodu = HeaderMap.RequireColumn(headerMap, "Hesap Kodu");
            int colHesapAdi  = HeaderMap.RequireColumn(headerMap, "Hesap Adı", "Hesap Adi");
            int colSNo       = HeaderMap.RequireColumn(headerMap, "S. No", "S.No", "SNo");
            int colBelgeNo   = HeaderMap.RequireColumn(headerMap, "Belge No", "BelgeNo");
            int colBorc      = HeaderMap.RequireColumn(headerMap, "Borç Tutar", "Borc Tutar", "Borç");
            int colAlacak    = HeaderMap.RequireColumn(headerMap, "Alacak Tutar", "Alacak");

            // Opsiyonel kolonlar — yoksa null kalır
            int? colFisTarihi   = HeaderMap.OptionalColumn(headerMap, "Fiş Tarihi", "Fis Tarihi");
            int? colFisNo       = HeaderMap.OptionalColumn(headerMap, "Fiş No", "Fis No");
            int? colFisAciklama = HeaderMap.OptionalColumn(headerMap, "Fiş Açıklaması", "Fis Aciklamasi");
            int? colBelgeTipi   = HeaderMap.OptionalColumn(headerMap, "Belge Tipi");
            int? colBelgeTarihi = HeaderMap.OptionalColumn(headerMap, "Belge Tarihi");
            int? colAciklama    = HeaderMap.OptionalColumn(headerMap, "Açıklama", "Aciklama");

            int headerRowNum = headerRow.RowNumber();

            foreach (var row in ws.RowsUsed())
            {
                if (row.RowNumber() <= headerRowNum) continue;

                // Hesap Kodu boşsa satır geçersiz; muhtemelen boş ya da toplam satırı.
                var hesapKodu = row.Cell(colHesapKodu).GetString().Trim();
                if (string.IsNullOrWhiteSpace(hesapKodu)) continue;

                result.OkunanSatir++;

                // S. No ve Belge No — kesinlikle string'e çevir (float'a düşmesi yasak).
                var sNo     = ReadCellAsString(row.Cell(colSNo));
                var belgeNo = ReadCellAsString(row.Cell(colBelgeNo));

                // Onaylanan kural: J veya K boş / "0" ise satır kaydedilmez.
                bool hasFaturaAnahtari = !string.IsNullOrWhiteSpace(sNo)
                                         && !string.IsNullOrWhiteSpace(belgeNo)
                                         && belgeNo != "0";
                if (!hasFaturaAnahtari)
                {
                    result.AtlananSatir++;
                    continue;
                }

                result.Rows.Add(new KdvYevmiyeParsedRow
                {
                    FisTarihi      = colFisTarihi   is int c1 ? ReadDate(row.Cell(c1)) : null,
                    FisNo          = colFisNo       is int c2 ? ReadCellAsString(row.Cell(c2)) : null,
                    FisAciklamasi  = colFisAciklama is int c3 ? row.Cell(c3).GetString().Trim() : null,
                    HesapKodu      = hesapKodu,
                    HesapAdi       = row.Cell(colHesapAdi).GetString().Trim(),
                    BelgeTipi      = colBelgeTipi   is int c4 ? row.Cell(c4).GetString().Trim() : null,
                    BelgeTarihi    = colBelgeTarihi is int c5 ? ReadDate(row.Cell(c5)) : null,
                    Aciklama       = colAciklama    is int c6 ? row.Cell(c6).GetString().Trim() : null,
                    SNo            = sNo,
                    BelgeNo        = belgeNo,
                    FaturaNo       = sNo + belgeNo,
                    Borc           = ReadDecimal(row.Cell(colBorc)),
                    Alacak         = ReadDecimal(row.Cell(colAlacak))
                });
            }

            if (result.Rows.Count == 0)
            {
                // Tüm satırlar boş J/K nedeniyle atlandıysa hata fırlat — kullanıcıya açıkla.
                throw new InvalidOperationException(
                    "Yevmiye dosyasında 'S. No' (J) ve 'Belge No' (K) kolonları dolu olan " +
                    "hiçbir satır bulunamadı. Bu kolonlar fatura eşleştirme için zorunludur.");
            }

            return result;
        }

        // ClosedXML sayıları double olarak okur; Belge No (örn. "2026000001376")
        // float precision sınırını aşar. cell.GetString() raw text'i verir.
        private static string? ReadCellAsString(IXLCell cell)
        {
            if (cell.IsEmpty()) return null;
            // GetFormattedString => sayılar bile excel'deki görüntüsüyle döner;
            // ama tarih hücreleri için tarih string'i döner. Burada belge no
            // kolonlarının tarih olmadığını biliyoruz.
            try
            {
                if (cell.DataType == XLDataType.Number)
                {
                    var d = cell.GetDouble();
                    if (d == Math.Truncate(d))
                        return ((long)d).ToString(CultureInfo.InvariantCulture);
                    return d.ToString("0.##########", CultureInfo.InvariantCulture);
                }
                return cell.GetString().Trim();
            }
            catch
            {
                return cell.GetString().Trim();
            }
        }

        private static DateTime? ReadDate(IXLCell cell)
        {
            if (cell.IsEmpty()) return null;
            try
            {
                if (cell.DataType == XLDataType.DateTime)
                    return cell.GetDateTime();

                var s = cell.GetString().Trim();
                if (string.IsNullOrEmpty(s)) return null;

                if (DateTime.TryParseExact(s,
                        new[] { "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd" },
                        new CultureInfo("tr-TR"),
                        DateTimeStyles.None, out var dt))
                    return dt;
                if (DateTime.TryParse(s, new CultureInfo("tr-TR"), DateTimeStyles.None, out dt))
                    return dt;
                return null;
            }
            catch
            {
                return null;
            }
        }

        private static decimal ReadDecimal(IXLCell cell)
        {
            if (cell.IsEmpty()) return 0m;
            try
            {
                if (cell.DataType == XLDataType.Number)
                    return (decimal)cell.GetDouble();
                var s = cell.GetString().Trim().Replace(" ", string.Empty);
                if (string.IsNullOrEmpty(s)) return 0m;
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
