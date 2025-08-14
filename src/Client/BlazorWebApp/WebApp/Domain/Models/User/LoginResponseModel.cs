using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static WebApp.Pages.SubPages.FirmSelectDialog;

namespace WebApp.Domain.Models.User
{
    public class LoginResponseModel
    {
        public string Username { get; set; } = "";
        public string Token { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public string Role { get; set; } = "";              // "Admin","MaliIsler","IdariIsler","Personel"
        public List<FirmaDto> Firmalar { get; set; } = new();
    }

}