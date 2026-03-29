using CatalogService.Api.Features.Payroll.Commands.CalculatePayroll;
using CatalogService.Api.Features.Payroll.Dtos.Requests;
using CatalogService.Api.Features.Payroll.Dtos.Responses;
using CatalogService.Api.Features.Payroll.Enums;
using CatalogService.Api.Features.Payroll.Services.Interfaces;
using CatalogService.Api.Features.Payroll.Services.Models;

namespace CatalogService.Api.Features.Payroll.Services
{
    public class PayrollCalculationEngine : IPayrollCalculationEngine
    {
        public CalculatePayrollResponse Calculate(
            CalculatePayrollCommand command,
            PayrollCalculationContext context)
        {
            var sourceMonths = (command.Months ?? new List<PayrollMonthInputDto>())
                .OrderBy(x => x.Month)
                .ToList();

            var monthMap = sourceMonths.ToDictionary(x => x.Month, x => x.Amount);

            decimal currentAmount = 0m;
            var orderedMonths = new List<PayrollMonthInputDto>();

            for (int monthNo = 1; monthNo <= 12; monthNo++)
            {
                if (monthNo < command.StartMonth)
                {
                    orderedMonths.Add(new PayrollMonthInputDto
                    {
                        Month = monthNo,
                        Amount = 0m
                    });
                    continue;
                }

                if (monthMap.TryGetValue(monthNo, out var enteredAmount))
                {
                    currentAmount = enteredAmount;
                }

                orderedMonths.Add(new PayrollMonthInputDto
                {
                    Month = monthNo,
                    Amount = currentAmount
                });
            }

            var response = new CalculatePayrollResponse
            {
                Year = command.Year,
                CalculationType = command.CalculationType,
                EmployeeType = command.EmployeeType,
                HasMandatoryBes = command.HasMandatoryBes,
                DisabilityType = command.DisabilityType
            };

            decimal cumulativeTaxBase = command.PreviousCumulativeTaxBase;
            decimal minimumWageCumulativeTaxBase = 0m;

            foreach (var month in orderedMonths)
            {
                PayrollMonthResultDto monthResult;

                if (command.CalculationType == PayrollCalculationType.GrossToNet)
                {
                    monthResult = CalculateGrossToNetMonth(
                        month.Month,
                        month.Amount,
                        cumulativeTaxBase,
                        ref minimumWageCumulativeTaxBase,
                        command,
                        context);
                }
                else
                {
                    monthResult = CalculateNetToGrossMonth(
                        month.Month,
                        month.Amount,
                        cumulativeTaxBase,
                        ref minimumWageCumulativeTaxBase,
                        command,
                        context);
                }

                cumulativeTaxBase = monthResult.CumulativeIncomeTaxBase;
                response.Months.Add(monthResult);
            }

            response.Totals = BuildTotals(response.Months);

            return response;
        }

