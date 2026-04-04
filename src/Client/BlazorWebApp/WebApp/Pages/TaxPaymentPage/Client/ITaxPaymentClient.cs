using Microsoft.AspNetCore.Components.Forms;
using WebApp.Pages.TaxPaymentPage.DTO;

namespace WebApp.Pages.TaxPaymentPage.Client
{
    public interface ITaxPaymentClient
    {
        Task<List<TaxPaymentEntityDto>> GetAllAsync();
        Task<TaxPaymentEntityDto?> GetAsync(int id);
        Task<bool> CreateAsync(TaxPaymentEntityDto model);
        Task<bool> UpdateAsync(TaxPaymentEntityDto model);
        Task<bool> DeleteAsync(int id);
        Task<bool> ImportAsync(List<TaxPaymentEntityDto> items);
        Task<byte[]> ExportExcelAsync();
        Task<bool> DeleteAllAsync();
        Task<List<TaxPaymentEntityDto>> ParseExcelAsync(IBrowserFile file);
    }
}
