using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.KdvBeyanname.Domain;
using CatalogService.Api.Features.KdvBeyanname.Dtos;

namespace CatalogService.Api.Features.KdvBeyanname.Services.BdpXml
{
    public class BdpXmlOutput
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string FileName { get; set; } = string.Empty;
        public string ContentType => "application/xml";
    }

    public interface IBdpXmlBuilder
    {
        BdpXmlOutput Build(Firma firma, Duzenleyen duzenleyen, KdvSonucDto sonuc);
    }

    // Şablon-tabanlı XML üretim:
    //  - Template'i embedded resource'tan yükler (kdv1_44_template.xml).
    //  - Dinamik bölümleri (idari, mukellef, hsv, duzenleyen, tevkifat,
    //    indirilecekKDV, toplam alanları) XDocument üzerinden replace eder.
    //  - Diğer bloklar (indirimler, indirimNedenleri, sabit "0"/"0.00", ekler)
    //    şablondan birebir korunur — kullanıcı kararıyla v1'de dinamikleştirilmiyor.
    //  - Çıktı: ISO-8859-9 encoded byte[] + BDP standartında dosya adı.
    public class BdpXmlBuilder : IBdpXmlBuilder
    {
        private readonly IBdpXmlMapper _mapper;

        // Code page encoding (1254 / ISO-8859-9) için provider'ı bir kere register et.
        private static int _encodingRegistered;

        public BdpXmlBuilder(IBdpXmlMapper mapper)
        {
            _mapper = mapper;
            if (Interlocked.Exchange(ref _encodingRegistered, 1) == 0)
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }
        }

        public BdpXmlOutput Build(Firma firma, Duzenleyen duzenleyen, KdvSonucDto sonuc)
        {
            var model = _mapper.Map(firma, duzenleyen, sonuc);

            // Template'i embedded resource'tan oku.
            var assembly = typeof(BdpXmlBuilder).Assembly;
            using var stream = assembly.GetManifestResourceStream(BdpXmlConfig.TemplateResourceName)
                ?? throw new InvalidOperationException(
                    $"BDP XML şablonu bulunamadı: '{BdpXmlConfig.TemplateResourceName}'. " +
                    "csproj'da EmbeddedResource olarak işaretlendiğini kontrol edin.");

            var xdoc = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
            var root = xdoc.Root
                ?? throw new InvalidOperationException("Şablonda kök element yok.");

            ReplaceIdari(root, model);
            ReplaceKisiBlok(root, "mukellef",   model.Mukellef);
            ReplaceKisiBlok(root, "hsv",        model.Hsv);
            ReplaceKisiBlok(root, "duzenleyen", model.Duzenleyen);
            ReplaceOzel(root, model);

            // ISO-8859-9 encoded byte[] olarak serialize et.
            var iso = Encoding.GetEncoding(BdpXmlConfig.EncodingName);
            var settings = new XmlWriterSettings
            {
                Encoding = iso,
                Indent = true,
                IndentChars = "\t",
                NewLineChars = "\r\n",
                NewLineHandling = NewLineHandling.Replace,
                OmitXmlDeclaration = false
            };

            using var ms = new MemoryStream();
            using (var writer = XmlWriter.Create(ms, settings))
            {
                xdoc.Save(writer);
            }

            return new BdpXmlOutput
            {
                Content  = ms.ToArray(),
                FileName = BdpXmlConfig.DosyaAdi(
                    model.VdKodu, model.Mukellef.VergiNo,
                    model.DonemBaslangic, model.DonemBitis)
            };
        }

        // ── Replace helpers ────────────────────────────────────────────────

        private static void ReplaceIdari(XElement root, BdpXmlModel model)
        {
            var idari = root.Element("genel")?.Element("idari")
                ?? throw new InvalidOperationException("Şablonda <genel><idari> bloğu yok.");

            SetElement(idari, "vdKodu", model.VdKodu);

            var donem = idari.Element("donem")
                ?? throw new InvalidOperationException("Şablonda <donem> bloğu yok.");
            SetElement(donem, "tip", BdpXmlConfig.DonemTipi);
            SetElement(donem, "yil", model.Yil.ToString());
            SetElement(donem, "ay",  model.Ay.ToString());
        }

        private static void ReplaceKisiBlok(XElement root, string blokAdi, KisiBlok blok)
        {
            var element = root.Element("genel")?.Element(blokAdi)
                ?? throw new InvalidOperationException(
                    $"Şablonda <genel><{blokAdi}> bloğu yok.");

            SetElement(element, "vergiNo",    blok.VergiNo);
            SetElement(element, "soyadi",     blok.Soyadi);
            SetElement(element, "adi",        blok.Adi);
            SetElement(element, "ticSicilNo", blok.TicSicilNo);
            SetElement(element, "eposta",     blok.Eposta);
            SetElement(element, "alanKodu",   blok.AlanKodu);
            SetElement(element, "telNo",      blok.TelNo);
        }

        private static void ReplaceOzel(XElement root, BdpXmlModel model)
        {
            var ozel = root.Element("ozel")
                ?? throw new InvalidOperationException("Şablonda <ozel> bloğu yok.");

            // tevkifatUygulanmayanlar — child'ları rebuild et
            var tevkifat = ozel.Element("tevkifatUygulanmayanlar")
                ?? throw new InvalidOperationException("Şablonda <tevkifatUygulanmayanlar> yok.");
            tevkifat.RemoveNodes();
            foreach (var t in model.TevkifatUygulanmayanlar)
            {
                tevkifat.Add(new XElement("tevkifatUygulanmayan",
                    new XElement("tevkifatUygulanmayanIslemTuru", t.IslemTuru),
                    new XElement("matrah", t.Matrah),
                    new XElement("oran",   t.Oran),
                    new XElement("vergi",  t.Vergi)
                ));
            }

            SetElement(ozel, "vergiToplami",  model.VergiToplami);
            SetElement(ozel, "toplamMatrah",  model.ToplamMatrah);
            SetElement(ozel, "hesaplananKDV", model.HesaplananKDV);
            SetElement(ozel, "toplamKDV",     model.ToplamKDV);

            // indirilecekKDVODler — child'ları rebuild et
            var indirOd = ozel.Element("indirilecekKDVODler")
                ?? throw new InvalidOperationException("Şablonda <indirilecekKDVODler> yok.");
            indirOd.RemoveNodes();
            foreach (var i in model.IndirilecekKDVODler)
            {
                indirOd.Add(new XElement("indirilecekKDVOD",
                    new XElement("oran",      i.Oran),
                    new XElement("bedel",     i.Bedel),
                    new XElement("KDVTutari", i.KDVTutari)
                ));
            }
            SetElement(ozel, "indirilecekKDVODToplamKDV", model.IndirilecekKDVODToplamKDV);

            SetElement(ozel, "odenmesiGerekenKDV",      model.OdenmesiGerekenKDV);
            SetElement(ozel, "sonrakiDonemeDevredenKDV", model.SonrakiDonemeDevredenKDV);

            SetElement(ozel, "teslimVeHizmetleriTeskilEdenBedelAylik",
                model.TeslimVeHizmetleriTeskilEdenBedelAylik);
            SetElement(ozel, "teslimVeHizmetleriTeskilEdenBedelKumulatif",
                model.TeslimVeHizmetleriTeskilEdenBedelKumulatif);
        }

        private static void SetElement(XElement parent, string name, string value)
        {
            var el = parent.Element(name);
            if (el is null)
            {
                parent.Add(new XElement(name, value));
            }
            else
            {
                el.SetValue(value);
            }
        }
    }
}
