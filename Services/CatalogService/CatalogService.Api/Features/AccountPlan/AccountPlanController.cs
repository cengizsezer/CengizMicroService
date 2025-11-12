using CatalogService.Api.Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;

namespace CatalogService.Api.Features.AccountPlan
{
    [ApiController]
    [Route("api/catalog/accountplan")]
    public class AccountPlanController : ControllerBase
    {
        private readonly IAccountPlanService _svc;
        public AccountPlanController(IAccountPlanService svc) => _svc = svc;

        [HttpGet("tree")]
        public async Task<ActionResult<List<AccountNodeDto>>> GetTree(CancellationToken ct)
            => Ok(await _svc.GetTreeAsync(ct));

        [HttpGet("{id:int}")]
        public async Task<ActionResult<AccountNodeDto>> Get(int id, CancellationToken ct)
        {
            var dto = await _svc.GetAsync(id, ct);
            return dto is null ? NotFound() : Ok(dto);
        }

        public record UpdateNotesDto(string? Notes);

        [HttpPut("{id:int}/notes")]
        public async Task<IActionResult> UpdateNotes(int id, [FromBody] UpdateNotesDto dto, CancellationToken ct)
        {
            await _svc.UpdateNotesAsync(id, dto.Notes, ct);
            return NoContent();
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<AccountNodeDto>>> Search([FromQuery] string q, CancellationToken ct)
            => Ok(await _svc.SearchAsync(q, ct));
    }
}
