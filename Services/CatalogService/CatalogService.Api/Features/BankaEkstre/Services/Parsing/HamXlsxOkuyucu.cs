using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace CatalogService.Api.Features.BankaEkstre.Services.Parsing
{
    /// <summary>
    /// xlsx'i kütüphanesiz, doğrudan zip içindeki XML'den okur: <c>xl/sharedStrings.xml</c>
    /// ve sayfanın <c>xl/worksheets/sheetN.xml</c> dosyası.
    ///
    /// <b>Neden var?</b> Ziraat ekstresinin <c>styles.xml</c>'i bozuk (openpyxl
    /// "expected &lt;class 'openpyxl.styles.fills.Fill'&gt;" ile açamıyor); ClosedXML de
    /// aynı dosyada patlıyor. Bu yol biçim bilgisini <b>hiç okumaz</b>, yalnız hücre
    /// değerlerini alır — bozuk stil tablosu okumayı durduramaz.
    ///
    /// Bedeli: hücrenin tarih biçimli olup olmadığı bilinemez. Sayısal bir hücrenin tarih
    /// olup olmadığına <see cref="TabloDeger.Tarih"/> seri numarası aralığıyla karar verir
    /// ve bunu yalnız tarih kolonunda dener.
    /// </summary>
    public static class HamXlsxOkuyucu
    {
        private static readonly XNamespace Ana = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace Iliski = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PaketIliski = "http://schemas.openxmlformats.org/package/2006/relationships";

        public const string OkuyucuAdi = "ham XML";

        public static EkstreTablosu Oku(Stream dosya)
        {
            dosya.Position = 0;
            using var arsiv = new ZipArchive(dosya, ZipArchiveMode.Read, leaveOpen: true);

            var paylasilanlar = PaylasilanMetinler(arsiv);
            var sayfaYolu = IlkSayfaYolu(arsiv)
                            ?? throw new InvalidDataException(
                                "xlsx içinde sayfa bulunamadı (xl/worksheets/sheet*.xml yok).");

            var girdi = Girdi(arsiv, sayfaYolu)
                        ?? throw new InvalidDataException($"xlsx içinde '{sayfaYolu}' bulunamadı.");

            using var akis = girdi.Open();
            var belge = XDocument.Load(akis);

            var satirlar = new List<TabloSatiri>();
            var sonrakiSatirNo = 1;

            foreach (var satirXml in belge.Root?.Element(Ana + "sheetData")?.Elements(Ana + "row")
                                     ?? Enumerable.Empty<XElement>())
            {
                var satirNo = SayiOku(satirXml.Attribute("r")?.Value) ?? sonrakiSatirNo;
                sonrakiSatirNo = satirNo + 1;

                var hucreler = new List<TabloHucresi>();
                var sonrakiKolon = 1;

                foreach (var hucreXml in satirXml.Elements(Ana + "c"))
                {
                    // Hücre referansı (r="C13") bazı üreticilerde eksik; o durumda sıradaki
                    // kolon indeksi kullanılır.
                    var kolon = KolonNo(hucreXml.Attribute("r")?.Value) ?? sonrakiKolon;
                    sonrakiKolon = kolon + 1;

                    while (hucreler.Count < kolon - 1) hucreler.Add(TabloHucresi.Bos);

                    var hucre = HucreOku(hucreXml, paylasilanlar);
                    if (hucreler.Count == kolon - 1) hucreler.Add(hucre);
                    else hucreler[kolon - 1] = hucre;
                }

                satirlar.Add(new TabloSatiri(satirNo, hucreler));
            }

            return new EkstreTablosu(satirlar, OkuyucuAdi);
        }

        private static TabloHucresi HucreOku(XElement hucreXml, IReadOnlyList<string> paylasilanlar)
        {
            var tip = hucreXml.Attribute("t")?.Value ?? "n";

            switch (tip)
            {
                case "s":
                    var indeks = SayiOku(hucreXml.Element(Ana + "v")?.Value);
                    var metin = indeks is { } i && i >= 0 && i < paylasilanlar.Count ? paylasilanlar[i] : string.Empty;
                    return new TabloHucresi(metin, null, null);

                case "inlineStr":
                    return new TabloHucresi(MetinTopla(hucreXml.Element(Ana + "is")), null, null);

                case "str":
                case "b":
                case "e":
                    return new TabloHucresi(hucreXml.Element(Ana + "v")?.Value, null, null);

                default:
                    var ham = hucreXml.Element(Ana + "v")?.Value;
                    if (string.IsNullOrWhiteSpace(ham)) return TabloHucresi.Bos;

                    // Sayı her zaman invariant biçimde saklanır (ondalık nokta).
                    if (double.TryParse(ham, NumberStyles.Float, CultureInfo.InvariantCulture, out var sayi))
                        return new TabloHucresi(sayi.ToString(CultureInfo.InvariantCulture), sayi, null);

                    return new TabloHucresi(ham, null, null);
            }
        }

        /// <summary>
        /// <c>sharedStrings.xml</c> girdileri. Zengin metin (<c>&lt;r&gt;</c> parçaları)
        /// birleştirilir; fonetik alanlar (<c>rPh</c>) atılır — metnin parçası değiller.
        /// </summary>
        private static IReadOnlyList<string> PaylasilanMetinler(ZipArchive arsiv)
        {
            var girdi = Girdi(arsiv, "xl/sharedStrings.xml");
            if (girdi is null) return Array.Empty<string>();

            using var akis = girdi.Open();
            var belge = XDocument.Load(akis);

            return belge.Root?.Elements(Ana + "si").Select(MetinTopla).ToList() ?? new List<string>();
        }

        private static string MetinTopla(XElement? kapsayici)
        {
            if (kapsayici is null) return string.Empty;

            var parcalar = kapsayici.Descendants(Ana + "t")
                .Where(t => t.Ancestors(Ana + "rPh").Any() == false)
                .Select(t => t.Value);

            return string.Concat(parcalar);
        }

        /// <summary>
        /// İlk sayfanın zip içindeki yolu. Önce <c>workbook.xml</c> + ilişki dosyası
        /// üzerinden çözülür (sayfa sırası dosya adıyla aynı olmak zorunda değil);
        /// çözülemezse ada göre ilk <c>xl/worksheets/sheet*.xml</c> kullanılır.
        /// </summary>
        private static string? IlkSayfaYolu(ZipArchive arsiv)
        {
            try
            {
                var kitapGirdisi = Girdi(arsiv, "xl/workbook.xml");
                var iliskiGirdisi = Girdi(arsiv, "xl/_rels/workbook.xml.rels");

                if (kitapGirdisi is not null && iliskiGirdisi is not null)
                {
                    using var kitapAkisi = kitapGirdisi.Open();
                    var kitap = XDocument.Load(kitapAkisi);

                    var id = kitap.Root?.Element(Ana + "sheets")?.Elements(Ana + "sheet").FirstOrDefault()
                                 ?.Attribute(Iliski + "id")?.Value;

                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        using var iliskiAkisi = iliskiGirdisi.Open();
                        var iliskiler = XDocument.Load(iliskiAkisi);

                        var hedef = iliskiler.Root?.Elements(PaketIliski + "Relationship")
                            .FirstOrDefault(r => r.Attribute("Id")?.Value == id)
                            ?.Attribute("Target")?.Value;

                        if (!string.IsNullOrWhiteSpace(hedef))
                        {
                            var yol = hedef.TrimStart('/');
                            if (!yol.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)) yol = "xl/" + yol;
                            if (Girdi(arsiv, yol) is not null) return yol;
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is System.Xml.XmlException or InvalidDataException)
            {
                // workbook.xml da bozuk olabilir; ada göre aramaya düşülür.
            }

            return arsiv.Entries
                .Where(g => g.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
                            g.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(g => g.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.FullName)
                .FirstOrDefault();
        }

        private static ZipArchiveEntry? Girdi(ZipArchive arsiv, string yol)
            => arsiv.Entries.FirstOrDefault(g => string.Equals(g.FullName, yol, StringComparison.OrdinalIgnoreCase));

        private static int? SayiOku(string? metin)
            => int.TryParse(metin, NumberStyles.Integer, CultureInfo.InvariantCulture, out var deger) ? deger : null;

        /// <summary>"C13" → 3. Harf kısmı yoksa null.</summary>
        private static int? KolonNo(string? referans)
        {
            if (string.IsNullOrWhiteSpace(referans)) return null;

            var kolon = 0;
            foreach (var ch in referans)
            {
                if (char.IsDigit(ch)) break;

                var buyuk = char.ToUpperInvariant(ch);
                if (buyuk is < 'A' or > 'Z') return null;

                kolon = kolon * 26 + (buyuk - 'A' + 1);
            }

            return kolon > 0 ? kolon : null;
        }
    }
}
