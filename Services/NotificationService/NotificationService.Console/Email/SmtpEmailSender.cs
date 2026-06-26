using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;

namespace NotificationService.Console.Email
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly SmtpOptions _opt;
        public SmtpEmailSender(IOptions<SmtpOptions> options) => _opt = options.Value;

        public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(_opt.FromName, _opt.FromEmail));
            // 'to' tek adres olabileceği gibi virgülle ayrılmış birden çok alıcı da olabilir.
            msg.To.AddRange(InternetAddressList.Parse(to));
            msg.Subject = subject;
            msg.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new SmtpClient();
            var ssl = _opt.UseSsl ? SecureSocketOptions.SslOnConnect
                                  : (_opt.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
            await client.ConnectAsync(_opt.Host, _opt.Port, ssl, ct);

            if (!string.IsNullOrWhiteSpace(_opt.UserName))
                await client.AuthenticateAsync(_opt.UserName, _opt.Password, ct);

            await client.SendAsync(msg, ct);
            await client.DisconnectAsync(true, ct);
        }
    }
}
