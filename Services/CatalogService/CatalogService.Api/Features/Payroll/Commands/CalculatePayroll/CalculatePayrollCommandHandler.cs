using CatalogService.Api.Features.Payroll.Dtos.Responses;
using CatalogService.Api.Features.Payroll.Services;
using CatalogService.Api.Features.Payroll.Services.Interfaces;
using CatalogService.Api.Features.Payroll.Services.Models;
using CatalogService.Api.Infrastructure.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Payroll.Commands.CalculatePayroll
{
    public class CalculatePayrollCommandHandler : IRequestHandler<CalculatePayrollCommand, CalculatePayrollResponse>
    {
        private readonly CatalogContext _context;
        private readonly IPayrollCalculationEngine _payrollCalculationEngine;

        public CalculatePayrollCommandHandler(
            CatalogContext context,
            IPayrollCalculationEngine payrollCalculationEngine)
        {
            _context = context;
            _payrollCalculationEngine = payrollCalculationEngine;
        }

        public async Task<CalculatePayrollResponse> Handle(CalculatePayrollCommand request, CancellationToken cancellationToken)
        {
            var parameter = await _context.PayrollParameters
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Year == request.Year && x.IsActive, cancellationToken);

            if (parameter is null)
                throw new InvalidOperationException($"'{request.Year}' yılı için aktif payroll parametresi bulunamadı.");

            var taxBrackets = await _context.PayrollTaxBrackets
                .AsNoTracking()
                .Where(x => x.Year == request.Year)
                .OrderBy(x => x.Order)
                .ToListAsync(cancellationToken);

            if (!taxBrackets.Any())
                throw new InvalidOperationException($"'{request.Year}' yılı için vergi dilimi bulunamadı.");

            var disabilityExemption = await _context.PayrollDisabilityExemptions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Year == request.Year && x.DisabilityType == request.DisabilityType,
                    cancellationToken);

            var calculationContext = new PayrollCalculationContext
            {
                Parameter = parameter,
                TaxBrackets = taxBrackets,
                DisabilityExemption = disabilityExemption,
                IsManufacturingSector = request.IsManufacturingSector
            };

            var response = _payrollCalculationEngine.Calculate(request, calculationContext);

            var strategy = PayrollIncentiveStrategyFactory.Create(request.LawCode);
            strategy.EnrichEmployerCosts(response, calculationContext);

            return response;
        }
    }
}