        private PayrollMonthResultDto CalculateGrossToNetMonth(
     int month,
     decimal inputAmount,
     decimal previousCumulativeTaxBase,
     ref decimal minimumWageCumulativeTaxBase,
     CalculatePayrollCommand command,
     PayrollCalculationContext context)
        {
            var grossSalary = Round2(inputAmount);

            var sgkEmployeeAmount = CalculateSgkEmployee(grossSalary, context);
            var unemploymentEmployeeAmount = CalculateUnemploymentEmployee(grossSalary, context);

            var disabilityExemptionAmount = grossSalary > 0
                ? GetDisabilityExemptionAmount(context)
                : 0m;

            var incomeTaxBase = CalculateIncomeTaxBase(
                grossSalary,
                sgkEmployeeAmount,
                unemploymentEmployeeAmount,
                disabilityExemptionAmount);

            var cumulativeIncomeTaxBase = Round2(previousCumulativeTaxBase + incomeTaxBase);

            var calculatedIncomeTax = CalculateProgressiveIncomeTax(
                previousCumulativeTaxBase,
                incomeTaxBase,
                context);

            var incomeTaxExemption = grossSalary > 0
                ? CalculateMinimumWageIncomeTaxExemption(ref minimumWageCumulativeTaxBase, context)
                : 0m;

            var payableIncomeTax = Round2(Math.Max(0, calculatedIncomeTax - incomeTaxExemption));

            var calculatedStampTax = CalculateStampTax(grossSalary, context);

            var stampTaxExemption = grossSalary > 0
                ? CalculateMinimumWageStampTaxExemption(context)
                : 0m;

            var payableStampTax = Round2(Math.Max(0, calculatedStampTax - stampTaxExemption));

            var besAmount = CalculateBes(grossSalary, context, command.HasMandatoryBes);

            var totalDeductions = Round2(
                sgkEmployeeAmount +
                unemploymentEmployeeAmount +
                payableIncomeTax +
                payableStampTax +
                besAmount);

            var netSalary = Round2(grossSalary - totalDeductions);

            return new PayrollMonthResultDto
            {
                Month = month,
                InputAmount = Round2(inputAmount),

                GrossSalary = grossSalary,
                SgkEmployeeAmount = sgkEmployeeAmount,
                UnemploymentEmployeeAmount = unemploymentEmployeeAmount,

                IncomeTaxBase = incomeTaxBase,
                CumulativeIncomeTaxBase = cumulativeIncomeTaxBase,

                CalculatedIncomeTax = calculatedIncomeTax,
                IncomeTaxExemption = incomeTaxExemption,
                PayableIncomeTax = payableIncomeTax,

                CalculatedStampTax = calculatedStampTax,
                StampTaxExemption = stampTaxExemption,
                PayableStampTax = payableStampTax,

                BesAmount = besAmount,
                TotalDeductions = totalDeductions,
                NetSalary = netSalary
            };
        }
        private PayrollMonthResultDto CalculateNetToGrossMonth(
     int month,
     decimal targetNet,
     decimal previousCumulativeTaxBase,
     ref decimal minimumWageCumulativeTaxBase,
     CalculatePayrollCommand command,
     PayrollCalculationContext context)
        {
            var grossEstimate = SolveGrossFromTargetNet(
                targetNet,
                previousCumulativeTaxBase,
                minimumWageCumulativeTaxBase,
                command,
                context);

            return CalculateGrossToNetMonth(
                month,
                grossEstimate,
                previousCumulativeTaxBase,
                ref minimumWageCumulativeTaxBase,
                command,
                context);
        }

        private decimal SolveGrossFromTargetNet(
     decimal targetNet,
     decimal previousCumulativeTaxBase,
     decimal minimumWageCumulativeTaxBase,
     CalculatePayrollCommand command,
     PayrollCalculationContext context)
        {
            decimal low = targetNet;
            decimal high = targetNet * 3;

            for (int i = 0; i < 60; i++)
            {
                var mid = Round2((low + high) / 2m);
                var tempMinimumWageCumulativeTaxBase = minimumWageCumulativeTaxBase;

                var result = CalculateGrossToNetMonth(
                    month: 0,
                    inputAmount: mid,
                    previousCumulativeTaxBase: previousCumulativeTaxBase,
                    minimumWageCumulativeTaxBase: ref tempMinimumWageCumulativeTaxBase,
                    command: command,
                    context: context);

                if (Math.Abs(result.NetSalary - targetNet) < 0.01m)
                    return mid;

                if (result.NetSalary < targetNet)
                    low = mid;
                else
                    high = mid;
            }

            return Round2((low + high) / 2m);
        }
        private decimal CalculateSgkEmployee(decimal grossSalary, PayrollCalculationContext context)
        {
            return Round2(grossSalary * context.Parameter.SgkEmployeeRate);
        }

        private decimal CalculateUnemploymentEmployee(decimal grossSalary, PayrollCalculationContext context)
        {
            return Round2(grossSalary * context.Parameter.UnemploymentEmployeeRate);
        }

        private decimal CalculateIncomeTaxBase(
      decimal grossSalary,
      decimal sgkEmployeeAmount,
      decimal unemploymentEmployeeAmount,
      decimal disabilityExemptionAmount)
        {
            var baseAmount = grossSalary - sgkEmployeeAmount - unemploymentEmployeeAmount - disabilityExemptionAmount;
            return Round2(Math.Max(0, baseAmount));
        }

