using CatalogService.Api.Contracts.Dtos;
using CatalogService.Api.Core.Domain;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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


        [HttpDelete("alldelete")] // DELETE api/catalog/vehicles/all
        public async Task<ActionResult> DeleteAll()
        {
            var ok = await _svc.DeleteAllAsync(resetIdentity: true);
            return ok ? NoContent() : StatusCode(500, "Toplu silme başarısız.");
        }

       


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
            if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Sadece .xlsx yükleyin.");

            using var stream = file.OpenReadStream();
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheet(1);

            var headerRow = ws.Row(1);
            var lastCol = ws.LastColumnUsed().ColumnNumber();
            var lastRow = ws.LastRowUsed().RowNumber();

            // Başlık haritası: "AD" -> kolonIndex
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int c = 1; c <= lastCol; c++)
            {
                var name = (headerRow.Cell(c).GetString() ?? "").Trim();
                if (!string.IsNullOrEmpty(name)) map[Normalize(name)] = c;
            }

            // Eşleme yardımcıları
            int ColAny(params string[] names)
            {
                // 1) tam eşleşme
                foreach (var raw in names)
                {
                    var key = Normalize(raw);
                    if (map.TryGetValue(key, out var ix)) return ix;
                }
                // 2) içerir eşleşme
                var norms = names.Select(Normalize).ToList();
                foreach (var kv in map)
                    if (norms.Any(n => kv.Key.Contains(n))) return kv.Value;

                return -1;
            }

            // Alias’larla kolon indexleri
            int cPlate = ColAny("PLAKA", "PLATE");
            int cDriver = ColAny("SÜRÜCÜ", "SÜRÜCÜ ADI", "ŞOFÖR", "ŞOFÖR ADI", "DRIVER","KULLANICI ADI");
            int cUnit = ColAny("BİRİM", "BIRIM", "UNIT");
            int cDepartment = ColAny("BÖLÜM", "DEPARTMAN", "DEPARTMENT");
            int cRegion = ColAny("BÖLGE", "REGION");
            int cType = ColAny("TİP", "TIP", "TYPE");
            int cBrand = ColAny("MARKA", "BRAND");
            int cModel = ColAny("MODEL");
            int cGear = ColAny("VİTES", "VITES", "GEAR");
            int cFuel = ColAny("YAKIT", "FUEL");
            int cFleet = ColAny("FİLO", "FILO", "FLEET");
            int cDesc1 = ColAny("AÇIKLAMA-1", "AÇIKLAMA 1", "AÇIKLAMA1", "AÇIKLAMA");
            int cDesc2 = ColAny("AÇIKLAMA-2", "AÇIKLAMA 2", "AÇIKLAMA2");

            var list = new List<VehicleDto>();

            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                string Get(int col) => col > 0 ? row.Cell(col).GetString()?.Trim() ?? "" : "";

                var dto = new VehicleDto
                {
                    Plate = Get(cPlate),
                    Driver = Get(cDriver),
                    Unit = Get(cUnit),
                    Department = Get(cDepartment),
                    Region = Get(cRegion),
                    Type = Get(cType),
                    Brand = Get(cBrand),
                    Model = Get(cModel),
                    Gear = Get(cGear),
                    Fuel = Get(cFuel),
                    Fleet = Get(cFleet),
                    Description1 = Get(cDesc1),
                    Description2 = Get(cDesc2)
                };

                if (!string.IsNullOrWhiteSpace(dto.Plate))
                    list.Add(dto);
            }

            return Ok(list);

            static string Normalize(string s)
            {
                s = (s ?? "").Trim().ToUpperInvariant();
                return s.Replace('İ', 'I').Replace('Ş', 'S').Replace('Ğ', 'G')
                        .Replace('Ü', 'U').Replace('Ö', 'O').Replace('Ç', 'C');
            }
        }

        //[HttpPost("parse-excel")]
        //[RequestSizeLimit(100_000_000)]
        //public async Task<ActionResult<List<VehicleDto>>> ParseExcel([FromForm] IFormFile file)
        //{
        //    if (file == null || file.Length == 0) return BadRequest("Dosya yok.");
        //    if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        //        return BadRequest("Sadece .xlsx yükleyin.");

        //    using var stream = file.OpenReadStream();
        //    using var wb = new XLWorkbook(stream);
        //    var ws = wb.Worksheet(1);

        //    var headerRow = ws.Row(1);
        //    var lastCol = ws.LastColumnUsed().ColumnNumber();
        //    var lastRow = ws.LastRowUsed().RowNumber();

        //    // Başlık ad -> kolon index (case-insensitive, TR karakterlerini normalize ederek)
        //    var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        //    for (int c = 1; c <= lastCol; c++)
        //    {
        //        var name = (headerRow.Cell(c).GetString() ?? "").Trim();
        //        if (!string.IsNullOrEmpty(name)) map[Normalize(name)] = c;
        //    }

        //    int Col(string name) => map.TryGetValue(Normalize(name), out var ix) ? ix : -1;
        //    int cPlate = Col("PLAKA");
        //    int cDriver = Col("SÜRÜCÜ");
        //    int cUnit = Col("BİRİM");
        //    int cDepartment = Col("BÖLÜM");
        //    int cDesc1 = Col("AÇIKLAMA-1");
        //    int cRegion = Col("BÖLGE");
        //    int cDesc2 = Col("AÇIKLAMA-2");
        //    int cType = Col("TİP");
        //    int cBrand = Col("MARKA");
        //    int cModel = Col("MODEL");
        //    int cGear = Col("VİTES");
        //    int cFuel = Col("YAKIT");
        //    int cFleet = Col("FİLO");

        //    var list = new List<VehicleDto>();
        //    for (int r = 2; r <= lastRow; r++)
        //    {
        //        string Get(int col) => col > 0 ? ws.Row(r).Cell(col).GetString()?.Trim() ?? "" : "";

        //        var dto = new VehicleDto
        //        {
        //            Plate = Get(cPlate),
        //            Driver = Get(cDriver),
        //            Unit = Get(cUnit),
        //            Department = Get(cDepartment),
        //            Description1 = Get(cDesc1),
        //            Region = Get(cRegion),
        //            Description2 = Get(cDesc2),
        //            Type = Get(cType),
        //            Brand = Get(cBrand),
        //            Model = Get(cModel),
        //            Gear = Get(cGear),
        //            Fuel = Get(cFuel),
        //            Fleet = Get(cFleet)
        //        };

        //        if (!string.IsNullOrWhiteSpace(dto.Plate))
        //            list.Add(dto);
        //    }

        //    return Ok(list);

        //    static string Normalize(string s)
        //    {
        //        // basit normalize: trimming + büyük harf + Türkçe karakter eşitleme
        //        s = s.Trim().ToUpperInvariant();
        //        return s
        //            .Replace('İ', 'I').Replace('Ş', 'S').Replace('Ğ', 'G')
        //            .Replace('Ü', 'U').Replace('Ö', 'O').Replace('Ç', 'C');
        //    }
        //}
    }
}
