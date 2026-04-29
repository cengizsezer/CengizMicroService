using System.ComponentModel.DataAnnotations;

namespace WebApp.Shared.Dto.Sovos;

public class NewSovosCompanyDto
{
    [Required(ErrorMessage = "Firma adı zorunludur.")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Şirket kodu zorunludur.")]
    public string CompanyCode { get; set; } = "";

    [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Şifre zorunludur.")]
    [MinLength(4, ErrorMessage = "Şifre en az 4 karakter olmalıdır.")]
    public string Password { get; set; } = "";

    public string NotificationEmails { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public ScheduleMode ScheduleMode { get; set; } = ScheduleMode.Daily;
    public int? ScheduleHour { get; set; }
}
