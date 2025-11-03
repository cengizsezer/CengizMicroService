using AutoMapper;
using CatalogService.Api.Features.Education.Domain;
using CatalogService.Api.Features.Education.DTO;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.Api.Infrastructure.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace CatalogService.Api.Features.Education.Controller
{
    [Route("api/catalog/education")]
    [ApiController]
    public class EducationController : ControllerBase
    {
        private readonly CatalogContext _db;
        private readonly IMapper _mapper;

        public EducationController(IMapper mapper, CatalogContext db)
        {
            _mapper = mapper;
            _db = db;
            
        }

        // GET api/catalog/education?p=0&ps=20&q=...&orderBy=createdAtDesc|createdAtAsc|titleAsc|titleDesc
        [HttpGet]
        [ProducesResponseType(typeof(PaginatedItemsViewModel<EducationItemListItemDto>), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<PaginatedItemsViewModel<EducationItemListItemDto>>> GetPagedAsync(
            [FromQuery(Name = "p")] int pageIndex = 0,
            [FromQuery(Name = "ps")] int pageSize = 20,
            [FromQuery] string? q = null,
            [FromQuery] string orderBy = "createdAtDesc")
        {
            var query = _db.EducationItems.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(x =>
                    x.Title.Contains(term) ||
                    x.BodyText != null && x.BodyText.Contains(term));
            }

            var key = (orderBy ?? "createdatdesc").ToLowerInvariant();

            query = key switch
            {
                "createdatasc" => query.OrderBy(x => x.CreatedAt),
                "titleasc" => query.OrderBy(x => x.Title),
                "titledesc" => query.OrderByDescending(x => x.Title),
                _ => query.OrderByDescending(x => x.CreatedAt) // createdatdesc (default)
            };

            var totalItems = await query.CountAsync();

            var items = await query
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .Select(e => new EducationItemListItemDto
                {
                    Id = e.Id,
                    Title = e.Title,
                    BodyText = e.BodyText,
                    IsPublished = e.IsPublished,
                    CreatedAt = e.CreatedAt
                })
                .ToListAsync();

            var vm = new PaginatedItemsViewModel<EducationItemListItemDto>(pageIndex, pageSize, totalItems, items);
            return Ok(vm);
        }

        // GET api/catalog/education/5
        [HttpGet("{id:int}", Name = "GetEducationById")]
        [ProducesResponseType(typeof(EducationItemDto), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<ActionResult<EducationItemDto>> GetByIdAsync(int id)
        {
            var e = await _db.EducationItems.FirstOrDefaultAsync(x => x.Id == id);
            if (e is null) return NotFound();

            return Ok(_mapper.Map<EducationItemDto>(e));
        }

        // POST api/catalog/education
        [HttpPost]
        [ProducesResponseType(typeof(EducationItemDto), (int)HttpStatusCode.Created)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateEducationItemDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest("Title is required.");

            var entity = _mapper.Map<EducationItem>(dto);
            entity.CreatedAt = DateTime.UtcNow;

            _db.EducationItems.Add(entity);
            await _db.SaveChangesAsync();

            var result = _mapper.Map<EducationItemDto>(entity);

            // Named route sayesinde hatasız Location üretir
            return CreatedAtRoute("GetEducationById", new { id = entity.Id }, result);
        }

        // PUT api/catalog/education/5
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(EducationItemDto), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateEducationItemDto dto)
        {
            // 1) Bu sorgu tracked döner
            var e = await _db.EducationItems
                .AsTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (e is null) return NotFound();

            // 2) Global davranışı kurcalama (bu satırı tamamen kaldır)
            // _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;

            if (!string.IsNullOrWhiteSpace(dto.Title)) e.Title = dto.Title.Trim();
            if (dto.BodyText is not null) e.BodyText = dto.BodyText;
            if (dto.IsPublished is not null) e.IsPublished = dto.IsPublished.Value;

            e.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            var result = _mapper.Map<EducationItemDto>(e);
            return Ok(result);
        }
        //public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateEducationItemDto dto)
        //{
        //    var e = await _db.EducationItems.FirstOrDefaultAsync(x => x.Id == id);
        //    if (e is null) return NotFound();

        //    _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;

        //    if (!string.IsNullOrWhiteSpace(dto.Title)) e.Title = dto.Title.Trim();
        //    if (dto.BodyText is not null) e.BodyText = dto.BodyText;
        //    if (dto.IsPublished is not null) e.IsPublished = dto.IsPublished.Value;
        //    e.UpdatedAt = DateTime.UtcNow;

        //    await _db.SaveChangesAsync();

        //    var result = _mapper.Map<EducationItemDto>(e);
        //    return Ok(result);
        //}

        // DELETE api/catalog/education/5
        [HttpDelete("{id:int}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> DeleteAsync(int id)
        {
            var exists = await _db.EducationItems.AsNoTracking().AnyAsync(x => x.Id == id);
            if (!exists) return NotFound();

            _db.EducationItems.Remove(new EducationItem { Id = id });
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
