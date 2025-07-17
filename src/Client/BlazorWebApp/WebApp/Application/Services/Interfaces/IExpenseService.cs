using WebApp.Domain.Models;
using WebApp.Domain.Models.Catalog;

namespace WebApp.Application.Services.Interfaces
{
    public interface IExpenseService
    {
        Task<PaginatedItemsViewModel<Expense>> GetExpensesAsync(int pageIndex = 0, int pageSize = 10);
    }
}
