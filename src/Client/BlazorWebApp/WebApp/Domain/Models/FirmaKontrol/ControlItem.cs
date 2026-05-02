namespace WebApp.Domain.Models.FirmaKontrol
{
    public class ControlItem
    {
        public int Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public bool IsChecked { get; set; }
        public ControlStatus Status { get; set; } = ControlStatus.Pending;
        public string? Note { get; set; }
    }
}
