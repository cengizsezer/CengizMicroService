using CatalogService.Api.Features.Declarations.Dtos;
using CatalogService.Api.Features.Declarations.Entities;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;

namespace CatalogService.Api.Features.Declarations.Services
{
    public class DeclarationCommandService : IDeclarationCommandService
    {
        private readonly CatalogContext _context;

        public DeclarationCommandService(CatalogContext context)
        {
            _context = context;
        }

        public async Task<int> CreateAsync(CreateDeclarationRequest request)
        {
            var entity = new Declaration
            {
                TenantNo = request.TenantNo?.Trim() ?? string.Empty,
                CompanyName = request.CompanyName?.Trim() ?? string.Empty,
                DeclarationType = request.DeclarationType?.Trim() ?? string.Empty,
                Year = request.Year,
                Month = request.Month,
                Amount = request.Amount,
                DueDate = request.DueDate,
                DeclarationStatus = request.DeclarationStatus,
                PaymentStatus = request.PaymentStatus,
                PaymentDate = request.PaymentDate,
                Note = request.Note,
                CustomerCompanyId = request.CustomerCompanyId,
                
            };

            _context.Declarations.Add(entity);
            await _context.SaveChangesAsync();

            return entity.Id;
        }

        public async Task UpdateAsync(int id, UpdateDeclarationRequest request)
        {
            var entity = await _context.Declarations.FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
                throw new Exception("Declaration not found");

            entity.TenantNo = request.TenantNo?.Trim() ?? string.Empty;
            entity.CompanyName = request.CompanyName?.Trim() ?? string.Empty;
            entity.DeclarationType = request.DeclarationType?.Trim() ?? string.Empty;
            entity.Year = request.Year;
            entity.Month = request.Month;
            entity.Amount = request.Amount;
            entity.DueDate = request.DueDate;
            entity.DeclarationStatus = request.DeclarationStatus;
            entity.PaymentStatus = request.PaymentStatus;
            entity.PaymentDate = request.PaymentDate;
            entity.Note = request.Note;
            entity.CustomerCompanyId = request.CustomerCompanyId;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _context.Declarations.FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
                throw new Exception("Declaration not found");

            _context.Declarations.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
}
