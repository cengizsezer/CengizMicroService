using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationService.Console.Entitiy
{
    public class NotifUser
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = "";
        public string? PhoneE164 { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
