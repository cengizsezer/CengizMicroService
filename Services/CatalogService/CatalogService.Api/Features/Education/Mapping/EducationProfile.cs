using AutoMapper;
using CatalogService.Api.Features.Education.Domain;
using CatalogService.Api.Features.Education.DTO;

namespace CatalogService.Api.Features.Education.Mapping
{
    public sealed class EducationProfile : Profile
    {
        public EducationProfile()
        {
            CreateMap<EducationItem, EducationItemDto>();
            CreateMap<EducationItem, EducationItemListItemDto>();
            CreateMap<CreateEducationItemDto, EducationItem>();
            // UpdateEducationItemDto için map gerekmez; controller içinde partial update yaptık.
        }
    }
}
