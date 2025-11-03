using EventBus.Base.Events;

namespace IdentityService.IntegrationEvents.Event
{
    public class UserRegisterFailedIntegrationEvent: IntegrationEvent
    {
        public string UserMail { get; }
        public string ErrorMessage { get; }


        public UserRegisterFailedIntegrationEvent(string errorMessage, string userMail)
        {
            ErrorMessage = errorMessage;
            UserMail = userMail;
        }
    }
}
