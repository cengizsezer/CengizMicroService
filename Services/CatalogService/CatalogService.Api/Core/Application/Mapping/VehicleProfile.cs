using AutoMapper;
using CatalogService.Api.Contracts.Dtos;
using CatalogService.Api.Core.Domain;

namespace CatalogService.Api.Core.Application.Mapping
{
    public class VehicleProfile : Profile
    {
        public VehicleProfile()
        {
            CreateMap<VehicleDto, Vehicle>();
        }
    }
}
