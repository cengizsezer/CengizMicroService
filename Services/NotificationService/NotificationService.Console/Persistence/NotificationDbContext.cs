using Microsoft.EntityFrameworkCore;
using NotificationService.Console.Entitiy;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationService.Console.Persistence
{
    public class NotificationDbContext : DbContext
    {
        public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

        public DbSet<ReminderEntity> Reminders => Set<ReminderEntity>();
        public DbSet<TaskEntity> Tasks => Set<TaskEntity>();

        public DbSet<SentReminder> SentReminders => Set<SentReminder>();
        public DbSet<NotifUser> Users => Set<NotifUser>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            NotificationDbModelBuilder.Configure(modelBuilder);
            base.OnModelCreating(modelBuilder);
        }
    }
}
