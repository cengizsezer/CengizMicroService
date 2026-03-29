using CatalogService.Api.Features.Payroll.Commands.CalculatePayroll;
using CatalogService.Api.Features.Payroll.Dtos.Requests;
using CatalogService.Api.Features.Payroll.Queries.GetPayrollCalculatorBootstrap;
using CatalogService.Api.Features.Payroll.Queries.GetPayrollParametersByYear;
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
            var result = await _mediator.Send(new GetPayrollCalculatorBootstrapQuery
            {
                Year = year
            });

            if (result is null)
                return NotFound($"'{year}' yılı için bootstrap verisi bulunamadı.");

            return Ok(result);
        }

        [HttpGet("parameters/{year:int}")]
        public async Task<IActionResult> GetParametersByYear(int year)
        {
            var result = await _mediator.Send(new GetPayrollParametersByYearQuery
            {
                Year = year
            });

            if (result is null)
                return NotFound($"'{year}' yılı için payroll parametresi bulunamadı.");

            return Ok(result);
        }

        [HttpPost("calculate")]
        public async Task<IActionResult> Calculate([FromBody] CalculatePayrollRequest request)
        {
            var command = new CalculatePayrollCommand
            {
                Year = request.Year,
                CalculationType = request.CalculationType,
                EmployeeType = request.EmployeeType,
                HasMandatoryBes = request.HasMandatoryBes,
                DisabilityType = request.DisabilityType,
                StartMonth = request.StartMonth,
                PreviousCumulativeTaxBase = request.PreviousCumulativeTaxBase,
                Months = request.Months
            };

            var result = await _mediator.Send(command);
            return Ok(result);
        }
    }
}
