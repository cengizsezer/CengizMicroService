using FluentValidation;

namespace CatalogService.Api.Features.Payroll.Commands.CalculatePayroll
{
    public class CalculatePayrollCommandValidator : AbstractValidator<CalculatePayrollCommand>
    {
        public CalculatePayrollCommandValidator()
        {
            RuleFor(x => x.Year)
                .GreaterThan(2000);

            RuleFor(x => x.StartMonth)
                .InclusiveBetween(1, 12);

            RuleFor(x => x.Months)
                .NotNull()
                .Must(months => months != null && months.Count > 0)
                .WithMessage("En az bir maaş girişi yapılmalıdır.");

            RuleForEach(x => x.Months)
                .ChildRules(month =>
                {
                    month.RuleFor(x => x.Month)
                        .InclusiveBetween(1, 12);

                    month.RuleFor(x => x.Amount)
                        .GreaterThanOrEqualTo(0);
                });

            RuleFor(x => x.Months)
                .Must(months => months.Select(x => x.Month).Distinct().Count() == months.Count)
                .WithMessage("Aylar tekrarlı olamaz.");

            RuleFor(x => x)
                .Must(x => x.Months.All(m => m.Month >= x.StartMonth))
                .WithMessage("Girilen aylar başlangıç ayından küçük olamaz.");
        }
    }
}
