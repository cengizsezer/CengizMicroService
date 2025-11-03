using EventBus.Base.Events;

namespace IdentityService.IntegrationEvents.Event
{
    public class UserRegisterSuccessIntegrationEvent : IntegrationEvent
    {
        public string UserMail { get; }

        public UserRegisterSuccessIntegrationEvent(string userMail) => UserMail = userMail;
    }
}
