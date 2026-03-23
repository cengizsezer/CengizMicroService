using CatalogService.Api.Features.Declarations.Dtos;
using CatalogService.Api.Features.Declarations.Entities;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

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
                TenantNo = request.TenantNo.Trim(),
                CompanyName = request.CompanyName.Trim(),
                DeclarationType = request.DeclarationType.Trim(),
                Year = request.Year,
                Month = request.Month,
                Amount = request.Amount,
                DueDate = request.DueDate,
                DeclarationStatus = request.DeclarationStatus,
                PaymentStatus = request.PaymentStatus,
                PaymentDate = request.PaymentDate,
                Note = request.Note
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

            entity.TenantNo = request.TenantNo.Trim();
            entity.CompanyName = request.CompanyName.Trim();
            entity.DeclarationType = request.DeclarationType.Trim();
            entity.Year = request.Year;
            entity.Month = request.Month;
            entity.Amount = request.Amount;
            entity.DueDate = request.DueDate;
            entity.DeclarationStatus = request.DeclarationStatus;
            entity.PaymentStatus = request.PaymentStatus;
            entity.PaymentDate = request.PaymentDate;
            entity.Note = request.Note;

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
