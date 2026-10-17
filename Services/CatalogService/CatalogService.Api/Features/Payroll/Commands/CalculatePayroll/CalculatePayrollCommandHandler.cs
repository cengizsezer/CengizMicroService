using CatalogService.Api.Features.Payroll.Configuration;
using CatalogService.Api.Features.Payroll.Dtos.Responses;
using CatalogService.Api.Features.Payroll.Enums;
using CatalogService.Api.Features.Payroll.Services;
using CatalogService.Api.Features.Payroll.Services.Interfaces;
using CatalogService.Api.Features.Payroll.Services.Models;
using CatalogService.Api.Features.Payroll.Services.Strategies;
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

            var hatalar = PayrollTesvikDogrulama.Hatalar(request);
            if (hatalar.Count > 0)
                throw new PayrollGirdiHatasi(string.Join(" ", hatalar));

            var disabilityExemption = yearConfig.DisabilityExemptions
                .FirstOrDefault(x => x.DisabilityType == request.DisabilityType);

            var kural = PayrollTesvikKurallari.Bul(request.LawCode)!;
            var tesvik = kural.Coz(request.TesvikAltSecimi);
            var yeniKurallar = request.Year >= PayrollTesvikKurallari.YeniKurallarYili;
            var uyarilar = new List<string>();

            // 2026+: SGDP'li emekliye bazı teşvikler uygulanmaz — hesap teşviksiz yapılır, uyarı verilir.
            var uygulanir = true;
            if (yeniKurallar && request.EmployeeType == PayrollEmployeeType.Retired && kural.EmekliyeUygulanmaz)
            {
                uygulanir = false;
                uyarilar.Add($"Emekli (SGDP) çalışanda \"{kural.Ad}\" uygulanmaz; hesap teşviksiz yapıldı.");
            }
            if (request.EmployeeType == PayrollEmployeeType.Honorarium && kural.Anahtar != PayrollTesvikKurallari.Indirimsiz)
                uyarilar.Add("Huzur hakkında teşvik uygulanmaz.");

            var calculationContext = new PayrollCalculationContext
            {
                Parameter = yearConfig.Parameter,
                TaxBrackets = yearConfig.TaxBrackets.ToList(),
                DisabilityExemption = disabilityExemption,
                IsManufacturingSector = request.IsManufacturingSector,
                EmployeeTaxIncentive = uygulanir
                    ? PayrollEmployeeTaxIncentive.Resolve(
                        tesvik,
                        request.ArGeEgitimDurumu,
                        sigortaliKurallariUygulanir: yeniKurallar,
                        ucretTavani: yearConfig.IncomeTaxIncentiveCeilingMultiplier * yearConfig.Parameter.MinimumWageGrossAmount)
                    : PayrollEmployeeTaxIncentive.None,
                RetiredSgdpEmployerRate = request.EmployeeType == PayrollEmployeeType.Retired
                    ? yearConfig.RetiredSgdpEmployerRate
                    : null
            };

            var response = _payrollCalculationEngine.Calculate(request, calculationContext);
            response.Warnings = uyarilar;

            // 2026+: uygulanmayan teşvik de yeni hesaplayıcıdan "teşviksiz" geçer (SGDP işveren primi orada).
            IPayrollIncentiveStrategy strategy = yeniKurallar
                ? new TesvikKuraliStratejisi(
                    uygulanir ? tesvik : PayrollTesvikKurallari.Bul(PayrollTesvikKurallari.Indirimsiz)!.Coz(null),
                    new TesvikGirdileri(
                        request.Year,
                        request.TurizmPrimGunu,
                        request.IsgDestegiTutari,
                        request.ManuelTesvikTuru,
                        request.ManuelTesvikDegeri))
                : PayrollIncentiveStrategyFactory.Create(request.LawCode);
            strategy.EnrichEmployerCosts(response, calculationContext);

            return Task.FromResult(response);
        }
    }
}
