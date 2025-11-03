using Microsoft.Extensions.Logging;
using NotificationService.Console.Entitiy;
using NotificationService.Console.Persistence;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationService.Console.Helper
{
    public static class TimeHelper
    {
        // Sabit: 08:00 yerel
        private const int AlarmHour = 8;
        private const int AlarmMinute = 0;

        /// <summary>
        /// Task için hatırlatmaları planlar. Kaç adet eklendiğini döner.
        /// </summary>
        public static int PlanRemindersForTask(TaskEntity task, NotificationDbContext db, ILogger? log = null)
        {
            var tz = SafeTz(task.Timezone);
            var added = 0;

            // İsteğe bağlı korumalar
            if (!task.AlarmEnabled || (!task.RemindOneDayBefore && !task.RemindOnDay))
            {
                log?.LogInformation("Plan skip: Alarm kapalı veya seçenek yok. TaskId={TaskId}", task.Id);
                return 0;
            }

            // Görev tarihi (yerel) – sadece gün kısmını kullanıyoruz
            var dueLocal = TimeZoneInfo.ConvertTimeFromUtc(task.DueAtUtc, tz).Date;

            // 1 gün önce 08:00
            if (task.RemindOneDayBefore)
            {
                var local = dueLocal.AddDays(-1).AddHours(AlarmHour).AddMinutes(AlarmMinute);
                var utc = TimeZoneInfo.ConvertTimeToUtc(local, tz);

                db.Reminders.Add(new ReminderEntity
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Id,
                    UserId = task.UserId,   // string
                    Kind = 0,               // Before
                    ScheduledAtUtc = utc,
                    Status = 0              // Pending
                });
                added++;
                log?.LogInformation("Plan BEFORE: {Local} => {Utc:o} (TaskId={TaskId})", local, utc, task.Id);
            }

            // Görev günü 08:00
            if (task.RemindOnDay)
            {
                var local = dueLocal.AddHours(AlarmHour).AddMinutes(AlarmMinute);
                var utc = TimeZoneInfo.ConvertTimeToUtc(local, tz);

                db.Reminders.Add(new ReminderEntity
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Id,
                    UserId = task.UserId,
                    Kind = 1,               // OnDay
                    ScheduledAtUtc = utc,
                    Status = 0
                });
                added++;
                log?.LogInformation("Plan ONDAY:  {Local} => {Utc:o} (TaskId={TaskId})", local, utc, task.Id);
            }

            return added;
        }

        public static TimeZoneInfo SafeTz(string? tz)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(tz) ? "Europe/Istanbul" : tz!); }
            catch { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }
        }
    }

    //public static class TimeHelper
    //{
    //    public static TimeZoneInfo SafeTz(string iana)
    //    {
    //        try { return TimeZoneInfo.FindSystemTimeZoneById(iana); }
    //        catch { return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); } // Windows fallback
    //    }

    //    public static void PlanRemindersForTask(TaskEntity task, NotificationDbContext db)
    //    {
    //        if (!task.AlarmEnabled) return;

    //        var tz = SafeTz(task.Timezone ?? "Europe/Istanbul");

    //        // Görev tarihi yerel zamana
    //        var dueLocal = TimeZoneInfo.ConvertTimeFromUtc(task.DueAtUtc, tz);
    //        var dueDate = DateOnly.FromDateTime(dueLocal);

    //        // Sabit saat: 08:00
    //        DateTime local0800(DateOnly d) => new(d.Year, d.Month, d.Day, 8, 0, 0, DateTimeKind.Unspecified);
    //        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

    //        // 1) Due-1 gün 08:00
    //        if (task.RemindOneDayBefore)
    //        {
    //            var beforeUtc = TimeZoneInfo.ConvertTimeToUtc(local0800(dueDate.AddDays(-1)), tz);
    //            if (beforeUtc > DateTime.UtcNow)
    //            {
    //                // İsteyenler için: varsa mükerrer kontrolü (unique index ile)
    //                db.Reminders.Add(new ReminderEntity
    //                {
    //                    Id = Guid.NewGuid(),
    //                    TaskId = task.Id,
    //                    UserId = task.UserId,
    //                    Kind = 0,
    //                    ScheduledAtUtc = beforeUtc,
    //                    Status = 0
    //                });
    //            }
    //        }

    //        // 2) Due günü 08:00 (mesafe kuralı)
    //        if (task.RemindOnDay)
    //        {
    //            var onDayLocal0800 = local0800(dueDate);
    //            if (onDayLocal0800 > nowLocal) // bugün 08:00 geçtiyse oluşturma
    //            {
    //                var onDayUtc = TimeZoneInfo.ConvertTimeToUtc(onDayLocal0800, tz);
    //                if (onDayUtc > DateTime.UtcNow)
    //                {
    //                    db.Reminders.Add(new ReminderEntity
    //                    {
    //                        Id = Guid.NewGuid(),
    //                        TaskId = task.Id,
    //                        UserId = task.UserId,
    //                        Kind = 1,
    //                        ScheduledAtUtc = onDayUtc,
    //                        Status = 0
    //                    });
    //                }
    //            }
    //        }
    //    }

    //}
}
