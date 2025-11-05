using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityService.Application.Models
{
    public class LoginResponseModel
    {
        public string Username { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;        // tenant seçilince dolacak
        public string RefreshToken { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;         // legacy
        public List<string> Roles { get; set; } = new();
        public List<string> Permissions { get; set; } = new();

        public List<FirmaDto> Firmalar { get; set; } = new();
        
    }

}