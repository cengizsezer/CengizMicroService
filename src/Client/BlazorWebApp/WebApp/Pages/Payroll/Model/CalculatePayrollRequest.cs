namespace WebApp.Pages.Payroll.Model
{
    public class CalculatePayrollRequest
    {
        public int Year { get; set; }
        public int CalculationType { get; set; }
        public int EmployeeType { get; set; }
        public bool HasMandatoryBes { get; set; }
        public int DisabilityType { get; set; }
        public int StartMonth { get; set; }
        public decimal PreviousCumulativeTaxBase { get; set; }
        public List<PayrollMonthInputRequest> Months { get; set; } = new();
    }

    public class PayrollMonthInputRequest
    {
        public int Month { get; set; }
        public decimal Amount { get; set; }
    }
}
