using CatalogService.Api.Core.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace CatalogService.Api.Infrastructure.Context
{
    public class CatalogContextSeed
    {
        public async Task SeedAsync(CatalogContext context, IWebHostEnvironment env, ILogger<CatalogContextSeed> logger)
        {
            var policy = Policy.Handle<SqlException>().
                WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retry => TimeSpan.FromSeconds(5),
                    onRetry: (exception, timeSpan, retry, ctx) =>
                    {
                        logger.LogWarning(exception, "[{prefix}] Exception {ExceptionType} with message {Message} detected on attempt {retry} of {retries}", nameof(logger), exception.GetType().Name, exception.Message, retry, 3);
                    }
                );


            var setupDirPath = Path.Combine(env.ContentRootPath, "Infrastructure", "Setup", "SeedFiles");
            var picturePath = "Pics";

            await policy.ExecuteAsync(() => ProcessSeeding(context, setupDirPath, picturePath, logger));
        }


        private async Task ProcessSeeding(CatalogContext context, string setupDirPath, string picturePath, ILogger logger)
        {

            if (!context.Expenses.Any())
            {
                await context.Expenses.AddRangeAsync(GetExpensesFromFiles(setupDirPath));
                await context.SaveChangesAsync();
            }
           
        }


        private IEnumerable<Expense> GetExpensesFromFiles(string contentPath)
        {
            var expenses = new List<Expense>();

            var expenseLines = File.ReadAllLines(Path.Combine(contentPath, "Expenses.txt"))
                                   .Skip(1);

            var receiptLines = File.ReadAllLines(Path.Combine(contentPath, "ReceiptItems.txt"))
                                   .Skip(1)
                                   .Select(l => l.Split(','))
                                   .Select(r => new
                                   {
                                       Company = r[0].Trim(),
                                       Item = r[1].Trim(),
                                       Amount = decimal.Parse(r[2]),
                                       VatRate = decimal.Parse(r[3])
                                   });

            var productLines = File.ReadAllLines(Path.Combine(contentPath, "ProductDetails.txt"))
                                   .Skip(1)
                                   .Select(l => l.Split(','))
                                   .Select(p => new
                                   {
                                       Item = p[0].Trim(),
                                       Name = p[1].Trim(),
                                       Amount = decimal.Parse(p[2]),
                                       VatRate = decimal.Parse(p[3])
                                   });

            foreach (var company in expenseLines)
            {
                var expense = new Expense
                {
                    Company = company,
                    FullName = "Bilinmiyor",
                    PersonnelCode = "000.00.00.00000",
                    Note = "Otomatik yüklendi",
                    AccountingCode = "000.00.00.00000",
                    AmountExclVat = 0,
                    VatRate = 0,
                    ReceiptDetails = new List<ReceiptItem>()
                };

                var relatedReceipts = receiptLines.Where(r => r.Company == company);

                foreach (var r in relatedReceipts)
                {
                    var receipt = new ReceiptItem
                    {
                        Company = r.Company,
                        Item = r.Item,
                        Amount = r.Amount,
                        VatRate = r.VatRate,
                        AccountingCode = "000.00.00.00000",
                        PersonnelCode = "000.00.00.00000",
                        FullName = "Bilinmiyor",
                        Note = "Otomatik yüklendi",
                        AmountExclVat = r.Amount,
                        ProductDetails = productLines
                            .Where(p => p.Item == r.Item)
                            .Select(p => new ProductDetail
                            {
                                Company = r.Company,
                                Name = p.Name,
                                Amount = p.Amount,
                                VatRate = p.VatRate,
                                AccountingCode = "000.00.00.00000",
                                PersonnelCode = "000.00.00.00000",
                                FullName = "Bilinmiyor",
                                Note = "Otomatik yüklendi",
                                AmountExclVat = p.Amount
                            }).ToList()
                    };

                    expense.ReceiptDetails.Add(receipt);
                }

                expense.AmountExclVat = expense.ReceiptDetails.Sum(x => x.Amount);
                expense.VatRate = expense.ReceiptDetails.Any() ? expense.ReceiptDetails.Average(x => x.VatRate) : 0;

                expenses.Add(expense);
            }

            return expenses;
        }
    }
}


