using Sovos.InvoiceWorker.Core.Enums;

namespace SovosService.Api.Application.Models;

public class SovosCompanyListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string CompanyCode { get; set; } = "";
    public string Username { get; set; } = "";
    public string NotificationEmails { get; set; } = "";
    public bool IsActive { get; set; }
    public bool HasPassword { get; set; }
    public DateTime? LastSuccessfulRunAt { get; set; }
    public DateTime? LastFailedRunAt { get; set; }
    public string? LastErrorMessage { get; set; }
    public int? InvoiceCountLastRun { get; set; }
    public ScheduleMode ScheduleMode { get; set; }
    public int? ScheduleHour { get; set; }
}
