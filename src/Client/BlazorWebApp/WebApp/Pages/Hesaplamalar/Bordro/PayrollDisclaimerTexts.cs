namespace WebApp.Pages.Hesaplamalar.Bordro
{
    /// <summary>
    /// Ücret Bordrosu sayfasındaki disclaimer metinleri.
    /// Bu metinler backend export'larında (Excel + PDF) da AYNI şekilde kullanılır.
    /// Kaynak: <c>CatalogService.Api.Features.Payroll.Configuration.PayrollExportFooterTexts</c>
    /// İki proje ayrı; metni iki tarafta da senkron tutmaya dikkat et.
    /// </summary>
    public static class PayrollDisclaimerTexts
    {
        public const string PkfLegalName = "PKF [TAM UNVAN]";

        public const string FreeServiceNote =
            "Sadece temel parametrelere göre hesaplama yapmak üzere tasarlanan bu hizmetin kullanımı ücretsizdir.";

        public static string Disclaimer =>
            "Buradaki hesaplamalar bilgilendirme amaçlı olup profesyonel danışmanlık hizmeti niteliği taşımaz. " +
            "Bilgilerin doğruluğu garanti edilmez ve bu bilgilerin kullanımından doğabilecek herhangi bir zarardan " +
            $"{PkfLegalName} sorumlu tutulamaz. Konuyla ilgili işlem tesis etmeden önce profesyonel bir danışmana başvurunuz.";
    }
}
