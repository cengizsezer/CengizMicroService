using CatalogService.Api.Features.Jobs.Contracts;
using CatalogService.Api.Features.Jobs.Domain;
using CatalogService.Api.Features.Jobs.DTO;
using CatalogService.Api.Features.Jobs.Enum;
using CatalogService.Api.Features.Jobs.Helpers;
using CatalogService.Api.Infrastructure.Context;
using DocumentFormat.OpenXml.Drawing;
using EventBus.Base.Abstraction;
using Microsoft.EntityFrameworkCore;
using NotificationService.Console.IntegrationEvents.Event;

namespace CatalogService.Api.Features.Jobs.Service
{
    public class JobService : IJobService
    {
        private readonly CatalogContext _db;
        private readonly IEventBus _eventBus;
        private readonly ILogger<JobService> _logger;
        public JobService(CatalogContext db, IEventBus eventBus, ILogger<JobService> logger)
        {
            _db = db;
            _eventBus = eventBus;
            _logger = logger;
        }
        public async Task<JobDto> CreateAsync(CreateJobRequest req, string creatorId, string? creatorName, string tenantNo, CancellationToken ct)
        {
            var job = new Job
            {
                Title = req.Title,
                Description = req.Description,
                Start = req.Start,
                End = req.End,
                CreatedById = creatorId,
                CreatedByName = creatorName,
                TenantNo = tenantNo,
                Status = JobStatus.NotStarted
            };

            foreach (var uid in req.AssigneeIds.Distinct())
                job.Assignments.Add(new JobAssignment { UserId = uid, UserName = uid, Status = JobStatus.NotStarted });

            _db.Add(job);
            await _db.SaveChangesAsync(ct); // JobId lazım

            // --- EVENT PUBLISH (safe) ---
            var tz = JobAlarmHelpers.SafeTz(req.Timezone);
            var dueUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(req.Start, DateTimeKind.Unspecified), tz);
            var createdAtUtc = DateTime.UtcNow;

            // tek mail adresi zorunlu
            if (string.IsNullOrWhiteSpace(req.RecipientEmail) ||
                !req.RecipientEmail.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("RecipientEmail invalid (must be @gmail.com). jobId={JobId}", job.Id);
                return Map(job);
            }

            // event için userId olarak (örnek) ilk assignee’yi kullan
            var userId = (job.Assignments.FirstOrDefault()?.UserId) ?? Guid.Empty.ToString();
            var taskId = JobAlarmHelpers.DeterministicGuid(job.Id, userId);

            _eventBus.Publish(new TaskCreatedIntegrationEvent(
                taskId,
                userId,                      // string
                job.Title,
                job.Description,
                dueUtc,
                req.Timezone ?? "Europe/Istanbul",
                req.AlarmEnabled,
                req.RemindOneDayBefore,
                req.RemindOnDay,
                createdAtUtc,
                req.RecipientEmail          // << gönderilecek adres
            ));


            return Map(job);
        }
        //public async Task DeleteAsync(long id, string tenantNo, string currentUserId, CancellationToken ct)
        //{
        //    var job = await _db.Set<Job>()
        //                       .Include(j => j.Assignments)
        //                       .FirstOrDefaultAsync(j => j.Id == id, ct)
        //               ?? throw new KeyNotFoundException("Job not found.");
        //    if (job.TenantNo != tenantNo) throw new KeyNotFoundException("Job not in tenant.");
        //    if (job.IsDeleted) return;

        //    job.Status = JobStatus.Deleted;
        //    job.IsDeleted = true;
        //    job.DeletedAtUtc = DateTime.UtcNow;
        //    await _db.SaveChangesAsync(ct);

        //    // Notification’ı da temizlet (TaskDeletedIntegrationEvent)
        //    var userId = job.Assignments.FirstOrDefault()?.UserId ?? Guid.Empty.ToString();
        //    var taskId = JobAlarmHelpers.DeterministicGuid(job.Id, userId);

