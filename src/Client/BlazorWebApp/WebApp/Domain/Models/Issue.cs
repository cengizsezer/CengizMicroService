using System.Reflection.Emit;

namespace WebApp.Domain.Models
{
    public class Issue
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public IssueState State { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public MockUser User { get; set; }
        public List<MockUser> Assignees { get; set; } = new();
        public List<Label> Labels { get; set; } = new();
    }
}
