using WebApp.Pages.Hesaplamalar.Bordro.Model;

namespace WebApp.Pages.Hesaplamalar.Bordro.Services
{
    /// <summary>
    /// Hesaplayıcının ve karşılaştırma penceresinin çalışmak için ihtiyaç duyduğu uçlar:
    /// kanun listesi, hesaplama, bordro Excel/PDF, kâr dağıtımı karşılaştırması ve onun
    /// Excel/PDF'i. Bileşen hangi kapıdan gittiğini bilmez; iki uygulaması var
    /// (KARARLAR §155):
    /// <list type="bullet">
    /// <item><see cref="PayrollApiService"/> — girişli <c>/catalog/payroll</c>.</item>
    /// <item><see cref="PayrollAcikApiService"/> — açık <c>/catalog/payroll/acik</c>, token'sız.</item>
    /// </list>
    /// <c>bootstrap</c> ve <c>parameters</c> bilinçli olarak burada YOK (hiçbir ekran
    /// çağırmıyor, açık kapıda da açılmadı).
    /// </summary>
    public interface IPayrollHesapApi
    {
        Task<CalculatePayrollResponse?> CalculateAsync(CalculatePayrollRequest request, CancellationToken cancellationToken = default);
        Task<List<PayrollLawTypeDto>> GetLawTypesAsync(int year, CancellationToken cancellationToken = default);
        Task<byte[]> ExportExcelAsync(CalculatePayrollRequest request, CancellationToken cancellationToken = default);
        Task<byte[]> ExportPdfAsync(CalculatePayrollRequest request, CancellationToken cancellationToken = default);
        Task<DistributionComparisonResultDto?> CompareDistributionAsync(CompareDistributionRequest request, CancellationToken cancellationToken = default);
        Task<byte[]> ExportComparisonExcelAsync(CompareDistributionRequest request, CancellationToken cancellationToken = default);
        Task<byte[]> ExportComparisonPdfAsync(CompareDistributionRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Uygulama içi (girişli) kullanımın DI anahtarı. Şu an kendi metodu yok —
    /// <see cref="IPayrollHesapApi"/>'nin tamamı; kayıt ve girişli taraf değişmesin diye
    /// boş haliyle duruyor (Prompt 18 kararı).
    /// </summary>
    public interface IPayrollApiService : IPayrollHesapApi
    {
    }
}