        //    _eventBus.Publish(new TaskDeletedIntegrationEvent(
        //        taskId,
        //        userId,
        //        job.DeletedAtUtc
        //    ));
        //}
        public async Task DeleteAsync(long id, string tenantNo, string currentUserId, CancellationToken ct)
        {
            var job = await _db.Set<Job>()
                               .Include(j => j.Assignments)
                               .FirstOrDefaultAsync(j => j.Id == id, ct)
                       ?? throw new KeyNotFoundException("Job not found.");

            if (job.TenantNo != tenantNo)
                throw new KeyNotFoundException("Job not in tenant.");

            if (job.IsDeleted)
                return;

            job.Status = JobStatus.Deleted;
            job.IsDeleted = true;
            job.DeletedAtUtc = DateTime.UtcNow;
            job.UpdatedAt = job.DeletedAtUtc;
            job.UpdatedById = currentUserId;

            await _db.SaveChangesAsync(ct);

            // TASK ID: Create/Update ile aynı anahtar kullanılmalı!
            var userIdForTaskKey = job.Assignments.FirstOrDefault()?.UserId ?? Guid.Empty.ToString();
            var taskId = JobAlarmHelpers.DeterministicGuid(job.Id, userIdForTaskKey);

            try
            {
                _eventBus.Publish(new TaskDeletedIntegrationEvent(
                    taskId,
                    userIdForTaskKey,
                    job.DeletedAtUtc
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TaskDeletedIntegrationEvent publish failed. jobId={JobId}, taskId={TaskId}", job.Id, taskId);
                // İsteğe bağlı: telafi/gözetim için outbox, retry vs.
            }

            _logger.LogInformation("Job soft-deleted. jobId={JobId}, tenant={Tenant}, taskId={TaskId}", job.Id, tenantNo, taskId);
        }

        public async Task<JobDto> UpdateAsync(long id, CreateJobRequest req, string tenantNo, CancellationToken ct)
        {
            var job = await _db.Set<Job>()
                .Include(j => j.Assignments)
                .FirstOrDefaultAsync(j => j.Id == id, ct)
                ?? throw new KeyNotFoundException("Job not found.");

            if (job.TenantNo != tenantNo)
                throw new KeyNotFoundException("Job not in tenant.");

            // --- alanlar
            job.Title = req.Title;
            job.Description = req.Description;
            job.Start = req.Start;
            job.End = req.End;

            // --- assignee sync
            var newIds = (req.AssigneeIds ?? Array.Empty<string>())
              .Distinct()
              .ToList();
            var currentIds = job.Assignments.Select(a => a.UserId).ToList();

            // eklenecekler
            foreach (var uid in newIds.Except(currentIds))
                job.Assignments.Add(new JobAssignment { UserId = uid, UserName = uid, Status = JobStatus.NotStarted });

            // silinecekler
            var toRemove = job.Assignments.Where(a => !newIds.Contains(a.UserId)).ToList();
            if (toRemove.Count > 0) _db.RemoveRange(toRemove);

            await _db.SaveChangesAsync(ct);

            // --- EVENT PUBLISH (tek alıcı e-posta ile)
            // e-posta doğrulaması (gmail zorunlu ise)
            if (string.IsNullOrWhiteSpace(req.RecipientEmail) ||
                !req.RecipientEmail.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Update: RecipientEmail invalid (must be @gmail.com). jobId={JobId}", job.Id);
                return Map(job);
            }

            // timezone→UTC
            var tz = JobAlarmHelpers.SafeTz(req.Timezone);
            var dueUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(req.Start, DateTimeKind.Unspecified), tz);

            // taskId deterministik: (assignee yoksa fallback)
            var userId = job.Assignments.FirstOrDefault()?.UserId ?? Guid.Empty.ToString();
            var taskId = JobAlarmHelpers.DeterministicGuid(job.Id, userId);

            // alarm kapalıysa veya hiçbir seçenek yoksa publish etmeyebiliriz (isteğe bağlı)
            if (req.AlarmEnabled && (req.RemindOneDayBefore || req.RemindOnDay))
            {
                try
                {
                    _eventBus.Publish(new TaskUpdatedIntegrationEvent(
                        taskId,
                        userId,
                        job.Title,
                        job.Description,
                        dueUtc,
                        req.Timezone ?? "Europe/Istanbul",
                        req.AlarmEnabled,
                        req.RemindOneDayBefore,
                        req.RemindOnDay,
                        DateTime.UtcNow,
                        req.RecipientEmail
                    ));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "TaskUpdated publish failed. jobId={JobId}", job.Id);
                }
            }

            return Map(job);
        }

