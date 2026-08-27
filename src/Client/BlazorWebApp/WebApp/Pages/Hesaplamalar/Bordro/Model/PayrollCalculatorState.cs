namespace WebApp.Pages.Hesaplamalar.Bordro.Model
{
    public class PayrollCalculatorState
    {
        public int Year { get; set; } = 2026;
        public int StartMonth { get; set; } = 1;
        public int CalculationType { get; set; } = 1;
        public int EmployeeType { get; set; } = 1;
        public bool HasMandatoryBes { get; set; }
        public int DisabilityType { get; set; } = 0;
        public decimal PreviousCumulativeTaxBase { get; set; } = 0;

        public List<PayrollMonthInputModel> Months { get; set; } = new();
    }
}
