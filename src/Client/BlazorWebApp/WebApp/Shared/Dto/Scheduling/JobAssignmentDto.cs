namespace WebApp.Shared.Dto.Scheduling
{
    public class JobAssignmentDto
    {
        public long Id { get; set; }
        public string UserId { get; set; } = "";
        public string UserName { get; set; } = "";
        public JobStatus Status { get; set; }
    }
}
