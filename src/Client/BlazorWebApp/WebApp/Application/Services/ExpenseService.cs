using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using WebApp.Application.Services.Interfaces;
using WebApp.Domain.Models;
using WebApp.Domain.Models.Catalog;
using WebApp.Extensions;
using WebApp.Shared.Dto;

namespace WebApp.Application.Services
{
    public class ExpenseService : IExpenseService
    {
        private readonly HttpClient _httpClient;

        public ExpenseService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        //public async Task<PaginatedItemsViewModel<Expense>> GetExpensesAsync(int pageIndex = 0, int pageSize = 10)
        //{
        //    string prefix = _httpClient.BaseAddress?.Host.Contains("localhost") == true ? "" : "api/";
        //    string url = $"{prefix}catalog/expenses/paged?pageIndex={pageIndex}&pageSize={pageSize}";
        //    return await _httpClient.GetResponseAsync<PaginatedItemsViewModel<Expense>>(url);

        //}

        public async Task<PaginatedItemsViewModel<ExpenseDto>> GetExpensesAsync(int pageIndex = 0, int pageSize = 10)
        {
            string url = $"/catalog/expenses/paged?pageIndex={pageIndex}&pageSize={pageSize}";
            return await _httpClient.GetResponseAsync<PaginatedItemsViewModel<ExpenseDto>>(url);
        }


        public async Task<List<PersonnelDto>> GetPersonnelsAsync()
        {
            string url = $"/catalog/personnels"; // controller route: [HttpGet("personnels")]
            return await _httpClient.GetResponseAsync<List<PersonnelDto>>(url);
        }


        public async Task<List<AccountingCodeDto>> GetAccountingCodesAsync()
        {
            string url = $"/catalog/accountingcodes"; // not: catalog prefix varsa ekle
            return await _httpClient.GetResponseAsync<List<AccountingCodeDto>>(url);
        }


    }
}