        public async Task<List<JobDto>> GetRangeAsync(DateTime from, DateTime to, string tenantNo, string? assigneeId, CancellationToken ct)
        {
            _logger.LogInformation("GetRangeAsync start. Tenant={TenantNo}, From={From:o}, To={To:o}, Assignee={Assignee}",
                tenantNo, from, to, assigneeId);

            try
            {
                var q = _db.Set<Job>()
                    .Where(x => x.TenantNo == tenantNo && !x.IsDeleted && x.End >= from && x.Start <= to)
                    .Include(x => x.Assignments)
                    .AsNoTracking();

                if (!string.IsNullOrWhiteSpace(assigneeId))
                    q = q.Where(j => j.Assignments.Any(a => a.UserId == assigneeId));

                var list = await q.OrderBy(x => x.Start).ToListAsync(ct);
                _logger.LogInformation("GetRangeAsync ok. Count={Count}", list.Count);

                return list.Select(Map).ToList();
            }
            catch (Microsoft.Data.SqlClient.SqlException sqlEx)
            {
                _logger.LogError(sqlEx, "GetRangeAsync SQL error. Tenant={TenantNo}, Number={Number}, Line={Line}",
                    tenantNo, sqlEx.Number, sqlEx.LineNumber);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetRangeAsync failed. Tenant={TenantNo}", tenantNo);
                throw;
            }
        }

        //public async Task<List<JobDto>> GetRangeAsync(DateTime from, DateTime to, string tenantNo, string? assigneeId, CancellationToken ct)
        //{
        //    var q = _db.Set<Job>()
        //        .Where(x => x.TenantNo == tenantNo && !x.IsDeleted && x.End >= from && x.Start <= to)
        //        .Include(x => x.Assignments)
        //        .AsNoTracking();

        //    if (!string.IsNullOrWhiteSpace(assigneeId))
        //        q = q.Where(j => j.Assignments.Any(a => a.UserId == assigneeId));

        //    var list = await q.OrderBy(x => x.Start).ToListAsync(ct);
        //    return list.Select(Map).ToList();
        //}
        public async Task UpdateAssignmentStatusAsync(long assignmentId, JobStatus status, string tenantNo, string currentUserId, CancellationToken ct)
        {
            var a = await _db.Set<JobAssignment>()
                             .Include(x => x.Job)
                             .FirstOrDefaultAsync(x => x.Id == assignmentId, ct)
                     ?? throw new KeyNotFoundException("Assignment not found.");

            if (a.Job.TenantNo != tenantNo)
                throw new KeyNotFoundException("Assignment not in tenant.");

            if (a.UserId != currentUserId)
                throw new UnauthorizedAccessException("Only your own assignment can be updated.");

            a.Status = status;
            a.UpdatedAt = DateTime.UtcNow;
           
            await _db.SaveChangesAsync(ct);
        }
        private static JobDto Map(Job j) => new()
        {
            Id = j.Id,
            Title = j.Title,
            Description = j.Description,
            Start = j.Start,
            End = j.End,
            Status = j.Status,
            Assignments = j.Assignments.Select(a => new JobAssignmentDto
            {
                Id = a.Id,
                UserId = a.UserId,
                UserName = string.IsNullOrWhiteSpace(a.UserName) ? a.UserId : a.UserName,
                Status = a.Status
            }).ToList()
        };
    }
}
