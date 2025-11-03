using EventBus.Base.Abstraction;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationService.Console.Entitiy;
using NotificationService.Console.Helper;
using NotificationService.Console.IntegrationEvents.Event;
using NotificationService.Console.Persistence;

public class TaskUpdatedHandler : IIntegrationEventHandler<TaskUpdatedIntegrationEvent>
{
    private readonly IDbContextFactory<NotificationDbContext> _factory;
    private readonly ILogger<TaskUpdatedHandler> _log;

    public TaskUpdatedHandler(
        IDbContextFactory<NotificationDbContext> factory,
        ILogger<TaskUpdatedHandler> log)
        => (_factory, _log) = (factory, log);

    public async Task Handle(TaskUpdatedIntegrationEvent e)
    {
        await using var db = await _factory.CreateDbContextAsync();

        // Upsert task projection
        var task = await db.Tasks.FindAsync(e.TaskId);
        if (task is null)
        {
            task = new TaskEntity
            {
                Id = e.TaskId,
                UserId = e.UserId,                 // string ise uygun
                UserEmail = e.AssigneeEmail,       // <- e-postayı task’a yaz
                Title = e.Title,
                Description = e.Description,
                DueAtUtc = e.DueAtUtc,
                Timezone = e.Timezone ?? "Europe/Istanbul",
                AlarmEnabled = e.AlarmEnabled,
                RemindOneDayBefore = e.RemindOneDayBefore,
                RemindOnDay = e.RemindOnDay,
                AlarmHour = 8,
                AlarmMinute = 0,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Tasks.Add(task);
        }
        else
        {
            task.UserId = e.UserId;
            task.UserEmail = e.AssigneeEmail;     // <- güncelle
            task.Title = e.Title;
            task.Description = e.Description;
            task.DueAtUtc = e.DueAtUtc;
            task.Timezone = e.Timezone ?? task.Timezone ?? "Europe/Istanbul";
            task.AlarmEnabled = e.AlarmEnabled;
            task.RemindOneDayBefore = e.RemindOneDayBefore;
            task.RemindOnDay = e.RemindOnDay;
            task.AlarmHour = 8;
            task.AlarmMinute = 0;
        }

        // Eski pending planları iptal et
        var pending = await db.Reminders
            .Where(r => r.TaskId == task.Id && r.Status == 0)
            .ToListAsync();
        foreach (var r in pending) r.Status = 2;

        // Alarm kapalıysa yeniden planlama yapma
        if (task.AlarmEnabled && (task.RemindOneDayBefore || task.RemindOnDay))
        {
            TimeHelper.PlanRemindersForTask(task, db);
            _log.LogInformation("Task {TaskId} için hatırlatmalar yeniden planlandı.", task.Id);
        }
        else
        {
            _log.LogInformation("Task {TaskId} için alarm kapalı veya hiçbir hatırlatma seçili değil; planlama yapılmadı.", task.Id);
        }

        await db.SaveChangesAsync();
    }
}
