using AutoMapper;
using CatalogService.Api.Core.Domain.Education;

namespace CatalogService.Api.Core.Application.Mapping
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
