using AutoMapper;
using IdentityService.Application.Models.Tenants;
using IdentityService.Domain.Entities;

namespace IdentityService.Application.Mapping
{
    public class IdentityMappingProfile : Profile
    {
        public IdentityMappingProfile()
        {
            CreateMap<Tenant, TenantDto>().ReverseMap();
            CreateMap<Permission, PermissionDto>().ReverseMap();
            CreateMap<Role, RoleDto>().ReverseMap();
            // UserTenant ↔ UserTenantDto, UserTenantRole ↔ UserTenantRoleDto vs. ihtiyaca göre
        }
    }

}
