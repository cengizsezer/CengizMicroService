using CatalogService.Api.Features.Declarations.Dtos;
using CatalogService.Api.Features.Declarations.Entities;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Declarations.Services
{
    public class DeclarationQueryService : IDeclarationQueryService
    {
        private readonly CatalogContext _context;

        public DeclarationQueryService(CatalogContext context)
        {
            _context = context;
        }

        public async Task<List<CompanyMonthlySummaryDto>> GetMonthlySummaryAsync(
            int year,
            int month,
            int? customerCompanyId = null,
            string? declarationType = null)
        {
            var query = _context.Declarations
                .AsNoTracking()
                .Where(x => x.Year == year && x.Month == month);

            if (customerCompanyId.HasValue)
            {
                query = query.Where(x => x.CustomerCompanyId == customerCompanyId.Value);
            }

            if (!string.IsNullOrWhiteSpace(declarationType))
            {
                declarationType = declarationType.Trim();
                query = query.Where(x => x.DeclarationType == declarationType);
            }

            var declarations = await query
                .OrderBy(x => x.CompanyName)
                .ThenBy(x => x.DueDate)
                .ToListAsync();

            var result = declarations
                .GroupBy(x => new { x.CustomerCompanyId, x.TenantNo, x.CompanyName })
                .Select(g => new CompanyMonthlySummaryDto
                {
                    CustomerCompanyId = g.Key.CustomerCompanyId,
                    TenantNo = g.Key.TenantNo,
                    CompanyName = g.Key.CompanyName,
                    Year = year,
                    Month = month,
                    DeclarationCount = g.Count(),
                    ApprovedCount = g.Count(x => x.DeclarationStatus == DeclarationStatus.Approved),
                    PaidCount = g.Count(x => x.PaymentStatus == PaymentStatus.Paid),
                    TotalAmount = g.Sum(x => x.Amount),
                    PaidAmount = g.Where(x => x.PaymentStatus == PaymentStatus.Paid).Sum(x => x.Amount),
                    PendingAmount = g.Where(x => x.PaymentStatus != PaymentStatus.Paid).Sum(x => x.Amount),
                    Declarations = g.Select(x => new DeclarationDto
                    {
                        Id = x.Id,
                        CustomerCompanyId = x.CustomerCompanyId,
                        TenantNo = x.TenantNo,
                        CompanyName = x.CompanyName,
                        DeclarationType = x.DeclarationType,
                        Year = x.Year,
                        Month = x.Month,
                        Amount = x.Amount,
                        DueDate = x.DueDate,
                        DeclarationStatus = x.DeclarationStatus,
                        PaymentStatus = x.PaymentStatus,
                        PaymentDate = x.PaymentDate,
                        Note = x.Note
                    })
                    .OrderBy(x => x.DueDate)
                    .ToList()
                })
                .OrderBy(x => x.CompanyName)
                .ToList();

            return result;
        }

        public async Task<YearlyTaxSummaryDto> GetYearlySummaryAsync(int year, int? customerCompanyId = null)
        {
            var query = _context.Declarations
                .AsNoTracking()
                .Where(x => x.Year == year);

            if (customerCompanyId.HasValue)
            {
                query = query.Where(x => x.CustomerCompanyId == customerCompanyId.Value);
            }

            var declarations = await query.ToListAsync();

            return new YearlyTaxSummaryDto
            {
                Year = year,
                TotalAmount = declarations.Sum(x => x.Amount),
                PaidAmount = declarations.Where(x => x.PaymentStatus == PaymentStatus.Paid).Sum(x => x.Amount),
                PendingAmount = declarations.Where(x => x.PaymentStatus != PaymentStatus.Paid).Sum(x => x.Amount),
                TotalCompanyCount = declarations
                    .Select(x => x.CustomerCompanyId)
                    .Distinct()
                    .Count(),
                TotalDeclarationCount = declarations.Count
            };
        }

        public async Task<List<CompanyYearlySummaryDto>> GetCompanyYearlySummaryAsync(int year)
        {
            var declarations = await _context.Declarations
                .AsNoTracking()
                .Where(x => x.Year == year)
                .ToListAsync();

            var result = declarations
                .GroupBy(x => new { x.CustomerCompanyId, x.TenantNo, x.CompanyName })
                .Select(g => new CompanyYearlySummaryDto
                {
                    CustomerCompanyId = g.Key.CustomerCompanyId,
                    TenantNo = g.Key.TenantNo,
                    CompanyName = g.Key.CompanyName,
                    Year = year,
                    DeclarationCount = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount),
                    PaidAmount = g.Where(x => x.PaymentStatus == PaymentStatus.Paid).Sum(x => x.Amount),
                    PendingAmount = g.Where(x => x.PaymentStatus != PaymentStatus.Paid).Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.TotalAmount)
                .ToList();

            return result;
        }
    }
}