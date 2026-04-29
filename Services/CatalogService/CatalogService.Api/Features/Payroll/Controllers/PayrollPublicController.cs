using CatalogService.Api.Features.Payroll.Commands.CalculatePayroll;
using CatalogService.Api.Features.Payroll.Dtos.Requests;
using CatalogService.Api.Features.Payroll.Dtos.Responses;
using CatalogService.Api.Features.Payroll.Dtos.Shared;
using CatalogService.Api.Features.Payroll.Enums;
using CatalogService.Api.Features.Payroll.Queries.GetPayrollCalculatorBootstrap;
using CatalogService.Api.Features.Payroll.Queries.GetPayrollLawTypes;
using CatalogService.Api.Features.Payroll.Queries.GetPayrollParametersByYear;
using ClosedXML.Excel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Payroll.Controllers
{
    [ApiController]
    [Route("api/public/payroll")]
    [AllowAnonymous]
    public class PayrollPublicController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PayrollPublicController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("bootstrap")]
        public async Task<IActionResult> GetBootstrap([FromQuery] int year = 2026)
        {
            var result = await _mediator.Send(new GetPayrollCalculatorBootstrapQuery { Year = year });
            if (result is null)
                return NotFound($"'{year}' yılı için bootstrap verisi bulunamadı.");
            return Ok(result);
        }

        [HttpGet("parameters/{year:int}")]
        public async Task<IActionResult> GetParametersByYear(int year)
        {
            var result = await _mediator.Send(new GetPayrollParametersByYearQuery { Year = year });
            if (result is null)
                return NotFound($"'{year}' yılı için payroll parametresi bulunamadı.");
            return Ok(result);
        }

        [HttpPost("calculate")]
        public async Task<IActionResult> Calculate([FromBody] CalculatePayrollRequest request)
        {
            var result = await _mediator.Send(MapToCommand(request));
            return Ok(result);
        }

        [HttpPost("export-excel")]
        public async Task<IActionResult> ExportExcel([FromBody] CalculatePayrollRequest request)
        {
            var result = await _mediator.Send(MapToCommand(request));
            var bytes = GeneratePayrollExcel(result, request);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"bordro_{request.Year}.xlsx");
        }

        [HttpGet("law-types")]
        public async Task<ActionResult<List<PayrollLawTypeDto>>> GetLawTypes([FromQuery] int year = 2026)
        {
            var result = await _mediator.Send(new GetPayrollLawTypesQuery { Year = year });
            return Ok(result);
        }

        private static CalculatePayrollCommand MapToCommand(CalculatePayrollRequest request) =>
            new()
            {
                Year = request.Year,
                CalculationType = request.CalculationType,
                EmployeeType = request.EmployeeType,
                HasMandatoryBes = request.HasMandatoryBes,
                DisabilityType = request.DisabilityType,
                IncludeMinimumWageExemption = request.IncludeMinimumWageExemption,
                IncludeStampTax = request.IncludeStampTax,
                IncludeEmployerCost = request.IncludeEmployerCost,
                StartMonth = request.StartMonth,
                PreviousCumulativeTaxBase = request.PreviousCumulativeTaxBase,
                Months = request.Months,
                LawCode = request.LawCode,
                IsManufacturingSector = request.IsManufacturingSector,
            };

        private static byte[] GeneratePayrollExcel(
            CalculatePayrollResponse result,
            CalculatePayrollRequest request)
        {
            var monthNames = new[] { "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
                "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };

            bool hasDisability = result.DisabilityType != PayrollDisabilityType.None;
            bool hasEmployer = request.IncludeEmployerCost;
            bool hasIncentive = request.LawCode != "00000";

            string? incentiveSource = result.Months
                .FirstOrDefault(x => x.IncentiveSource != null)?.IncentiveSource;
            string incentiveLabel = incentiveSource != null
                ? $"SGK Teşvik ({incentiveSource})"
                : "SGK Teşvik";

            string calcTypeLabel = result.CalculationType == PayrollCalculationType.GrossToNet
                ? "Brütten Nete" : "Netten Brüte";

            const string numFmt = "#,##0.00";

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Bordro");

            // Title row
            ws.Cell(1, 1).Value = $"Ücret Bordrosu Hesaplama — {result.Year}";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 13;

            // Info row
            ws.Cell(2, 1).Value = $"Kanun: {request.LawCode}";
            ws.Cell(2, 3).Value = calcTypeLabel;

            const int headerRow = 4;

            // Build column headers
            int col = 1;
            void Header(string text)
            {
                var cell = ws.Cell(headerRow, col++);
                cell.Value = text;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2C5F8A");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            Header("Ay");
            Header("Tutar");
            Header("Brüt Ücret");
            Header("SGK İşçi");
            Header("İSP İşçi");
            if (hasDisability) Header("Engel İndirimi");
            Header("GV Matrahı");
            Header("Kümülatif Matrah");
            Header("Hesaplanan GV");
            Header("İstisna GV");
            Header("Ödenecek GV");
            Header("Hesaplanan DV");
            Header("İstisna DV");
            Header("Ödenecek DV");
            Header("Kesintiler");
            Header("Net Ücret");
            if (hasEmployer)
            {
                Header("SGK İşveren Brüt");
                if (hasIncentive) Header(incentiveLabel);
                Header("SGK İşveren Net");
                Header("İSP İşveren");
                Header("Toplam İşveren Maliyet");
            }

            int lastCol = col - 1;

            // Data rows
            foreach (var m in result.Months)
            {
                int row = headerRow + m.Month;
                col = 1;

                void Num(decimal? val)
                {
                    var c = ws.Cell(row, col++);
                    c.Value = val ?? 0m;
                    c.Style.NumberFormat.Format = numFmt;
                }

                ws.Cell(row, col++).Value = monthNames[m.Month];
                Num(m.InputAmount);
                Num(m.GrossSalary);
                Num(m.SgkEmployeeAmount);
                Num(m.UnemploymentEmployeeAmount);
                if (hasDisability) Num(m.DisabilityExemptionAmount);
                Num(m.IncomeTaxBase);
                Num(m.CumulativeIncomeTaxBase);
                Num(m.CalculatedIncomeTax);
                Num(m.IncomeTaxExemption);
                Num(m.PayableIncomeTax);
                Num(m.CalculatedStampTax);
                Num(m.StampTaxExemption);
                Num(m.PayableStampTax);
                Num(m.TotalDeductions);
                Num(m.NetSalary);
                if (hasEmployer)
                {
                    Num(m.SgkEmployerGross);
                    if (hasIncentive) Num(m.SgkEmployerIncentive);
                    Num(m.SgkEmployerNet);
                    Num(m.UnemploymentEmployerAmount);
                    Num(m.TotalEmployerCost);
                }
            }

            // Totals row
            int totalRow = headerRow + 13;

            if (result.Totals != null)
            {
                col = 1;

                void Total(decimal? val)
                {
                    var c = ws.Cell(totalRow, col++);
                    c.Value = val ?? 0m;
                    c.Style.NumberFormat.Format = numFmt;
                    c.Style.Font.Bold = true;
                }

                var tc = ws.Cell(totalRow, col++);
                tc.Value = "Toplam";
                tc.Style.Font.Bold = true;
                col++; // skip Tutar

                Total(result.Totals.TotalGrossSalary);
                Total(result.Totals.TotalSgkEmployeeAmount);
                Total(result.Totals.TotalUnemploymentEmployeeAmount);
                if (hasDisability) Total(result.Totals.TotalDisabilityExemptionAmount);
                Total(result.Totals.TotalIncomeTaxBase);
                Total(result.Totals.TotalIncomeTaxBase);
                Total(result.Totals.TotalCalculatedIncomeTax);
                Total(result.Totals.TotalIncomeTaxExemption);
                Total(result.Totals.TotalPayableIncomeTax);
                Total(result.Totals.TotalCalculatedStampTax);
                Total(result.Totals.TotalStampTaxExemption);
                Total(result.Totals.TotalPayableStampTax);
                Total(result.Totals.TotalDeductions);
                Total(result.Totals.TotalNetSalary);
                if (hasEmployer)
                {
                    Total(result.Totals.TotalSgkEmployerGross);
                    if (hasIncentive) Total(result.Totals.TotalSgkEmployerIncentive);
                    Total(result.Totals.TotalSgkEmployerNet);
                    Total(result.Totals.TotalUnemploymentEmployerAmount);
                    Total(result.Totals.TotalEmployerCost);
                }
            }

            ws.Range(headerRow, 1, totalRow, lastCol).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(headerRow, 1, totalRow, lastCol).Style.Border.InsideBorder = XLBorderStyleValues.Hair;
            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            return stream.ToArray();
        }
    }
}
