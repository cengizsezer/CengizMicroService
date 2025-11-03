using EventBus.Base.Abstraction;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationService.Console.Entitiy;
using NotificationService.Console.Helper;
using NotificationService.Console.IntegrationEvents.Event;
using NotificationService.Console.Persistence;

public class TaskCreatedHandler : IIntegrationEventHandler<TaskCreatedIntegrationEvent>
{
    private readonly IDbContextFactory<NotificationDbContext> _factory;
    private readonly ILogger<TaskCreatedHandler> _log;

    public TaskCreatedHandler(IDbContextFactory<NotificationDbContext> factory,
                              ILogger<TaskCreatedHandler> log)
        => (_factory, _log) = (factory, log);

    public async Task Handle(TaskCreatedIntegrationEvent e)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var exists = await db.Tasks.AsNoTracking().AnyAsync(t => t.Id == e.TaskId);

        var task = new TaskEntity
        {
            Id = e.TaskId,
            UserId = e.UserId,              // e.UserId zaten string, ToString() kullanma
            UserEmail = e.AssigneeEmail,    // <- e-postayı task’ta tutuyoruz
            Title = e.Title,
            Description = e.Description,
            DueAtUtc = e.DueAtUtc,
            Timezone = e.Timezone,
            AlarmEnabled = e.AlarmEnabled,
            RemindOneDayBefore = e.RemindOneDayBefore,
            RemindOnDay = e.RemindOnDay,
            AlarmHour = 8,
            AlarmMinute = 0,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (exists)
        {
            db.Tasks.Update(task);   // var olanı güncelle
            _log.LogInformation("Reminders updated for task {TaskId} (email: {Email})", task.Id, task.UserEmail);
        }
        else
        {
            await db.Tasks.AddAsync(task); // yoksa ekle
            _log.LogInformation("Reminders added for task {TaskId} (email: {Email})", task.Id, task.UserEmail);
        }

        // Hatırlatmaları planla
        TimeHelper.PlanRemindersForTask(task, db);

        await db.SaveChangesAsync();
        _log.LogInformation("Reminders planned for task {TaskId} (email: {Email})", task.Id, task.UserEmail);
    }
}
