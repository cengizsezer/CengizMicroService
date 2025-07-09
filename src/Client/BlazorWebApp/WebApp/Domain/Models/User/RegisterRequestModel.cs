using System.ComponentModel.DataAnnotations;

namespace WebApp.Domain.Models.User
{
    public class RegisterRequestModel
    {
        [Required]
        public string UserName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }

        public string Role { get; set; }  // Opsiyonel, şimdilik formda eklemeyebilirsin

    }
}
