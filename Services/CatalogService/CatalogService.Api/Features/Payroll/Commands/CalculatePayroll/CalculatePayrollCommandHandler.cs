using CatalogService.Api.Features.Payroll.Configuration;
using CatalogService.Api.Features.Payroll.Dtos.Responses;
using CatalogService.Api.Features.Payroll.Services;
using CatalogService.Api.Features.Payroll.Services.Interfaces;
using CatalogService.Api.Features.Payroll.Services.Models;
using MediatR;

namespace CatalogService.Api.Features.Payroll.Commands.CalculatePayroll
{
    public class CalculatePayrollCommandHandler : IRequestHandler<CalculatePayrollCommand, CalculatePayrollResponse>
    {
        private readonly IPayrollCalculationEngine _payrollCalculationEngine;

        public CalculatePayrollCommandHandler(IPayrollCalculationEngine payrollCalculationEngine)
        {
            _payrollCalculationEngine = payrollCalculationEngine;
        }

        public Task<CalculatePayrollResponse> Handle(CalculatePayrollCommand request, CancellationToken cancellationToken)
        {
            if (!PayrollYearConfigStore.All.TryGetValue(request.Year, out var yearConfig))
                throw new InvalidOperationException($"'{request.Year}' yılı için payroll konfigürasyonu bulunamadı.");

            if (yearConfig.TaxBrackets.Count == 0)
                throw new InvalidOperationException($"'{request.Year}' yılı için vergi dilimi tanımlı değil.");

            var disabilityExemption = yearConfig.DisabilityExemptions
                .FirstOrDefault(x => x.DisabilityType == request.DisabilityType);

            var calculationContext = new PayrollCalculationContext
            {
                Parameter = yearConfig.Parameter,
                TaxBrackets = yearConfig.TaxBrackets.ToList(),
                DisabilityExemption = disabilityExemption,
                IsManufacturingSector = request.IsManufacturingSector
            };

            var response = _payrollCalculationEngine.Calculate(request, calculationContext);

            var strategy = PayrollIncentiveStrategyFactory.Create(request.LawCode);
            strategy.EnrichEmployerCosts(response, calculationContext);

            return Task.FromResult(response);
        }
    }
}
