using AutoMapper;
using CatalogService.Api.Features.Expenses.Domain;
using CatalogService.Api.Features.Expenses.DTO;

namespace CatalogService.Api.Features.Expenses.Mapping
{
    public class ExpenseProfile : Profile
    {
        public ExpenseProfile()
        {
            CreateMap<ExpenseDto, Expense>();
            CreateMap<ReceiptItemDto, ReceiptItem>();
            CreateMap<ProductDetailDto, ProductDetail>();
        }
    }
}
