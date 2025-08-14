using CatalogService.Api.Core.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Polly;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatalogService.Api.Infrastructure.Context
{
    public class CatalogContextSeed
    {
        /// <summary>
        /// Çoklu tenant için seed. Örn: new[] { "201","106","108","105","107" }
        /// </summary>
        public async Task SeedAsync(
            CatalogContext context,
            IWebHostEnvironment env,
            ILogger<CatalogContextSeed> logger,
            IEnumerable<string> tenantNos,
            bool force = false)
        {
            var policy = Policy.Handle<SqlException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: _ => TimeSpan.FromSeconds(5),
                    onRetry: (ex, _, retry, __) =>
                    {
                        logger.LogWarning(ex,
                            "[Seed] Retry {retry}/3: {Message}",
                            retry, ex.Message);
                    });

            var setupDirPath = Path.Combine(env.ContentRootPath, "Infrastructure", "Setup", "SeedFiles");

            foreach (var tenantNo in tenantNos.Distinct())
            {
                Console.WriteLine($"[Seed] Expense.TenantNo={tenantNo} {this.ToString()}");
                await policy.ExecuteAsync(() => ProcessSeeding(context, setupDirPath, logger, tenantNo, force));
            }
        }

        // İstersen eski imzayı da korumak için bir helper bırakıyoruz:
        public Task SeedAsync(CatalogContext context, IWebHostEnvironment env, ILogger<CatalogContextSeed> logger, bool force = false)
            => SeedAsync(context, env, logger, new[] { "201" }, force); // default tek tenant; istersen değiştir

        private async Task ProcessSeeding(
            CatalogContext context,
            string setupDirPath,
            ILogger logger,
            string tenantNo,
            bool force)
        {
            Console.WriteLine($"🚀 [Seeder] Tenant {tenantNo} seeding başladı…");
            Console.WriteLine($"📂 setupDirPath: {setupDirPath}");
            Console.WriteLine($"🧠 Database: {context.Database.GetDbConnection().ConnectionString}");

            // ========== PERSONNEL ==========
            bool hasPersonnels = await context.Personnels.IgnoreQueryFilters().AnyAsync(p => p.TenantNo == tenantNo);
            if (force || !hasPersonnels)
            {
                var personnels = GetPersonnelsFromCsv(setupDirPath, tenantNo)
                    .Select(p => { p.TenantNo = tenantNo; return p; })
                    .ToList();

                Console.WriteLine($"[Seed:{tenantNo}] ✅ Personel sayısı: {personnels.Count}");
                if (personnels.Count > 0)
                {
                    await context.Personnels.AddRangeAsync(personnels);
                    try
                    {
                        await context.SaveChangesAsync();
                        logger.LogInformation("[Seed:{Tenant}] ✅ Personeller kaydedildi.", tenantNo);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ ({tenantNo}) Personeller kaydedilirken hata: {ex.Message}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"[Seed:{tenantNo}] ℹ️ Personeller zaten mevcut, atlandı.");
            }

            // ========== ACCOUNTING CODES ==========
            bool hasAcc = await context.AccountingCodes.IgnoreQueryFilters().AnyAsync(a => a.TenantNo == tenantNo);
            if (force || !hasAcc)
            {
                var accountingCodes = GetAccountingCodesFromCsv(setupDirPath, tenantNo)
                    .Select(a => { a.TenantNo = tenantNo; return a; })
                    .ToList();

                Console.WriteLine($"[Seed:{tenantNo}] ✅ Muhasebe kodu sayısı: {accountingCodes.Count}");
                if (accountingCodes.Count > 0)
                {
                    await context.AccountingCodes.AddRangeAsync(accountingCodes);
                    try
                    {
                        await context.SaveChangesAsync();
                        Console.WriteLine($"[Seed:{tenantNo}] ✅ Muhasebe kodları kaydedildi.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ ({tenantNo}) Muhasebe kodları kaydedilirken hata: {ex.Message}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"[Seed:{tenantNo}] ℹ️ Muhasebe kodları zaten mevcut, atlandı.");
            }

            // ========== EXPENSES (ReceiptItem + ProductDetail ile birlikte) ==========
            bool hasExpenses = await context.Expenses.IgnoreQueryFilters().AnyAsync(e => e.TenantNo == tenantNo);
            if (force || !hasExpenses)
            {
                var expenses = GetExpensesFromFiles(setupDirPath, tenantNo)
                    .Select(e =>
                    {
                        e.TenantNo = tenantNo;
                        if (e.ReceiptItems != null)
                        {
                            foreach (var r in e.ReceiptItems)
                            {
                                r.TenantNo = tenantNo;
                                if (r.ProductDetails != null)
                                {
                                    foreach (var pd in r.ProductDetails)
                                        pd.TenantNo = tenantNo;
                                }
                            }
                        }
                        return e;
                    })
                    .ToList();

                Console.WriteLine($"[Seed:{tenantNo}] ✅ Masraf sayısı: {expenses.Count}");
                if (expenses.Count > 0)
                {
                    await context.Expenses.AddRangeAsync(expenses);
                    try
                    {
                        await context.SaveChangesAsync();
                        Console.WriteLine($"[Seed:{tenantNo}] ✅ Masraflar kaydedildi.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ ({tenantNo}) Masraflar kaydedilirken hata: {ex.Message}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"[Seed:{tenantNo}] ℹ️ Masraflar zaten mevcut, atlandı.");
            }

            Console.WriteLine($"🏁 [Seeder] Tenant {tenantNo} tamamlandı.");
        }

        // ---------------- CSV PARSERS (Aynen bırakıldı) ----------------

        private IEnumerable<Personnel> GetPersonnelsFromCsv(string path, string tenantNo)
        {
            var filePath = Path.Combine(path, "Personnels.txt");
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[HATA] CSV dosyası bulunamadı: {filePath}");
                yield break;
            }

            const int RequiredCols = 15; // 0..14
            var allLines = File.ReadAllLines(filePath, new UTF8Encoding(false));
            if (allLines.Length == 0) yield break;

            int lineNumber = 1;
            foreach (var rawLine in allLines)
            {
                if (lineNumber == 1 || rawLine.StartsWith("AD SOYAD", StringComparison.OrdinalIgnoreCase))
                { lineNumber++; continue; }

                if (string.IsNullOrWhiteSpace(rawLine))
                {
                    Console.WriteLine($"[Uyarı] {lineNumber}. satır boş, atlanıyor.");
                    lineNumber++;
                    continue;
                }

                Personnel? person = null;
                try
                {
                    char delimiter = rawLine.Count(c => c == ';') >= rawLine.Count(c => c == ',') ? ';' : ',';
                    var fields = rawLine.Split(delimiter).Select(f => (f ?? "").Trim().Trim('"')).ToList();

                    while (fields.Count < RequiredCols) fields.Add(string.Empty);
                    if (fields.Count > RequiredCols)
                    {
                        Console.WriteLine($"[Uyarı] {lineNumber}. satırda fazla sütun var. İlk {RequiredCols} kolon alınacak (toplam {fields.Count}).");
                        fields = fields.Take(RequiredCols).ToList();
                    }

                    string F(int i) => i < fields.Count ? fields[i] ?? "" : "";

                    person = new Personnel
                    {
                        FullName = $"{F(0)}_{tenantNo}",
                        NormalExpenseNumber = F(1),
                        SalaryExpenseNumber = F(2),
                        CaseExpenseNumber = F(3),
                        Company = F(4),
                        Department = F(5),
                        Unit = F(6),
                        NationalId = F(7),
                        FirstName = F(8),
                        LastName = F(9),
                        Title = F(10),
                        PhoneNumber = F(11),
                        ExpenseCenter = F(12),
                        Email = F(13),
                        IBAN = F(14)
                    };
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Uyarı] {lineNumber}. satır işlenemedi: {ex.Message}");
                }

                if (person != null)
                    yield return person;

                lineNumber++;
            }
        }

        private IEnumerable<AccountingCode> GetAccountingCodesFromCsv(string path, string tenantNo)
        {
            var filePath = Path.Combine(path, "AccountingCodes.txt");
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[HATA] CSV dosyası bulunamadı: {filePath}");
                yield break;
            }

            var allLines = File.ReadAllLines(filePath, new UTF8Encoding(false));
            if (allLines.Length == 0) yield break;

            var header = allLines[0];
            char delimiter = header.Contains(';') ? ';' : ',';

            for (int i = 1; i < allLines.Length; i++)
            {
                var raw = allLines[i];
                if (string.IsNullOrWhiteSpace(raw)) continue;

                var line = raw.Replace('\u00A0', ' ').TrimStart();

                int sep = line.IndexOf(delimiter);
                if (sep < 0)
                {
                    Console.WriteLine($"[ACC CSV] {i + 1}. satırda ayraç ({delimiter}) yok, atlanıyor. Satır: {line}");
                    continue;
                }

                var code = line.Substring(0, sep).Trim().Trim('"');
                var desc = line.Substring(sep + 1).Trim().Trim('"');

                if (string.IsNullOrWhiteSpace(code))
                {
                    Console.WriteLine($"[ACC CSV] {i + 1}. satırda kod boş, atlanıyor.");
                    continue;
                }

                yield return new AccountingCode
                {
                    Code = code,
                    Description = desc
                };
            }
        }

        private IEnumerable<Expense> GetExpensesFromFiles(string contentPath, string tenantNo)
        {
            var expenses = new List<Expense>();
            var expenseLines = File.ReadAllLines(Path.Combine(contentPath, "Expenses.txt")).Skip(1);
            var receiptLines = File.ReadAllLines(Path.Combine(contentPath, "ReceiptItems.txt")).Skip(1).Select(l => l.Split(','));
            var productLines = File.ReadAllLines(Path.Combine(contentPath, "ProductDetails.txt")).Skip(1).Select(l => l.Split(','));

            foreach (var line in expenseLines)
            {
                var fields = line.Split(',');
                var expenseCode = fields[0].Trim();

                var expense = new Expense
                {
                    ExpenseCode = expenseCode,
                    PersonnelFullName = $"{fields[1].Trim()}_{tenantNo}",
                    PersonnelAccountingCode = fields[2].Trim(),
                    ExpenseDate = DateTime.Parse(fields[3]),
                    ProjectCode = fields[4].Trim(),
                    CreatedDate = DateTime.Parse(fields[5]),
                    CreatedTime = TimeSpan.Parse(fields[6]),
                    ApprovedBy = fields[7].Trim(),
                    ApprovedAt = DateTime.TryParse(fields[8], out var approved) ? approved : null,
                    TotalAmount = decimal.Parse(fields[9]),
                    TotalVat = decimal.Parse(fields[10]),
                    Description = fields[11],
                    ReceiptItems = new List<ReceiptItem>()
                };

                var relatedReceipts = receiptLines.Where(r => r[0].Trim() == expenseCode);
                foreach (var r in relatedReceipts)
                {
                    var receipt = new ReceiptItem
                    {
                        ExpenseCode = expenseCode,
                        Type = r[1].Trim(),
                        AccountingCode = r[2].Trim(),
                        AccountingCodeDescription = r[3].Trim(),
                        Description = $"{r[4].Trim()}_{tenantNo}",
                        Quantity = 1,
                        Unit = "Adet",
                        TotalAmount = decimal.Parse(r[5]),
                        TotalVat = decimal.Parse(r[6]),
                        ReceiptNumber = r[7].Trim(),
                        ReceiptDate = DateTime.Parse(r[8]),
                        ProductDetails = new List<ProductDetail>()
                    };

                    var relatedProducts = productLines.Where(p => p[0].Trim() == r[7].Trim());
                    int rank = 1;
                    foreach (var p in relatedProducts)
                    {
                        var product = new ProductDetail
                        {
                            Rank = rank++,
                            TaxBase = decimal.Parse(p[1]),
                            VatRate = decimal.Parse(p[2]),
                            VatAmount = decimal.Parse(p[3]),
                            TotalAmount = decimal.Parse(p[4])
                        };
                        receipt.ProductDetails.Add(product);
                    }

                    expense.ReceiptItems.Add(receipt);
                }

                expenses.Add(expense);
            }

            return expenses;
        }
    }
}
