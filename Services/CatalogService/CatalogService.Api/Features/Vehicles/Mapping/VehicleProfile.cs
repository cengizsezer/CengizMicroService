using AutoMapper;
using CatalogService.Api.Features.Vehicles.Domain;
using CatalogService.Api.Features.Vehicles.DTO;

namespace CatalogService.Api.Features.Vehicles.Mapping
{
    public class VehicleProfile : Profile
    {
        public VehicleProfile()
        {
            CreateMap<VehicleDto, Vehicle>();
        }
    }
}
