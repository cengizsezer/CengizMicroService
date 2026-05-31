namespace CatalogService.Api.Features.Payroll.Configuration
{
    /// <summary>
    /// Ücret Bordrosu Excel/PDF çıktılarının altında basılan yasal açıklama / sorumluluk reddi.
    /// PKF tam unvanı tek yerden değiştirilebilsin diye sabit olarak burada tutulur.
    /// </summary>
    public static class PayrollExportFooterTexts
    {
        public const string PkfLegalName = "PKF [TAM UNVAN]";

        public const string FreeServiceNote =
            "Sadece temel parametrelere göre hesaplama yapmak üzere tasarlanan bu hizmetin kullanımı ücretsizdir.";

        public static string Disclaimer =>
            "Buradaki hesaplamalar bilgilendirme amaçlı olup profesyonel danışmanlık hizmeti niteliği taşımaz. " +
            "Bilgilerin doğruluğu garanti edilmez ve bu bilgilerin kullanımından doğabilecek herhangi bir zarardan " +
            $"{PkfLegalName} sorumlu tutulamaz. Konuyla ilgili işlem tesis etmeden önce profesyonel bir danışmana başvurunuz.";

        /// <summary>
        /// Backend assembly içine gömülü PKF logosunun manifest kaynak adı.
        /// </summary>
        public const string LogoEmbeddedResourceName = "CatalogService.Api.Assets.pkf-logo.png";
    }
}
