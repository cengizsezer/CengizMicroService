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
        [Consumes("multipart/form-data")]
        public ActionResult<List<VehicleDto>> ParseExcel([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("Dosya yok.");
            if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Sadece .xlsx yükleyin.");

            using var stream = file.OpenReadStream();
            using var wb = new XLWorkbook(stream);

            // 0) Birden fazla veri içeren sayfa var mı?
            var usedSheets = wb.Worksheets.Where(sh => sh.RangeUsed() != null).ToList();
            if (usedSheets.Count == 0)
                return BadRequest("Excel'de veri içeren sayfa bulunamadı.");

            if (usedSheets.Count > 1)
            {
                var names = string.Join(", ", usedSheets.Select(s => s.Name));
                return BadRequest($"Bu dosyada {usedSheets.Count} adet veri içeren sayfa var: {names}. Lütfen tek sayfada başlıkları olan bir Excel yükleyin.");
            }

            // 1) Kullanılan tek sayfayı al
            var ws = usedSheets[0];
            var used = ws.RangeUsed();
            if (used == null) return Ok(new List<VehicleDto>());

            int firstRow = used.RangeAddress.FirstAddress.RowNumber;
            int firstCol = used.RangeAddress.FirstAddress.ColumnNumber;
            int lastRow = used.RangeAddress.LastAddress.RowNumber;
            int lastCol = used.RangeAddress.LastAddress.ColumnNumber;

            // 2) Muhtemel header satırını SKORLAYARAK bul
            int ScoreRow(int r)
            {
                var toks = ws.Row(r).Cells(firstCol, lastCol)
                             .Select(c => Normalize(c.GetString()))
                             .Where(t => !string.IsNullOrEmpty(t)).ToList();
                int s = 0;
                if (toks.Any(t => t.Contains("PLAKA") || t.Contains("PLATE"))) s += 5;
                if (toks.Any(t => t.Contains("SURUCU") || t.Contains("SOFOR") || t.Contains("DRIVER"))) s += 2;
                if (toks.Any(t => t.Contains("BIRIM") || t.Contains("UNIT"))) s++;
                if (toks.Any(t => t.Contains("DEPARTMAN") || t.Contains("DEPARTMENT") || t.Contains("BOLUM"))) s++;
                if (toks.Any(t => t.Contains("MARKA") || t.Contains("BRAND"))) s++;
                if (toks.Any(t => t.Contains("MODEL"))) s++;
                return s;
            }

            int scanTo = Math.Min(firstRow + 20, lastRow);
            int hdrRowNum = firstRow;
            int bestScore = -1;
            for (int r = firstRow; r <= scanTo; r++)
            {
                int s = ScoreRow(r);
                if (s > bestScore) { bestScore = s; hdrRowNum = r; }
            }

            if (lastCol == 0 || lastRow <= hdrRowNum)
                return Ok(new List<VehicleDto>());

            // 3) Başlık haritası
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var headerRow = ws.Row(hdrRowNum);
            for (int c = firstCol; c <= lastCol; c++)
            {
                var key = Normalize(headerRow.Cell(c).GetString());
                if (!string.IsNullOrEmpty(key)) map[key] = c;
            }

            int ColAny(params string[] names)
            {
                // exact
                foreach (var n in names)
                {
                    var k = Normalize(n);
                    if (map.TryGetValue(k, out var ix)) return ix;
                }
                // contains
                var ns = names.Select(Normalize).ToList();
                foreach (var kv in map)
                    if (ns.Any(n => kv.Key.Contains(n))) return kv.Value;
                return -1;
            }

            int cPlate = ColAny("PLAKA", "PLATE");
            int cDriver = ColAny("SÜRÜCÜ", "SÜRÜCÜ ADI", "ŞOFÖR", "ŞOFÖR ADI", "DRIVER", "KULLANICI", "KULLANICI ADI");
            int cUnit = ColAny("BİRİM", "BIRIM", "UNIT", "YER");
            int cDepartment = ColAny("BÖLÜM", "DEPARTMAN", "DEPARTMENT", "ŞİRKET");
            int cRegion = ColAny("BÖLGE", "REGION", "LOKASYON");
            int cType = ColAny("TİP", "TIP", "TYPE", "ARAÇ TİPİ", "ARAC TIPI");
            int cBrand = ColAny("MARKA", "BRAND");
            int cModel = ColAny("MODEL");
            int cGear = ColAny("VİTES", "VITES", "GEAR");
            int cFuel = ColAny("YAKIT", "FUEL");
            int cFleet = ColAny("FİLO", "FILO", "FLEET");
            int cDesc1 = ColAny("AÇIKLAMA-1", "AÇIKLAMA 1", "AÇIKLAMA1", "AÇIKLAMA");
            int cDesc2 = ColAny("AÇIKLAMA-2", "AÇIKLAMA 2", "AÇIKLAMA2", "GÖREVİ", "GOREVI");

            // 4) Fallback: Plate bulunamadıysa sıra tabanlı okuma
            if (cPlate == -1)
            {
                var order = new[] { "Plate", "Driver", "Unit", "Department", "Region", "Type", "Brand", "Model", "Gear", "Fuel", "Fleet", "Description1", "Description2" };
                for (int i = 0; i < order.Length && (firstCol + i) <= lastCol; i++)
                {
                    switch (order[i])
                    {
                        case "Plate": cPlate = firstCol + i; break;
                        case "Driver": cDriver = firstCol + i; break;
                        case "Unit": cUnit = firstCol + i; break;
                        case "Department": cDepartment = firstCol + i; break;
                        case "Region": cRegion = firstCol + i; break;
                        case "Type": cType = firstCol + i; break;
                        case "Brand": cBrand = firstCol + i; break;
                        case "Model": cModel = firstCol + i; break;
                        case "Gear": cGear = firstCol + i; break;
                        case "Fuel": cFuel = firstCol + i; break;
                        case "Fleet": cFleet = firstCol + i; break;
                        case "Description1": cDesc1 = firstCol + i; break;
                        case "Description2": cDesc2 = firstCol + i; break;
                    }
                }
                // Header'larda ASCII dışı karakter kullanma
                Response.Headers.Append("X-Parse-Warnings", "Baslik net bulunamadi; kolon sirasina gore okundu.");
            }

            // 5) Satırları oku
            var list = new List<VehicleDto>();
            for (int r = hdrRowNum + 1; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                bool anyCell = row.Cells(firstCol, lastCol).Any(c => !string.IsNullOrWhiteSpace(c.GetString()));
                if (!anyCell) continue;

                string Get(int col) => col > 0 ? (row.Cell(col).GetString() ?? "").Trim() : "";

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

            Response.Headers.Append("X-Parsed-Count", list.Count.ToString());
            return Ok(list);

            static string Normalize(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return "";
                s = s.Replace('\u00A0', ' ').Trim().ToUpperInvariant();
                s = s.Replace('İ', 'I').Replace('Ş', 'S').Replace('Ğ', 'G')
                     .Replace('Ü', 'U').Replace('Ö', 'O').Replace('Ç', 'C');
                var sb = new System.Text.StringBuilder(s.Length);
                foreach (var ch in s) if (char.IsLetterOrDigit(ch) || ch == ' ') sb.Append(ch);
                return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ");
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

        //    // Başlık haritası: "AD" -> kolonIndex
        //    var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        //    for (int c = 1; c <= lastCol; c++)
        //    {
        //        var name = (headerRow.Cell(c).GetString() ?? "").Trim();
        //        if (!string.IsNullOrEmpty(name)) map[Normalize(name)] = c;
        //    }

        //    // Eşleme yardımcıları
        //    int ColAny(params string[] names)
        //    {
        //        // 1) tam eşleşme
        //        foreach (var raw in names)
        //        {
        //            var key = Normalize(raw);
        //            if (map.TryGetValue(key, out var ix)) return ix;
        //        }
        //        // 2) içerir eşleşme
        //        var norms = names.Select(Normalize).ToList();
        //        foreach (var kv in map)
        //            if (norms.Any(n => kv.Key.Contains(n))) return kv.Value;

        //        return -1;
        //    }

        //    // Alias’larla kolon indexleri
        //    int cPlate = ColAny("PLAKA", "PLATE");
        //    int cDriver = ColAny("SÜRÜCÜ", "SÜRÜCÜ ADI", "ŞOFÖR", "ŞOFÖR ADI", "DRIVER","KULLANICI ADI","KULLANICI");
        //    int cUnit = ColAny("BİRİM", "BIRIM", "UNIT","YER");
        //    int cDepartment = ColAny("BÖLÜM", "DEPARTMAN", "DEPARTMENT","ŞİRKET");
        //    int cRegion = ColAny("BÖLGE", "REGION","LOKASYON");
        //    int cType = ColAny("TİP", "TIP", "TYPE","ARAÇ TİPİ");
        //    int cBrand = ColAny("MARKA", "BRAND");
        //    int cModel = ColAny("MODEL");
        //    int cGear = ColAny("VİTES", "VITES", "GEAR");
        //    int cFuel = ColAny("YAKIT", "FUEL");
        //    int cFleet = ColAny("FİLO", "FILO", "FLEET");
        //    int cDesc1 = ColAny("AÇIKLAMA-1", "AÇIKLAMA 1", "AÇIKLAMA1", "AÇIKLAMA");
        //    int cDesc2 = ColAny("AÇIKLAMA-2", "AÇIKLAMA 2", "AÇIKLAMA2","GÖREVİ");

        //    var list = new List<VehicleDto>();

        //    for (int r = 2; r <= lastRow; r++)
        //    {
        //        var row = ws.Row(r);
        //        string Get(int col) => col > 0 ? row.Cell(col).GetString()?.Trim() ?? "" : "";

        //        var dto = new VehicleDto
        //        {
        //            Plate = Get(cPlate),
        //            Driver = Get(cDriver),
        //            Unit = Get(cUnit),
        //            Department = Get(cDepartment),
        //            Region = Get(cRegion),
        //            Type = Get(cType),
        //            Brand = Get(cBrand),
        //            Model = Get(cModel),
        //            Gear = Get(cGear),
        //            Fuel = Get(cFuel),
        //            Fleet = Get(cFleet),
        //            Description1 = Get(cDesc1),
        //            Description2 = Get(cDesc2)
        //        };

        //        if (!string.IsNullOrWhiteSpace(dto.Plate))
        //            list.Add(dto);
        //    }

        //    return Ok(list);

        //    static string Normalize(string s)
        //    {
        //        s = (s ?? "").Trim().ToUpperInvariant();
        //        return s.Replace('İ', 'I').Replace('Ş', 'S').Replace('Ğ', 'G')
        //                .Replace('Ü', 'U').Replace('Ö', 'O').Replace('Ç', 'C');
        //    }
        //}

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
