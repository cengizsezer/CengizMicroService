using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationService.Console.Entitiy
{
    public class ReminderEntity
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public string UserId { get; set; } = "";
        public byte Kind { get; set; }              // 0 before, 1 onday
        public DateTime ScheduledAtUtc { get; set; }
        public byte Status { get; set; }            // 0 pending, 1 sent, 2 cancelled
        public DateTime? SentAtUtc { get; set; }

      
    }
}
