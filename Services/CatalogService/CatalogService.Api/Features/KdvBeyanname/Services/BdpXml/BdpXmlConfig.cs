namespace CatalogService.Api.Features.KdvBeyanname.Services.BdpXml
{
    // BDP KDV1_44 beyanname XML üretimi için sabitler ve mapping'ler.
    // Mizan hesap kodları → XML alanları eşleştirmesi burada toplanır;
    // değişirse tek nokta.
    public static class BdpXmlConfig
    {
        // Mizan ana hesap kodları
        public const string HesaplananKdvHesabi   = "391";
        public const string IndirilecekKdvHesabi  = "191";
        public const string DevredenKdvHesabi     = "190";
        public const string YurticiSatislarHesabi = "600";

        // Embedded resource yolu (BdpXmlBuilder kullanır).
        public const string TemplateResourceName =
            "CatalogService.Api.Features.KdvBeyanname.Resources.Templates.kdv1_44_template.xml";

        // XSD ve KodVer sürümü
        public const string KodVer = "KDV1_44";
        public const string XsdSchemaLocation = "KDV1_44.xsd";

        // Encoding: ISO-8859-9 (Türkçe karakter)
        public const string EncodingName = "ISO-8859-9";

        // Dönem tipi (aylık standart)
        public const string DonemTipi = "aylik";

        // Tevkifat uygulanmayan işlem türü — sample'da 1100 (genel mal/hizmet teslimi).
        // Oran bazında farklı kodlar olabilir; şimdilik tek değer. Düzeltilirse
        // burada oran-bazlı bir mapping'e dönüşür.
        public const string TevkifatUygulanmayanIslemTuruDefault = "1100";

        // Dosya adı formatı: {vdKodu}_{vergiNo}_KDV1_44_{ddMMyyyy}-{ddMMyyyy}.xml
        public static string DosyaAdi(
            string vdKodu, string vergiNo, DateTime donemBaslangic, DateTime donemBitis)
        {
            return $"{vdKodu}_{vergiNo}_{KodVer}_" +
                   $"{donemBaslangic:ddMMyyyy}-{donemBitis:ddMMyyyy}.xml";
        }
    }
}
