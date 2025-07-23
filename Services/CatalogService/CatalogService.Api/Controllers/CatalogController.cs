using CatalogService.Api.Core.Application.ViewModels;
using CatalogService.Api.Core.Domain;

using CatalogService.Api.Infrastructure;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
        private readonly CatalogSettings _settings;

        public CatalogController(CatalogContext context, IOptionsSnapshot<CatalogSettings> settings)
        {
            _catalogContext = context ?? throw new ArgumentNullException(nameof(context));
            _settings = settings.Value;

            context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }
       

        [HttpGet("expenses")]
        [ProducesResponseType(typeof(List<Expense>), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<List<Expense>>> GetAllExpensesAsync()
        {
            var expenses = await _catalogContext.Expenses
                .Include(e => e.ReceiptDetails)
                    .ThenInclude(r => r.ProductDetails)
                .ToListAsync();

            return Ok(expenses);
        }

        [HttpGet("expenses/{id}")]
        [ProducesResponseType(typeof(Expense), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<ActionResult<Expense>> GetExpenseByIdAsync(int id)
        {
            var expense = await _catalogContext.Expenses
                .Include(e => e.ReceiptDetails)
                    .ThenInclude(r => r.ProductDetails)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (expense == null)
            {
                return NotFound();
            }

            return Ok(expense);
        }

        [HttpGet("expenses/bycompany")]
        [ProducesResponseType(typeof(List<Expense>), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<List<Expense>>> GetExpensesByCompanyAsync([FromQuery] string company)
        {
            var expenses = await _catalogContext.Expenses
                .Include(e => e.ReceiptDetails)
                    .ThenInclude(r => r.ProductDetails)
                .Where(e => e.Company == company)
                .ToListAsync();

            return Ok(expenses);
        }

        [HttpGet("expenses/paged")]
        [ProducesResponseType(typeof(PaginatedItemsViewModel<Expense>), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<PaginatedItemsViewModel<Expense>>> GetPagedExpensesAsync([FromQuery] int pageIndex = 0, [FromQuery] int pageSize = 10)
        {
            try
            {
                var totalItems = await _catalogContext.Expenses.CountAsync();

                var items = await _catalogContext.Expenses
                    .Include(e => e.ReceiptDetails)
                        .ThenInclude(r => r.ProductDetails)
                    .OrderByDescending(x => x.Id)
                    .Skip(pageIndex * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var dtoItems = items.Select(e => new Expense
                {
                    Id = e.Id,
                    Company = e.Company,
                    Note = e.Note,
                    AmountExclVat = e.AmountExclVat,
                    VatRate = e.VatRate,
                    ReceiptDetails = e.ReceiptDetails?.Select(r => new ReceiptItem
                    {
                        Id = r.Id,
                        Company = r.Company,
                        Item = r.Item,
                        Amount = r.Amount,
                        VatRate = r.VatRate,
                        AccountingCode = r.AccountingCode,
                        PersonnelCode = r.PersonnelCode,
                        FullName = r.FullName,
                        Note = r.Note,
                        AmountExclVat = r.AmountExclVat,
                        ProductDetails = r.ProductDetails?.Select(p => new ProductDetail
                        {
                            Id = p.Id,
                            Company = p.Company,
                            Name = p.Name,
                            Amount = p.Amount,
                            VatRate = p.VatRate,
                            AccountingCode = p.AccountingCode,
                            PersonnelCode = p.PersonnelCode,
                            FullName = p.FullName,
                            Note = p.Note,
                            AmountExclVat = p.AmountExclVat
                        }).ToList() ?? new List<ProductDetail>()
                    }).ToList() ?? new List<ReceiptItem>()
                }).ToList();

                var model = new PaginatedItemsViewModel<Expense>(pageIndex, pageSize, totalItems, dtoItems);
                return Ok(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Hata oluştu: " + ex.ToString());
                return StatusCode(500, ex.ToString());
            }
        }




    }
}