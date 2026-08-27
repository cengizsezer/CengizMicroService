namespace WebApp.Pages.Hesaplamalar.Bordro.Model
{
    public class PayrollMonthInputModel
    {
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public bool IsEdited { get; set; }
    }
}
