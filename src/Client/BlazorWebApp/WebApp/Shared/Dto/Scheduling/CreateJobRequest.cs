namespace WebApp.Shared.Dto.Scheduling
{
    public class CreateJobRequest
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public IList<string> AssigneeIds { get; set; } = new List<string>();

        public bool AlarmEnabled { get; set; }
        public bool RemindOneDayBefore { get; set; }
        public bool RemindOnDay { get; set; }
        public string Timezone { get; set; } = "Europe/Istanbul";

        // tek hedef adres
        public string RecipientEmail { get; set; } = "";
    }
}
