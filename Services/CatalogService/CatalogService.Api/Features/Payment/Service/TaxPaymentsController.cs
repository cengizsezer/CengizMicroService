using CatalogService.Api.Features.Payment.DTO;
using CatalogService.Api.Features.TaxPayments.Service;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.TaxPayments.Controller
{
    [ApiController]
    [Route("api/catalog/[controller]")]
    public class TaxPaymentsController : ControllerBase
    {
        private readonly TaxPaymentService _svc;
        public TaxPaymentsController(TaxPaymentService svc) => _svc = svc;

        [HttpGet]
        public async Task<ActionResult<List<TaxPaymentEntityDto>>> GetAll()
            => await _svc.GetAllTaxPayments();

        [HttpGet("{id:int}")]
        public ActionResult<TaxPaymentEntityDto?> Get(int id)
        {
            var vm = _svc.FindTaxPayment(id);
            return vm is null ? NotFound() : Ok(vm);
        }

        [HttpPost]
        public ActionResult Create([FromBody] TaxPaymentEntityDto model)
            => _svc.CreateNewTaxPayment(model) ? Created("", null) : BadRequest();

        [HttpPut("{id:int}")]
        public ActionResult Update(int id, [FromBody] TaxPaymentEntityDto model)
        {
            if (id != model.Id) return BadRequest();
            return _svc.UpdateTaxPayment(model) ? NoContent() : NotFound();
        }

        [HttpDelete("{id:int}")]
        public ActionResult Delete(int id)
            => _svc.DeleteTaxPayment(id) ? NoContent() : NotFound();

        [HttpDelete("alldelete")]
        public async Task<ActionResult> DeleteAll()
        {
            var ok = await _svc.DeleteAllAsync(resetIdentity: true);
            return ok ? NoContent() : StatusCode(500, "Toplu silme başarısız.");
        }

        [HttpPost("import")]
        public async Task<ActionResult> Import([FromBody] List<TaxPaymentEntityDto> items)
            => await _svc.ImportTaxPayments(items) ? Ok() : BadRequest();

        [HttpGet("export")]
        public async Task<IActionResult> Export()
        {
            var bytes = await _svc.ExportToExcel();
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "taxpayments.xlsx");
        }

        [HttpPost("parse-excel")]
        [Consumes("multipart/form-data")]
        public ActionResult<List<TaxPaymentEntityDto>> ParseExcel([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Dosya yok");

            var result = _svc.ParseExcel(file);
            return Ok(result);
        }
    }
}