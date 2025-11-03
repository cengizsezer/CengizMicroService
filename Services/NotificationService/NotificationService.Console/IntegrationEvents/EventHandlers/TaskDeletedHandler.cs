using System.Linq;
using EventBus.Base.Abstraction;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationService.Console.IntegrationEvents.Event;
using NotificationService.Console.Persistence;

public class TaskDeletedHandler : IIntegrationEventHandler<TaskDeletedIntegrationEvent>
{
    private readonly IDbContextFactory<NotificationDbContext> _factory;
    private readonly ILogger<TaskDeletedHandler> _log;

    public TaskDeletedHandler(
        IDbContextFactory<NotificationDbContext> factory,
        ILogger<TaskDeletedHandler> log) => (_factory, _log) = (factory, log);

    public async Task Handle(TaskDeletedIntegrationEvent e)
    {
        await using var db = await _factory.CreateDbContextAsync();

        // Reminders: toplu sil (EF Core 7+)
        var remCnt = await db.Reminders
            .Where(r => r.TaskId == e.TaskId)
            .ExecuteDeleteAsync();

        // (Opsiyonel) SentReminders da silmek istersen aç:
        // var sentCnt = await db.SentReminders
        //     .Where(s => s.TaskId == e.TaskId)
        //     .ExecuteDeleteAsync();

        // Task projection sil (varsa)
        var task = await db.Tasks.FindAsync(e.TaskId);
        if (task is not null)
            db.Tasks.Remove(task);

        await db.SaveChangesAsync();

        _log.LogInformation(
            "TaskDeleted handled. TaskId={TaskId}. RemindersDeleted={RemCnt}{TaskMsg}",
            e.TaskId, remCnt, task is null ? " (task yoktu)" : " (task silindi)");
    }
}
