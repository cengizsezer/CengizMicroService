using Sovos.InvoiceWorker.Core.Enums;

namespace Sovos.InvoiceWorker.Core.Entities;

public class Company
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CompanyCode { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;

    /// <summary>
    /// Bildirim mail adresleri — virgülle ayrılmış liste (örn: "a@x.com, b@x.com").
    /// MailSender her seferinde split ederek tüm alıcılara gönderir.
    /// </summary>
    public string NotificationEmails { get; set; } = string.Empty;

    public bool IsActive { get; set; }
    public DateTime? LastSuccessfulRunAt { get; set; }
    public DateTime? LastFailedRunAt { get; set; }
    public string? LastErrorMessage { get; set; }

    /// <summary>
    /// Tarama modu
    /// </summary>
    public ScheduleMode ScheduleMode { get; set; } = ScheduleMode.Daily;

    /// <summary>
    /// Daily mode için saat (0-23)
    /// </summary>
    public int? ScheduleHour { get; set; }

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
