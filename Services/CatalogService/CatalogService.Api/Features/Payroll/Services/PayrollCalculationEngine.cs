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

            if (command.EmployeeType == PayrollEmployeeType.Honorarium)
            {
                foreach (var month in orderedMonths)
                {
                    HonorariumPayrollMonthResultDto monthResult;

                    if (command.CalculationType == PayrollCalculationType.GrossToNet)
                    {
                        monthResult = CalculateHonorariumGrossToNetMonth(
                            month.Month,
                            month.Amount,
                            cumulativeTaxBase,
                            ref minimumWageCumulativeTaxBase,
                            command,
                            context);
                    }
                    else
                    {
                        monthResult = CalculateHonorariumNetToGrossMonth(
                            month.Month,
                            month.Amount,
                            cumulativeTaxBase,
                            ref minimumWageCumulativeTaxBase,
                            command,
                            context);
                    }

                    cumulativeTaxBase = monthResult.CumulativeIncomeTaxBase;
                    response.HonorariumMonths.Add(monthResult);
                }

                response.HonorariumTotals = BuildHonorariumTotals(response.HonorariumMonths);
            }
            else
            {
                foreach (var month in orderedMonths)
                {
                    PayrollMonthResultDto monthResult;

                    if (command.CalculationType == PayrollCalculationType.GrossToNet)
                    {
                        monthResult = CalculateSalaryGrossToNetMonth(
                            month.Month,
                            month.Amount,
                            cumulativeTaxBase,
                            ref minimumWageCumulativeTaxBase,
                            command,
                            context);
                    }
                    else
                    {
                        monthResult = CalculateSalaryNetToGrossMonth(
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

                response.Totals = BuildSalaryTotals(response.Months);
            }

            return response;
        }

        private PayrollMonthResultDto CalculateSalaryGrossToNetMonth(
            int month,
            decimal inputAmount,
            decimal previousCumulativeTaxBase,
            ref decimal minimumWageCumulativeTaxBase,
            CalculatePayrollCommand command,
            PayrollCalculationContext context)
        {
            var grossSalary = Round2(inputAmount);

            var sgkEmployeeAmount = CalculateSgkEmployee(grossSalary, context, command.EmployeeType);
            var unemploymentEmployeeAmount = CalculateUnemploymentEmployee(grossSalary, context, command.EmployeeType);

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

            var incomeTaxExemption = (grossSalary > 0 && command.IncludeMinimumWageExemption)
     ? CalculateMinimumWageIncomeTaxExemption(ref minimumWageCumulativeTaxBase, context, command.EmployeeType)
     : 0m;

            var payableIncomeTax = Round2(Math.Max(0, calculatedIncomeTax - incomeTaxExemption));

            var calculatedStampTax = command.IncludeStampTax
    ? CalculateStampTax(grossSalary, context)
    : 0m;

            var stampTaxExemption = (grossSalary > 0 && command.IncludeStampTax && command.IncludeMinimumWageExemption)
       ? CalculateMinimumWageStampTaxExemption(context)
       : 0m;

            var payableStampTax = Round2(Math.Max(0, calculatedStampTax - stampTaxExemption));

            var besAmount = CalculateBes(
    grossSalary,
    context,
    command.HasMandatoryBes,
    command.EmployeeType);

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

                DisabilityExemptionAmount = disabilityExemptionAmount,

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

        private PayrollMonthResultDto CalculateSalaryNetToGrossMonth(
            int month,
            decimal targetNet,
            decimal previousCumulativeTaxBase,
            ref decimal minimumWageCumulativeTaxBase,
            CalculatePayrollCommand command,
            PayrollCalculationContext context)
        {
            var grossEstimate = SolveSalaryGrossFromTargetNet(
                targetNet,
                previousCumulativeTaxBase,
                minimumWageCumulativeTaxBase,
                command,
                context);

            return CalculateSalaryGrossToNetMonth(
                month,
                grossEstimate,
                previousCumulativeTaxBase,
                ref minimumWageCumulativeTaxBase,
                command,
                context);
        }

        private decimal SolveSalaryGrossFromTargetNet(
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

                var result = CalculateSalaryGrossToNetMonth(
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

        private HonorariumPayrollMonthResultDto CalculateHonorariumGrossToNetMonth(
            int month,
            decimal inputAmount,
            decimal previousCumulativeTaxBase,
            ref decimal minimumWageCumulativeTaxBase,
            CalculatePayrollCommand command,
            PayrollCalculationContext context)
        {
            var grossHonorarium = Round2(inputAmount);

            var incomeTaxBase = Round2(grossHonorarium);
            var cumulativeIncomeTaxBase = Round2(previousCumulativeTaxBase + incomeTaxBase);

            var calculatedIncomeTax = CalculateProgressiveIncomeTax(
                previousCumulativeTaxBase,
                incomeTaxBase,
                context);

            

            var incomeTaxExemption = (grossHonorarium > 0 && command.IncludeMinimumWageExemption)
    ? CalculateMinimumWageIncomeTaxExemption(ref minimumWageCumulativeTaxBase, context, command.EmployeeType)
    : 0m;

            var payableIncomeTax = Round2(Math.Max(0, calculatedIncomeTax - incomeTaxExemption));

            var calculatedStampTax = command.IncludeStampTax
      ? CalculateStampTax(grossHonorarium, context)
      : 0m;

            var stampTaxExemption = (grossHonorarium > 0 && command.IncludeStampTax)
      ? CalculateMinimumWageStampTaxExemption(context)
      : 0m;

            var payableStampTax = Round2(Math.Max(0, calculatedStampTax - stampTaxExemption));

            var totalDeductions = Round2(
                payableIncomeTax +
                payableStampTax);

            var netHonorarium = Round2(grossHonorarium - totalDeductions);

            var taxBurdenRate = grossHonorarium <= 0
                ? 0m
                : Round2((totalDeductions / grossHonorarium) * 100m);

            return new HonorariumPayrollMonthResultDto
            {
                Month = month,
                InputAmount = Round2(inputAmount),

                GrossHonorarium = grossHonorarium,

                IncomeTaxBase = incomeTaxBase,
                CumulativeIncomeTaxBase = cumulativeIncomeTaxBase,

                CalculatedIncomeTax = calculatedIncomeTax,
                IncomeTaxExemption = incomeTaxExemption,
                PayableIncomeTax = payableIncomeTax,

                CalculatedStampTax = calculatedStampTax,
                StampTaxExemption = stampTaxExemption,
                PayableStampTax = payableStampTax,

                TotalDeductions = totalDeductions,
                TaxBurdenRate = taxBurdenRate,
                NetHonorarium = netHonorarium
            };
        }

        private decimal CalculateBes(decimal grossSalary, PayrollCalculationContext context, bool hasMandatoryBes, PayrollEmployeeType employeeType)
        {
            if (!hasMandatoryBes) return 0m;
            if (employeeType != PayrollEmployeeType.Normal) return 0m;
            return Round2(grossSalary * context.Parameter.BesEmployeeRate);
        }

        private HonorariumPayrollMonthResultDto CalculateHonorariumNetToGrossMonth(
            int month,
            decimal targetNet,
            decimal previousCumulativeTaxBase,
            ref decimal minimumWageCumulativeTaxBase,
            CalculatePayrollCommand command,
            PayrollCalculationContext context)
        {
            var grossEstimate = SolveHonorariumGrossFromTargetNet(
                targetNet,
                previousCumulativeTaxBase,
                minimumWageCumulativeTaxBase,
                command,
                context);

            return CalculateHonorariumGrossToNetMonth(
                month,
                grossEstimate,
                previousCumulativeTaxBase,
                ref minimumWageCumulativeTaxBase,
                command,
                context);
        }

        private decimal SolveHonorariumGrossFromTargetNet(
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

                var result = CalculateHonorariumGrossToNetMonth(
                    month: 0,
                    inputAmount: mid,
                    previousCumulativeTaxBase: previousCumulativeTaxBase,
                    minimumWageCumulativeTaxBase: ref tempMinimumWageCumulativeTaxBase,
                    command: command,
                    context: context);

                if (Math.Abs(result.NetHonorarium - targetNet) < 0.01m)
                    return mid;

                if (result.NetHonorarium < targetNet)
                    low = mid;
                else
                    high = mid;
            }

            return Round2((low + high) / 2m);
        }

        private decimal CalculateSgkEmployee(
    decimal grossSalary,
    PayrollCalculationContext context,
    PayrollEmployeeType employeeType)
        {
            var rate = employeeType == PayrollEmployeeType.Retired
                ? context.Parameter.RetiredSgkEmployeeRate
                : context.Parameter.SgkEmployeeRate;

            return Round2(grossSalary * rate);
        }

        private decimal CalculateUnemploymentEmployee(
     decimal grossSalary,
     PayrollCalculationContext context,
     PayrollEmployeeType employeeType)
        {
            var rate = employeeType == PayrollEmployeeType.Retired
                ? context.Parameter.RetiredUnemploymentEmployeeRate
                : context.Parameter.UnemploymentEmployeeRate;

            return Round2(grossSalary * rate);
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

       

        private PayrollTotalsDto BuildSalaryTotals(List<PayrollMonthResultDto> months)
        {
            return new PayrollTotalsDto
            {
                TotalGrossSalary = Round2(months.Sum(x => x.GrossSalary)),
                TotalSgkEmployeeAmount = Round2(months.Sum(x => x.SgkEmployeeAmount)),
                TotalUnemploymentEmployeeAmount = Round2(months.Sum(x => x.UnemploymentEmployeeAmount)),
                TotalDisabilityExemptionAmount = Round2(months.Sum(x => x.DisabilityExemptionAmount)),
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

        private HonorariumPayrollTotalsDto BuildHonorariumTotals(List<HonorariumPayrollMonthResultDto> months)
        {
            return new HonorariumPayrollTotalsDto
            {
                TotalGrossHonorarium = Round2(months.Sum(x => x.GrossHonorarium)),
                TotalIncomeTaxBase = Round2(months.Sum(x => x.IncomeTaxBase)),
                TotalCalculatedIncomeTax = Round2(months.Sum(x => x.CalculatedIncomeTax)),
                TotalIncomeTaxExemption = Round2(months.Sum(x => x.IncomeTaxExemption)),
                TotalPayableIncomeTax = Round2(months.Sum(x => x.PayableIncomeTax)),
                TotalCalculatedStampTax = Round2(months.Sum(x => x.CalculatedStampTax)),
                TotalStampTaxExemption = Round2(months.Sum(x => x.StampTaxExemption)),
                TotalPayableStampTax = Round2(months.Sum(x => x.PayableStampTax)),
                TotalDeductions = Round2(months.Sum(x => x.TotalDeductions)),
                TotalNetHonorarium = Round2(months.Sum(x => x.NetHonorarium)),
                AverageTaxBurdenRate = months.Count == 0 ? 0m : Round2(months.Average(x => x.TaxBurdenRate))
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

            var sgkEmployeeAmount = Round2(gross * context.Parameter.SgkEmployeeRate);
            var unemploymentEmployeeAmount = Round2(gross * context.Parameter.UnemploymentEmployeeRate);

            var baseAmount = gross - sgkEmployeeAmount - unemploymentEmployeeAmount;
            return Round2(Math.Max(0, baseAmount));
        }

        private decimal CalculateMinimumWageIncomeTaxExemption(
     ref decimal minimumWageCumulativeTaxBase,
     PayrollCalculationContext context,
     PayrollEmployeeType employeeType)
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
