using AutoMapper;
using CatalogService.Api.Contracts.Dtos;
using CatalogService.Api.Core.Domain;

namespace CatalogService.Api.Core.Application.Mapping
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
