using CatalogService.Api.Features.Payroll.Commands.CalculatePayroll;
using CatalogService.Api.Features.Payroll.Commands.CompareDistribution;
using CatalogService.Api.Features.Payroll.Dtos.Requests;
using CatalogService.Api.Features.Payroll.Dtos.Responses;
using CatalogService.Api.Features.Payroll.Dtos.Shared;
using CatalogService.Api.Features.Payroll.Enums;
using CatalogService.Api.Features.Payroll.Queries.GetPayrollCalculatorBootstrap;
using CatalogService.Api.Features.Payroll.Queries.GetPayrollLawTypes;
using CatalogService.Api.Features.Payroll.Queries.GetPayrollParametersByYear;
using CatalogService.Api.Features.Payroll.Services.Interfaces;
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
        private readonly IDistributionExportService _distributionExportService;
        private readonly IPayrollCalculationExportService _payrollExportService;

        public PayrollPublicController(
            IMediator mediator,
            IDistributionExportService distributionExportService,
            IPayrollCalculationExportService payrollExportService)
        {
            _mediator = mediator;
            _distributionExportService = distributionExportService;
            _payrollExportService = payrollExportService;
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
            var bytes = _payrollExportService.ExportToExcel(result, request);
            var fileName = result.EmployeeType == PayrollEmployeeType.Honorarium
                ? $"huzur_hakki_{request.Year}.xlsx"
                : $"bordro_{request.Year}.xlsx";
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        [HttpPost("export-pdf")]
        public async Task<IActionResult> ExportPdf([FromBody] CalculatePayrollRequest request)
        {
            var result = await _mediator.Send(MapToCommand(request));
            var bytes = _payrollExportService.ExportToPdf(result, request);
            var fileName = result.EmployeeType == PayrollEmployeeType.Honorarium
                ? $"huzur_hakki_{request.Year}.pdf"
                : $"bordro_{request.Year}.pdf";
            return File(bytes, "application/pdf", fileName);
        }

        [HttpGet("law-types")]
        public async Task<ActionResult<List<PayrollLawTypeDto>>> GetLawTypes([FromQuery] int year = 2026)
        {
            var result = await _mediator.Send(new GetPayrollLawTypesQuery { Year = year });
            return Ok(result);
        }

        [HttpPost("compare-distribution")]
        public async Task<ActionResult<DistributionComparisonResultDto>> CompareDistribution(
            [FromBody] CompareDistributionCommand request)
        {
            var result = await _mediator.Send(request);
            return Ok(result);
        }

        [HttpPost("compare-distribution/export-excel")]
        public async Task<IActionResult> ExportComparisonExcel([FromBody] CompareDistributionCommand request)
        {
            var result = await _mediator.Send(request);
            var bytes = _distributionExportService.ExportToExcel(result, request.StopajOrani, request.Year);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"kar_dagitimi_karsilastirma_{request.Year}.xlsx");
        }

        [HttpPost("compare-distribution/export-pdf")]
        public async Task<IActionResult> ExportComparisonPdf([FromBody] CompareDistributionCommand request)
        {
            var result = await _mediator.Send(request);
            var bytes = _distributionExportService.ExportToPdf(result, request.StopajOrani, request.Year);
            return File(bytes,
                "application/pdf",
                $"kar_dagitimi_karsilastirma_{request.Year}.pdf");
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

    }
}
