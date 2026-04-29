using System.ComponentModel.DataAnnotations;

namespace WebApp.Shared.Dto.Sovos;

public class SovosCompanyEditDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Firma adı zorunludur.")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Şirket kodu zorunludur.")]
    public string CompanyCode { get; set; } = "";

    [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
    public string Username { get; set; } = "";

    public string NotificationEmails { get; set; } = "";

    public bool IsActive { get; set; }

    public ScheduleMode ScheduleMode { get; set; } = ScheduleMode.Daily;
    public int? ScheduleHour { get; set; }
}
