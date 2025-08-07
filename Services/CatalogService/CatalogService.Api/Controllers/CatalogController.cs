using AutoMapper;
using CatalogService.Api.Contracts.Dtos;
using CatalogService.Api.Core.Application.ViewModels;
using CatalogService.Api.Core.Domain;

using CatalogService.Api.Infrastructure;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace CatalogService.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CatalogController : ControllerBase
    {
        private readonly CatalogContext _catalogContext;
        private readonly IMapper _catalogmapper;
        private readonly CatalogSettings _settings;

        public CatalogController(IMapper mapper,CatalogContext context, IOptionsSnapshot<CatalogSettings> settings)
        {
            _catalogContext = context ?? throw new ArgumentNullException(nameof(context));
            _settings = settings.Value;
            _catalogmapper=mapper; ;
            context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }
       

        [HttpGet("expenses")]
        [ProducesResponseType(typeof(List<Expense>), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<List<Expense>>> GetAllExpensesAsync()
        {
            var expenses = await _catalogContext.Expenses
                .Include(e => e.ReceiptItems)
                    .ThenInclude(r => r.ProductDetails)
                .ToListAsync();

            return Ok(expenses);
        }

        [HttpGet("personnels")]
        [ProducesResponseType(typeof(Expense), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<ActionResult<PersonnelDto>> GetPersonnel()
        {
            var personnel = await _catalogContext.Personnels
                .Select(p => new PersonnelDto
                {
                    Id = p.Id,
                    Company = p.Company,
                    Title = p.Title,
                    PhoneNumber = p.PhoneNumber,
                    Department = p.Department,
                    Unit= p.Unit,
                    ExpenseCenter = p.ExpenseCenter,
                    NationalId = p.NationalId,
                    IBAN = p.IBAN,
                    Email = p.Email,
                    FirstName= p.FirstName,
                    LastName= p.LastName,
                    SalaryExpenseNumber = p.SalaryExpenseNumber,
                    CaseExpenseNumber = p.CaseExpenseNumber,
                    NormalExpenseNumber = p.NormalExpenseNumber,
                    FullName = p.FullName,
                    
                    
                })
                .ToListAsync();

            return Ok(personnel);
        }


        [HttpGet("accountingcodes")]
        [ProducesResponseType(typeof(Expense), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<ActionResult<AccountingCodeDto>> GetAccountingCodes()
        {
            var accountingCodeDto = await _catalogContext.AccountingCodes
                .Select(p => new AccountingCodeDto
                {
                    Id = p.Id,
                    Code = p.Code,
                    Description = p.Description
                })
                .ToListAsync();

            return Ok(accountingCodeDto);
        }

        [HttpGet("expenses/{id}")]
        [ProducesResponseType(typeof(Expense), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<ActionResult<Expense>> GetExpenseByIdAsync(int id)
        {
            var expense = await _catalogContext.Expenses
                .Include(e => e.ReceiptItems)
                    .ThenInclude(r => r.ProductDetails)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (expense == null)
            {
                return NotFound();
            }

            return Ok(expense);
        }


        [HttpGet("expenses/paged")]
        [ProducesResponseType(typeof(PaginatedItemsViewModel<ExpenseDto>), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<PaginatedItemsViewModel<ExpenseDto>>> GetPagedExpensesAsync(
     [FromQuery] int pageIndex = 0,
     [FromQuery] int pageSize = 10)
        {
            try
            {
                var totalItems = await _catalogContext.Expenses.CountAsync();

                var items = await _catalogContext.Expenses
                    .Include(e => e.ReceiptItems)
                        .ThenInclude(r => r.ProductDetails)
                    .OrderByDescending(x => x.Id)
                    .Skip(pageIndex * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var dtoItems = items.Select(e => new ExpenseDto
                {
                    Id = e.Id,
                    ExpenseCode = e.ExpenseCode,
                    PersonnelFullName = e.PersonnelFullName,
                    PersonnelAccountingCode = e.PersonnelAccountingCode,
                    ExpenseDate = e.ExpenseDate,
                    ProjectCode = e.ProjectCode,
                    CreatedDate = e.CreatedDate,
                    CreatedTime = e.CreatedTime,
                    ApprovedBy = e.ApprovedBy,
                    ApprovedAt = e.ApprovedAt,
                    TotalAmount = e.TotalAmount,
                    TotalVat = e.TotalVat,
                    Description = e.Description,
                    ReceiptItems = e.ReceiptItems?.Select(r => new ReceiptItemDto
                    {
                        Id = r.Id,
                        ExpenseId = r.ExpenseId,
                        ExpenseCode = r.ExpenseCode,
                        Type = r.Type,
                        AccountingCode = r.AccountingCode,
                        AccountingCodeDescription = r.AccountingCodeDescription,
                        Description = r.Description,
                        Quantity = r.Quantity,
                        Unit = r.Unit,
                        TotalAmount = r.TotalAmount,
                        TotalVat = r.TotalVat,
                        ReceiptNumber = r.ReceiptNumber,
                        ReceiptDate = r.ReceiptDate,
                        ProductDetails = r.ProductDetails?.Select(p => new ProductDetailDto
                        {
                            Id = p.Id,
                            Rank = p.Rank,
                            TaxBase = p.TaxBase,
                            VatRate = p.VatRate,
                            VatAmount = p.VatAmount,
                            TotalAmount = p.TotalAmount,
                            ReceiptItemId = p.ReceiptItemId,
                            // ReceiptItem property opsiyonel olarak gönderilebilir, döngüsel bağı engellemek için null bırakıyorum
                            ReceiptItem = null
                        }).ToList() ?? new List<ProductDetailDto>()
                    }).ToList() ?? new List<ReceiptItemDto>()
                }).ToList();

                var model = new PaginatedItemsViewModel<ExpenseDto>(pageIndex, pageSize, totalItems, dtoItems);
                return Ok(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Hata oluştu: " + ex);
                return StatusCode(500, ex.ToString());
            }
        }




        [HttpPost]
        public async Task<IActionResult> CreateExpense([FromBody] ExpenseDto dto)
        {
            var expense = _catalogmapper.Map<Expense>(dto);
            _catalogContext.Expenses.Add(expense);
            await _catalogContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetExpenseByIdAsync), new { id = expense.Id }, expense);
        }


    }
}