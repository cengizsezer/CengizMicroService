namespace CatalogService.Api.Features.Payroll.Configuration
{
    /// <summary>
    /// Ücret Bordrosu Excel/PDF çıktılarının altında basılan yasal açıklama / sorumluluk reddi.
    /// Sayfadaki metinle (WebApp PayrollDisclaimerTexts) birebir aynı tutulur.
    /// </summary>
    public static class PayrollExportFooterTexts
    {
        public const string FreeServiceNote =
            "Sadece temel parametrelere göre hesaplama yapmak üzere tasarlanan bu hizmetin kullanımı ücretsizdir.";

        public const string Disclaimer =
            "Buradaki hesaplamalar bilgilendirme amaçlıdır ve profesyonel danışmanlık hizmeti niteliği taşımaz. " +
            "Bilgilerin doğruluğu garanti edilmez; bu bilgilerin kullanımından doğabilecek zararlardan sorumluluk kabul edilmez. " +
            "Konuyla ilgili işlem tesis etmeden önce profesyonel bir danışmana başvurunuz.";

        /// <summary>
        /// Backend assembly içine gömülü "Bordro Hesaplama" logosunun manifest kaynak adı.
        /// WebApp wwwroot/img/bordro-logo.svg'den üretilmiş PNG (ClosedXML SVG basamıyor).
        /// </summary>
        public const string LogoEmbeddedResourceName = "CatalogService.Api.Assets.bordro-logo.png";
    }
}
