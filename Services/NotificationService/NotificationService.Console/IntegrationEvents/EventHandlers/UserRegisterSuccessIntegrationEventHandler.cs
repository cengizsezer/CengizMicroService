using EventBus.Base.Abstraction;
using IdentityService.IntegrationEvents.Event;
using Microsoft.Extensions.Logging;
using NotificationService.Console.Email;

namespace NotificationService.Console.IntegrationEvents.EventHandlers
{
    public class UserRegisterSuccessIntegrationEventHandler : IIntegrationEventHandler<UserRegisterSuccessIntegrationEvent>
    {
        private readonly ILogger<UserRegisterSuccessIntegrationEventHandler> _logger;
        private readonly IEmailSender _email;
        public UserRegisterSuccessIntegrationEventHandler(ILogger<UserRegisterSuccessIntegrationEventHandler> logger, IEmailSender email)
        {
            _logger = logger;
            _email = email;
        }
        public async Task Handle(UserRegisterSuccessIntegrationEvent @event)
        {
            // Send Fail Notification (Sms, EMail, Push)

            var html = $@"
            <p>Merhaba,</p>
            <p>Kaydınız başarıyla tamamlandı. Hoş geldiniz! 🎉</p>
            <p>Hesabınız: <strong>{@event.UserMail}</strong></p>
            <p>İyi kullanımlar,<br/>Dijital Masraf</p>";

            await _email.SendAsync(@event.UserMail, "Kayıt Başarılı – Hoş Geldiniz", html);

            _logger.LogInformation($"User Register Success with Mail: {@event.UserMail}");

           
        }
    }
}

