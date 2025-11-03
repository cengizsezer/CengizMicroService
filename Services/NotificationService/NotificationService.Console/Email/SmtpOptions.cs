using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationService.Console.Email
{
    public class SmtpOptions
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 25;
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string FromEmail { get; set; } = "";
        public string FromName { get; set; } = "";
        public bool UseSsl { get; set; } = false;
        public bool UseStartTls { get; set; } = true;
    }
}
