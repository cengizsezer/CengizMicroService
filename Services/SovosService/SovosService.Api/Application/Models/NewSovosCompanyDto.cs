using System.ComponentModel.DataAnnotations;
using Sovos.InvoiceWorker.Core.Enums;

namespace SovosService.Api.Application.Models;

public class NewSovosCompanyDto
{
    [Required]
    public string Name { get; set; } = "";

    [Required]
    public string CompanyCode { get; set; } = "";

    [Required]
    public string Username { get; set; } = "";

    [Required, MinLength(4)]
    public string Password { get; set; } = "";

    public string NotificationEmails { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public ScheduleMode ScheduleMode { get; set; } = ScheduleMode.Daily;
    public int? ScheduleHour { get; set; }
}
