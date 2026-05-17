using ClosedXML.Excel;

namespace CatalogService.Api.Features.KdvBeyanname.Services.Parsing
{
    // Excel başlık satırını okuyup, beklenen kolon adlarını sütun indexine eşler.
    // İsim eşleşmesi case-insensitive ve trim'li; Türkçe karakterler korunur.
    internal static class HeaderMap
    {
        public static Dictionary<string, int> Build(IXLRow headerRow)
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

        public static int RequireColumn(Dictionary<string, int> map, params string[] candidates)
        {
            foreach (var name in candidates)
            {
                if (map.TryGetValue(name, out var col)) return col;
            }
            throw new InvalidOperationException(
                $"Kolon bulunamadı: '{string.Join(" / ", candidates)}'. " +
                "Excel başlık satırını kontrol edin.");
        }

        public static int? OptionalColumn(Dictionary<string, int> map, params string[] candidates)
        {
            foreach (var name in candidates)
            {
                if (map.TryGetValue(name, out var col)) return col;
            }
            return null;
        }
    }
}
