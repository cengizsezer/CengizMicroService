using System.Text;

namespace WebApp.Application.Services
{
    /// <summary>
    /// Excel'in doğrudan açtığı CSV üretir. İstemcide xlsx yazan bir paket yok
    /// (<c>ExcelDataReader</c> yalnızca okur) ve API'ye dokunmamak gerektiği için
    /// dışa aktarım istemci tarafında CSV olarak yapılıyor.
    ///
    /// İki ayrıntı Excel'de doğru açılması için şart:
    /// <list type="bullet">
    /// <item>İlk satırdaki <c>sep=;</c> yönergesi — ayracı yerel ayardan bağımsız sabitler.</item>
    /// <item>UTF-8 BOM — Türkçe karakterler bozulmadan gelir.</item>
    /// </list>
    /// Tutarlar <c>N2</c> ile yazılır; tr-TR ayracıyla Excel bunları sayı olarak okur.
    /// </summary>
    public static class CsvAktarim
    {
        private const char Ayrac = ';';

        /// <param name="basliklar">Kolon başlıkları.</param>
        /// <param name="satirlar">Her satır, başlıklarla aynı sayıda hücre.</param>
        /// <param name="ustSatirlar">Tablonun üstüne yazılacak serbest bilgi satırları (rapor adı, dönem…).</param>
        public static byte[] Olustur(IEnumerable<string> basliklar,
                                     IEnumerable<IEnumerable<string?>> satirlar,
                                     IEnumerable<string>? ustSatirlar = null)
        {
            var sb = new StringBuilder();
            sb.Append("sep=").Append(Ayrac).Append('\n');

            if (ustSatirlar is not null)
                foreach (var bilgi in ustSatirlar)
                    sb.Append(Hucre(bilgi)).Append('\n');

            sb.Append(string.Join(Ayrac, basliklar.Select(Hucre))).Append('\n');

            foreach (var satir in satirlar)
                sb.Append(string.Join(Ayrac, satir.Select(Hucre))).Append('\n');

            // BOM'lu UTF-8: Excel dosyayı UTF-8 olarak tanısın.
            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        }

        /// <summary>Ayraç, tırnak veya satır sonu içeren hücreyi tırnaklar; içteki tırnağı ikiler.</summary>
        private static string Hucre(string? deger)
        {
            var s = deger ?? string.Empty;

            if (s.IndexOf(Ayrac) < 0 && s.IndexOf('"') < 0 && s.IndexOf('\n') < 0 && s.IndexOf('\r') < 0)
                return s;

            return '"' + s.Replace("\"", "\"\"") + '"';
        }

        /// <summary>Dosya adında kullanılamayacak karakterleri temizler.</summary>
        public static string DosyaAdi(string taban, string uzanti = "csv")
        {
            var temiz = new string(taban.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c).ToArray());
            return $"{temiz}.{uzanti}";
        }
    }
}
