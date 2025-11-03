using CatalogService.Api.Features.Jobs.Contracts;
using CatalogService.Api.Features.Jobs.Domain;
using CatalogService.Api.Features.Jobs.DTO;
using CatalogService.Api.Features.Jobs.Enum;
using CatalogService.Api.Features.Jobs.Service;
using CatalogService.Api.Infrastructure.Accessor;
using CatalogService.Api.Infrastructure.Auth;
using EventBus.Base.Abstraction;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Console.IntegrationEvents.Event;

namespace CatalogService.Api.Features.Jobs.Controller
{
    [ApiController]
    [Route("api/catalog/jobs")]
    [Authorize]
    public class JobsController : ControllerBase
    {
        private readonly IJobService _jobService;
        private readonly IHttpCurrentTenant _tenant;
        private readonly IHttpCurrentUser _user;

        public JobsController(IJobService svc, IHttpCurrentTenant tenant, IHttpCurrentUser user)
        {
            _jobService = svc;
            _tenant = tenant;
            _user = user;
        }
       
        [HttpPost]
        public async Task<ActionResult<JobDto>> Create([FromBody] CreateJobRequest req, CancellationToken ct)
        {
           
            var dto = await _jobService.CreateAsync(req, _user.UserId, _user.UserName, _tenant.CurrentTenantNo, ct);
            return CreatedAtAction(nameof(GetRange), new { from = req.Start, to = req.End }, dto);
        }

        [HttpGet]
        public async Task<ActionResult<List<JobDto>>> GetRange(
      [FromQuery] DateTime from,
      [FromQuery] DateTime to,
      [FromQuery] string? assigneeId,
      CancellationToken ct)
        {
            // HttpCurrentTenant zaten claim "tn" + header "x-tenant-no" okuyacak şekilde düzeltildi varsayıyorum
            var tenantNo = _tenant.CurrentTenantNo;

            if (string.IsNullOrWhiteSpace(tenantNo))
                return BadRequest("Tenant eksik (header 'x-tenant-no' veya JWT claim 'tn').");

            // Eğer servis string bekliyorsa:
            var list = await _jobService.GetRangeAsync(from, to, tenantNo, assigneeId, ct);
            return Ok(list);

            // Eğer servis int bekliyorsa, üstteki yerine şunu kullan:
            /*
            if (!int.TryParse(tenantNo, out var tenantId))
                return BadRequest("TenantNo sayısal olmalı.");
            var list = await _jobService.GetRangeAsync(from, to, tenantId, assigneeId, ct);
            return Ok(list);
            */
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id, CancellationToken ct)
        {
            await _jobService.DeleteAsync(id, _tenant.CurrentTenantNo, _user.UserId, ct); // soft delete
            return NoContent();
        }


        [HttpPatch("assignments/{assignmentId:long}/status")]
        public async Task<IActionResult> UpdateAssignmentStatus(
    [FromRoute] long assignmentId,
    [FromBody] UpdateAssignmentStatusRequest req,
    CancellationToken ct)
        {
            var tenantNo = _tenant.CurrentTenantNo;
            if (string.IsNullOrWhiteSpace(tenantNo))
                return BadRequest("Tenant eksik.");

            try
            {
                await _jobService.UpdateAssignmentStatusAsync(assignmentId, req.Status, tenantNo, _user.UserId, ct);
               
                
                return NoContent();
            }
            catch (KeyNotFoundException knf)
            {
                return NotFound(knf.Message);            // 404 → ID yok veya tenant uyuşmadı
            }
            catch (UnauthorizedAccessException uae)
            {
                return Forbid();                         // 403 → Başkasının görevi
            }
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<JobDto>> Update(long id, [FromBody] CreateJobRequest req, CancellationToken ct)
        {
            var tenantNo = _tenant.CurrentTenantNo!;
            var dto = await _jobService.UpdateAsync(id, req, tenantNo, ct);
            return Ok(dto);
        }


    }
}
