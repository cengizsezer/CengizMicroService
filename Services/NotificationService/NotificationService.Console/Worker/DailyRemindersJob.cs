using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NotificationService.Console.Email;
using NotificationService.Console.Entitiy;
using NotificationService.Console.Persistence;
using Quartz;

namespace NotificationService.Console.Worker
{
    public class DailyRemindersJob : IJob
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<DailyRemindersJob> _log;

        public DailyRemindersJob(IServiceProvider sp, ILogger<DailyRemindersJob> log)
            => (_sp, _log) = (sp, log);

        public async Task Execute(IJobExecutionContext context)
        {
            var ct = context.CancellationToken;
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

            var nowUtc = DateTime.UtcNow;
            var windowStart = nowUtc.AddMinutes(-5);
            var windowEnd = nowUtc.AddMinutes(+5);

            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            var due = await db.Reminders
                .Where(r => r.Status == 0 &&
                            r.ScheduledAtUtc >= windowStart &&
                            r.ScheduledAtUtc <= windowEnd)
                .OrderBy(r => r.ScheduledAtUtc)
                .Take(500)
                .ToListAsync(ct);

            if (due.Count == 0)
            {
                _log.LogInformation("DailyRemindersJob: due yok.");
                return;
            }

            var taskIds = due.Select(r => r.TaskId).Distinct().ToList();
            var tasks = await db.Tasks
                .AsNoTracking()
                .Where(t => taskIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t, ct);

            var sentCount = 0;

            foreach (var r in due)
            {
                if (!tasks.TryGetValue(r.TaskId, out var task) ||
                    string.IsNullOrWhiteSpace(task.UserEmail))
                {
                    _log.LogWarning("Task veya e-posta yok. Reminder {ReminderId} skip. TaskId={TaskId}", r.Id, r.TaskId);
                    continue;
                }

                var to = task.UserEmail.Trim();
                var dueLocal = TimeZoneInfo.ConvertTimeFromUtc(task.DueAtUtc, tz);
                var subject = r.Kind == 0 ? $"Yarın: {task.Title}" : $"Bugün: {task.Title}";
                var body = $@"<p>Merhaba,</p>
                              <p><strong>{System.Net.WebUtility.HtmlEncode(task.Title)}</strong> için hatırlatma.</p>
                              <p>Tarih/Saat: {dueLocal:dd MMM yyyy HH:mm}</p>
                              <p>{System.Net.WebUtility.HtmlEncode(task.Description ?? "")}</p>";

                try
                {
                    await email.SendAsync(to, subject, body, ct);

                    // 1) Reminder'ı "Sent" yap
                    r.Status = 1;
                    r.SentAtUtc = DateTime.UtcNow;

                    // 2) SentReminders kaydı (aynı gün/aynı türü ikinci kez yazmamak için kontrol)
                    var localSendDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz));
                    bool already = await db.SentReminders
                        .AnyAsync(s => s.TaskId == r.TaskId && s.Kind == r.Kind && s.SendDate == localSendDate, ct);

                    if (!already)
                    {
                        db.SentReminders.Add(new SentReminder
                        {
                            Id = Guid.NewGuid(),
                            TaskId = r.TaskId,
                            UserId = task.UserId ?? "",   // TaskEntity.UserId string
                            Kind = r.Kind,
                            SendDate = localSendDate,       // Istanbul local day
                            SentAtUtc = DateTime.UtcNow
                        });
                    }

                    sentCount++;
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Mail gönderilemedi. TaskId={TaskId}, To={To}", r.TaskId, to);
                    // Hata olursa pending bırakıyoruz ki yeniden denensin.
                }
            }

            await db.SaveChangesAsync(ct);
            _log.LogInformation("DailyRemindersJob sent {Count} reminders.", sentCount);
        }
    }
}