        private decimal CalculateProgressiveIncomeTax(
            decimal previousCumulativeTaxBase,
            decimal currentIncomeTaxBase,
            PayrollCalculationContext context)
        {
            decimal remainingBase = currentIncomeTaxBase;
            decimal runningBase = previousCumulativeTaxBase;
            decimal totalTax = 0m;

            foreach (var bracket in context.TaxBrackets.OrderBy(x => x.Order))
            {
                var bracketMin = bracket.MinAmount;
                var bracketMax = bracket.MaxAmount ?? decimal.MaxValue;

                if (runningBase >= bracketMax)
                    continue;

                var taxableInThisBracket = Math.Min(
                    bracketMax - Math.Max(runningBase, bracketMin),
                    remainingBase);

                if (taxableInThisBracket <= 0)
                    continue;

                totalTax += taxableInThisBracket * bracket.TaxRate;
                remainingBase -= taxableInThisBracket;
                runningBase += taxableInThisBracket;

                if (remainingBase <= 0)
                    break;
            }

            return Round2(totalTax);
        }

       

        private decimal CalculateStampTax(decimal grossSalary, PayrollCalculationContext context)
        {
            return Round2(grossSalary * context.Parameter.StampTaxRate);
        }

       

        private decimal CalculateBes(
            decimal grossSalary,
            PayrollCalculationContext context,
            bool hasMandatoryBes)
        {
            if (!hasMandatoryBes)
                return 0m;

            return Round2(grossSalary * context.Parameter.BesEmployeeRate);
        }

        private PayrollTotalsDto BuildTotals(List<PayrollMonthResultDto> months)
        {
            return new PayrollTotalsDto
            {
                TotalGrossSalary = Round2(months.Sum(x => x.GrossSalary)),
                TotalSgkEmployeeAmount = Round2(months.Sum(x => x.SgkEmployeeAmount)),
                TotalUnemploymentEmployeeAmount = Round2(months.Sum(x => x.UnemploymentEmployeeAmount)),
                TotalIncomeTaxBase = Round2(months.Sum(x => x.IncomeTaxBase)),
                TotalCalculatedIncomeTax = Round2(months.Sum(x => x.CalculatedIncomeTax)),
                TotalIncomeTaxExemption = Round2(months.Sum(x => x.IncomeTaxExemption)),
                TotalPayableIncomeTax = Round2(months.Sum(x => x.PayableIncomeTax)),
                TotalCalculatedStampTax = Round2(months.Sum(x => x.CalculatedStampTax)),
                TotalStampTaxExemption = Round2(months.Sum(x => x.StampTaxExemption)),
                TotalPayableStampTax = Round2(months.Sum(x => x.PayableStampTax)),
                TotalBesAmount = Round2(months.Sum(x => x.BesAmount)),
                TotalDeductions = Round2(months.Sum(x => x.TotalDeductions)),
                TotalNetSalary = Round2(months.Sum(x => x.NetSalary))
            };
        }

        private decimal Round2(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private decimal GetDisabilityExemptionAmount(PayrollCalculationContext context)
        {
            return Round2(context.DisabilityExemption?.MonthlyExemptionAmount ?? 0m);
        }

        private decimal CalculateMinimumWageIncomeTaxBase(PayrollCalculationContext context)
        {
            var gross = context.Parameter.MinimumWageGrossAmount;
            var sgk = CalculateSgkEmployee(gross, context);
            var unemployment = CalculateUnemploymentEmployee(gross, context);

            var baseAmount = gross - sgk - unemployment;
            return Round2(Math.Max(0, baseAmount));
        }

        private decimal CalculateMinimumWageIncomeTaxExemption(
            ref decimal minimumWageCumulativeTaxBase,
            PayrollCalculationContext context)
        {
            var minimumWageTaxBase = CalculateMinimumWageIncomeTaxBase(context);

            var exemption = CalculateProgressiveIncomeTax(
                minimumWageCumulativeTaxBase,
                minimumWageTaxBase,
                context);

            minimumWageCumulativeTaxBase = Round2(minimumWageCumulativeTaxBase + minimumWageTaxBase);

            return Round2(exemption);
        }

        private decimal CalculateMinimumWageStampTaxExemption(PayrollCalculationContext context)
        {
            return Round2(context.Parameter.MinimumWageGrossAmount * context.Parameter.StampTaxRate);
        }

    }
}
