namespace WebApp.Pages.Payroll.Model
{
    public class CalculatePayrollRequest
    {
        public int Year { get; set; }
        public int CalculationType { get; set; }
        public int EmployeeType { get; set; }

        public bool HasMandatoryBes { get; set; }
        public int DisabilityType { get; set; }

        public bool IncludeMinimumWageExemption { get; set; } = true;
        public bool IncludeStampTax { get; set; } = true;
        public bool IncludeEmployerCost { get; set; } = false;

        public int StartMonth { get; set; } = 1;
        public decimal PreviousCumulativeTaxBase { get; set; }

        public List<PayrollMonthInputRequest> Months { get; set; } = new();

        public string LawCode { get; set; } = "00000";
        public bool IsManufacturingSector { get; set; } = false;
    }

    public class PayrollMonthInputRequest
    {
        public int Month { get; set; }
        public decimal Amount { get; set; }
    }
}
