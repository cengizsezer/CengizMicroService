using CatalogService.Api.Features.Payroll.Acik;
using CatalogService.Api.Features.Payroll.Commands.CompareDistribution;
using CatalogService.Api.Features.Payroll.Dtos.Requests;
using CatalogService.Api.Features.Payroll.Dtos.Responses;
using CatalogService.Api.Features.Payroll.Dtos.Shared;
using CatalogService.Api.Features.Payroll.Enums;
using CatalogService.Api.Features.Payroll.Queries.GetPayrollLawTypes;
using CatalogService.Api.Features.Payroll.Services.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CatalogService.Api.Features.Payroll.Controllers
{
    /// <summary>
    /// Bordro hesaplayıcısının açık (girişsiz) kapısı — <c>/payroll-calculator</c> sayfası
    /// buraya gelir (KARARLAR §155).
    ///
    /// Uygulama içi ekranla aynı işlevler: kanun listesi, hesaplama, bordro Excel/PDF,
    /// kâr dağıtımı karşılaştırması ve onun Excel/PDF'i. <c>bootstrap</c> ve
    /// <c>parameters</c> bilinçli olarak YOK (hiçbir ekran çağırmıyor). Her uç girişli
    /// <see cref="PayrollController"/> ile birebir aynı akış: aynı MediatR handler'ları,
    /// aynı dışa aktarma servisleri — hesap ve dosya üretim kodu kopyalanmadı.
    /// Veritabanına, firmaya, kullanıcıya dokunulmaz, hiçbir şey yazılmaz.
    ///
    /// Gateway'de kimliksiz <c>/catalog/payroll/acik/{everything}</c> rotasından gelir.
    /// Hesap uçları <see cref="BordroAcikHizSiniri.Politika"/> (controller düzeyi),
    /// dört dosya ucu ayrı ve daha dar <see cref="BordroAcikHizSiniri.DosyaPolitika"/>
    /// (aksiyon düzeyi, controller'dakini ezer) arkasında.
    /// </summary>
    [ApiController]
    [Route("api/catalog/payroll/acik")]
    [AllowAnonymous]
    [EnableRateLimiting(BordroAcikHizSiniri.Politika)]
    public class PayrollAcikController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IDistributionExportService _distributionExportService;
        private readonly IPayrollCalculationExportService _payrollExportService;

        public PayrollAcikController(
            IMediator mediator,
            IDistributionExportService distributionExportService,
            IPayrollCalculationExportService payrollExportService)
        {
            _mediator = mediator;
            _distributionExportService = distributionExportService;
            _payrollExportService = payrollExportService;
        }

        [HttpGet("law-types")]
        public async Task<ActionResult<List<PayrollLawTypeDto>>> GetLawTypes([FromQuery] int year = 2026)
        {
            var result = await _mediator.Send(new GetPayrollLawTypesQuery { Year = year });
            return Ok(result);
        }

        [HttpPost("calculate")]
        public async Task<ActionResult<CalculatePayrollResponse>> Calculate([FromBody] CalculatePayrollRequest request)
        {
            var result = await _mediator.Send(CalculatePayrollEslemesi.KomutaCevir(request));
            return Ok(result);
        }

        [HttpPost("export-excel")]
        [EnableRateLimiting(BordroAcikHizSiniri.DosyaPolitika)]
        public async Task<IActionResult> ExportExcel([FromBody] CalculatePayrollRequest request)
        {
            var result = await _mediator.Send(CalculatePayrollEslemesi.KomutaCevir(request));
            var bytes = _payrollExportService.ExportToExcel(result, request);
            var fileName = result.EmployeeType == PayrollEmployeeType.Honorarium
                ? $"huzur_hakki_{request.Year}.xlsx"
                : $"bordro_{request.Year}.xlsx";
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        [HttpPost("export-pdf")]
        [EnableRateLimiting(BordroAcikHizSiniri.DosyaPolitika)]
        public async Task<IActionResult> ExportPdf([FromBody] CalculatePayrollRequest request)
        {
            var result = await _mediator.Send(CalculatePayrollEslemesi.KomutaCevir(request));
            var bytes = _payrollExportService.ExportToPdf(result, request);
            var fileName = result.EmployeeType == PayrollEmployeeType.Honorarium
                ? $"huzur_hakki_{request.Year}.pdf"
                : $"bordro_{request.Year}.pdf";
            return File(bytes, "application/pdf", fileName);
        }

        /// <summary>
        /// Hesap sınırında (dosya sınırında DEĞİL): karşılaştırma penceresi açılınca ve
        /// yıl/stopaj listesi değişince kendiliğinden çağrılıyor.
        /// </summary>
        [HttpPost("compare-distribution")]
        public async Task<ActionResult<DistributionComparisonResultDto>> CompareDistribution(
            [FromBody] CompareDistributionCommand request)
        {
            var result = await _mediator.Send(request);
            return Ok(result);
        }

        [HttpPost("compare-distribution/export-excel")]
        [EnableRateLimiting(BordroAcikHizSiniri.DosyaPolitika)]
        public async Task<IActionResult> ExportComparisonExcel([FromBody] CompareDistributionCommand request)
        {
            var result = await _mediator.Send(request);
            var bytes = _distributionExportService.ExportToExcel(result, request.StopajOrani, request.Year);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"kar_dagitimi_karsilastirma_{request.Year}.xlsx");
        }

        [HttpPost("compare-distribution/export-pdf")]
        [EnableRateLimiting(BordroAcikHizSiniri.DosyaPolitika)]
        public async Task<IActionResult> ExportComparisonPdf([FromBody] CompareDistributionCommand request)
        {
            var result = await _mediator.Send(request);
            var bytes = _distributionExportService.ExportToPdf(result, request.StopajOrani, request.Year);
            return File(bytes,
                "application/pdf",
                $"kar_dagitimi_karsilastirma_{request.Year}.pdf");
        }
    }
}
