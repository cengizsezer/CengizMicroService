namespace WebApp.Shared.Dto.Scheduling
{
    public class JobDto
    {
        public long Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public DateTime CreatedAt { get; set; }
        public JobStatus Status { get; set; }
        public List<JobAssignmentDto> Assignments { get; set; } = new();
        public List<JobAttachmentDto> Attachments { get; set; } = new();
    }
}
