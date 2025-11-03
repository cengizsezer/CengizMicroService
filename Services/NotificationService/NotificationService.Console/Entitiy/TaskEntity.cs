using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationService.Console.Entitiy
{
    public class TaskEntity
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public DateTime DueAtUtc { get; set; }
        public string Timezone { get; set; } = "Europe/Istanbul";
        public bool AlarmEnabled { get; set; }
        public bool RemindOneDayBefore { get; set; }
        public bool RemindOnDay { get; set; }
        public int AlarmHour { get; set; }
        public int AlarmMinute { get; set; }

        // YENİ
        public DateTime CreatedAtUtc { get; set; }
        public string? UserEmail { get; set; }      // <- YENİ
    }
}
