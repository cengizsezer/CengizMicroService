using CatalogService.Api.Features.PersonnelEmails.Contracts;
using CatalogService.Api.Features.PersonnelEmails.DTO;
using CatalogService.Api.Features.PersonnelEmails.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.PersonnelEmails.Controller
{
    [ApiController]
    [Route("api/catalog/personnel-emails")]
    [Authorize]
    public class PersonnelEmailsController : ControllerBase
    {
        private readonly IPersonnelEmailService _svc;
        public PersonnelEmailsController(IPersonnelEmailService svc) => _svc = svc;

        [HttpGet]
        public async Task<ActionResult<List<PersonnelEmailDto>>> GetAll(CancellationToken ct)
            => Ok(await _svc.GetAllAsync(ct));

        [HttpPut]
        public async Task<ActionResult<PersonnelEmailDto>> Upsert(
            [FromBody] UpsertPersonnelEmailRequest req, CancellationToken ct)
            => Ok(await _svc.UpsertAsync(req, ct));
    }
}
