using EventBus.Base.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationService.Console.IntegrationEvents.Event
{
    public class TaskCreatedIntegrationEvent : IntegrationEvent
    {
        public Guid TaskId { get; }
        public string UserId { get; }
        public string AssigneeEmail { get; }// Guid → string
        public string Title { get; }
        public string? Description { get; }
        public DateTime DueAtUtc { get; }
        public string Timezone { get; }
        public bool AlarmEnabled { get; }
        public bool RemindOneDayBefore { get; }
        public bool RemindOnDay { get; }
        public DateTime CreatedAtUtc { get; }

        public TaskCreatedIntegrationEvent(
            Guid taskId,
            string userId,                     // Guid → string
            string title,
            string? description,
            DateTime dueAtUtc,
            string timezone,
            bool alarmEnabled,
            bool remindOneDayBefore,
            bool remindOnDay,
            DateTime createdAtUtc,string assigneeEmail)
        {
            TaskId = taskId;
            UserId = userId;                   // string set
            Title = title;
            Description = description;
            DueAtUtc = dueAtUtc;
            Timezone = timezone;
            AlarmEnabled = alarmEnabled;
            RemindOneDayBefore = remindOneDayBefore;
            RemindOnDay = remindOnDay;
            CreatedAtUtc = createdAtUtc;
            AssigneeEmail = assigneeEmail;
        }
    }

}
