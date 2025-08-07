using WebApp.Domain.Models;
using WebApp.Domain.Models.Catalog;
using WebApp.Shared.Dto;

namespace WebApp.Application.Services.Interfaces
{
    public interface IExpenseService
    {
        Task<PaginatedItemsViewModel<ExpenseDto>> GetExpensesAsync(int pageIndex = 0, int pageSize = 10);

        Task<List<PersonnelDto>> GetPersonnelsAsync();              // ✅ Liste döner
        Task<List<AccountingCodeDto>> GetAccountingCodesAsync();   // ✅ Liste döner
    }
}
