using CatalogService.Api.Contracts.Dtos;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Controllers
{
    [ApiController]
    [Route("api/catalog/vehicles")] // <<< sabit prefix
    public class VehiclesController : ControllerBase
    {
        private readonly VehicleService _svc;
        public VehiclesController(VehicleService svc) => _svc = svc;

        [HttpGet]                           // GET    api/catalog/vehicles
        public async Task<ActionResult<List<VehicleDto>>> GetAll()
            => await _svc.GetAllVehicles();

        [HttpGet("{id:int}")]               // GET    api/catalog/vehicles/5
        public ActionResult<VehicleDto?> Get(int id)
        {
            var vm = _svc.FindVehicle(id);
            return vm is null ? NotFound() : Ok(vm);
        }

        [HttpPost]                          // POST   api/catalog/vehicles
        public ActionResult Create([FromBody] VehicleDto model)
            => _svc.CreateNewVehicle(model) ? Created("", null) : BadRequest();

        [HttpPut("{id:int}")]               // PUT    api/catalog/vehicles/5
        public ActionResult Update(int id, [FromBody] VehicleDto model)
        {
            if (id != model.Id) return BadRequest();
            return _svc.UpdateVehicle(model) ? NoContent() : NotFound();
        }

        [HttpDelete("{id:int}")]            // DELETE api/catalog/vehicles/5
        public ActionResult Delete(int id)
            => _svc.DeleteVehicle(id) ? NoContent() : NotFound();

        [HttpPost("import")]                // POST   api/catalog/vehicles/import
        public async Task<ActionResult> Import([FromBody] List<VehicleDto> items)
            => await _svc.ImportVehicles(items) ? Ok() : BadRequest();

        [HttpGet("export")]                 // GET    api/catalog/vehicles/export
        public async Task<IActionResult> Export()
        {
            var bytes = await _svc.ExportToExcel();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "vehicles.xlsx");
        }

        [HttpPost("parse-excel")]
        [RequestSizeLimit(100_000_000)]
        public async Task<ActionResult<List<VehicleDto>>> ParseExcel([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("Dosya yok.");

            var list = new List<VehicleDto>();
            using var stream = file.OpenReadStream();
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheet(1); // ilk sayfa

            // Başlık satırı 1 ise, 2’den itibaren oku (örnek)
            var lastRow = ws.LastRowUsed().RowNumber();
            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                // Sütun indexlerini Excel’ine göre ayarla
                list.Add(new VehicleDto
                {
                    Plate = row.Cell(1).GetString()?.Trim(),
                    Driver = row.Cell(2).GetString(),
                    Unit = row.Cell(3).GetString(),
                    Department = row.Cell(4).GetString(),
                    Region = row.Cell(5).GetString(),
                    Type = row.Cell(6).GetString(),
                    Brand = row.Cell(7).GetString(),
                    Model = row.Cell(8).GetString(),
                    Gear = row.Cell(9).GetString(),
                    Fuel = row.Cell(10).GetString(),
                    Fleet = row.Cell(11).GetString(),
                    // Description1/2 gerekiyorsa ekle
                });
            }

            return Ok(list);
        }
    }
}
