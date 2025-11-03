using EventBus.Base.Abstraction;
using IdentityService.IntegrationEvents.Event;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationService.Console.IntegrationEvents.Event;
using NotificationService.Console.IntegrationEvents.EventHandlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationService.Console.Worker
{
    public class EventBusSubscriptionHostedService : IHostedService
    {
        private readonly IEventBus _eventBus;
        private readonly ILogger<EventBusSubscriptionHostedService> _logger;

        public EventBusSubscriptionHostedService(IEventBus eventBus, ILogger<EventBusSubscriptionHostedService> logger)
        {
            _eventBus = eventBus; _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _eventBus.Subscribe<UserRegisterSuccessIntegrationEvent, UserRegisterSuccessIntegrationEventHandler>();
            _eventBus.Subscribe<UserRegisterFailedIntegrationEvent, UserRegisterFailedIntegrationEventHandler>();
            _eventBus.Subscribe<TaskUpdatedIntegrationEvent, TaskUpdatedHandler>();
            _eventBus.Subscribe<TaskCreatedIntegrationEvent, TaskCreatedHandler>();
            _eventBus.Subscribe<TaskDeletedIntegrationEvent, TaskDeletedHandler>();
            _logger.LogInformation("EventBus subscriptions registered.");
            
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

}
