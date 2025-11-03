using EventBus.Base.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationService.Console.IntegrationEvents.Event
{
    public class TaskUpdatedIntegrationEvent : IntegrationEvent
    {
        public Guid TaskId { get; }
        public string UserId { get; }

        public string AssigneeEmail { get; }
        public string Title { get; }
        public string? Description { get; }
        public DateTime DueAtUtc { get; }            // UTC
        public string Timezone { get; }
        public bool AlarmEnabled { get; }
        public bool RemindOneDayBefore { get; }
        public bool RemindOnDay { get; }
        public DateTime UpdatedAtUtc { get; }

        public TaskUpdatedIntegrationEvent(
            Guid taskId,
            string userId,
            string title,
            string? description,
            DateTime dueAtUtc,
            string timezone,
            bool alarmEnabled,
            bool remindOneDayBefore,
            bool remindOnDay,
            DateTime updatedAtUtc,
            string assigneeEmail)
        {
            TaskId = taskId;
            UserId = userId;
            Title = title;
            Description = description;
            DueAtUtc = dueAtUtc;
            Timezone = timezone;
            AlarmEnabled = alarmEnabled;
            RemindOneDayBefore = remindOneDayBefore;
            RemindOnDay = remindOnDay;
            UpdatedAtUtc = updatedAtUtc;
            AssigneeEmail = assigneeEmail;
        }
    }
}
