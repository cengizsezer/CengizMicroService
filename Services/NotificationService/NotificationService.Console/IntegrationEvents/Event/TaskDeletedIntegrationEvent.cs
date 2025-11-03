using EventBus.Base.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationService.Console.IntegrationEvents.Event
{
    public class TaskDeletedIntegrationEvent : IntegrationEvent
    {
        public Guid TaskId { get; }
        public string UserId { get; }           // senin sistemde string

        public DateTime DeletedAtUtc { get; }

        public TaskDeletedIntegrationEvent(Guid taskId, string userId, DateTime deletedAtUtc)
        {
            TaskId = taskId;
            UserId = userId;
            DeletedAtUtc = deletedAtUtc;
        }
    }
}
