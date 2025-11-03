using EventBus.Base.Abstraction;
using IdentityService.IntegrationEvents.Event;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationService.Console.IntegrationEvents.EventHandlers
{
    public class UserRegisterFailedIntegrationEventHandler : IIntegrationEventHandler<UserRegisterFailedIntegrationEvent>
    {
        private readonly ILogger<UserRegisterFailedIntegrationEventHandler> _logger;

        public UserRegisterFailedIntegrationEventHandler(ILogger<UserRegisterFailedIntegrationEventHandler> logger)
        {
            _logger = logger;
        }
        public Task Handle(UserRegisterFailedIntegrationEvent @event)
        {
            // Send Fail Notification (Sms, EMail, Push)

            _logger.LogInformation($"User Register Failed with Mail: {@event.UserMail}, ErrorMessage :{@event.ErrorMessage}");

            return Task.CompletedTask;
        }
    }
}
