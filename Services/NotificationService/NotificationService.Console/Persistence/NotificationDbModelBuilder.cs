using Microsoft.EntityFrameworkCore;
using NotificationService.Console.Entitiy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationService.Console.Persistence
{
    public static class NotificationDbModelBuilder
    {
        public static void Configure(ModelBuilder b)
        {
            b.HasDefaultSchema("notif");

            b.Entity<NotifUser>(e =>
            {
                e.ToTable("Users");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();          // Guid
                e.Property(x => x.Email).HasMaxLength(256).IsRequired();
                e.Property(x => x.PhoneE164).HasMaxLength(32);
                e.Property(x => x.UpdatedAtUtc).IsRequired();
                e.HasIndex(x => x.Email).IsUnique();
            });

            b.Entity<TaskEntity>(e =>
            {
                e.ToTable("Tasks");
                e.HasKey(x => x.Id);
                e.Property(x => x.UserEmail).HasMaxLength(256);
            });

            b.Entity<ReminderEntity>(e =>
            {
                e.ToTable("Reminders");
                e.HasKey(x => x.Id);
                e.Property(x => x.ScheduledAtUtc).HasColumnType("datetime2");
                e.HasIndex(x => new { x.Status, x.ScheduledAtUtc });

                // Navigation YOKSA bile ilişkiyi tek yerde kur:
                e.HasOne<TaskEntity>()
                 .WithMany()
                 .HasForeignKey(x => x.TaskId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<SentReminder>(e =>
            {
                e.ToTable("SentReminders");
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.TaskId, x.Kind, x.SendDate }).IsUnique();

                e.HasOne<TaskEntity>()
                 .WithMany()
                 .HasForeignKey(x => x.TaskId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }


}
