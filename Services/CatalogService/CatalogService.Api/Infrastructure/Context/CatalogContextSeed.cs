using CatalogService.Api.Core.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Polly;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatalogService.Api.Infrastructure.Context
{
    public class CatalogContextSeed
    {
        public async Task SeedAsync(CatalogContext context, IWebHostEnvironment env, ILogger<CatalogContextSeed> logger, bool force = false)
        {
            var policy = Policy.Handle<SqlException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retry => TimeSpan.FromSeconds(5),
                    onRetry: (exception, timeSpan, retry, ctx) =>
                    {
                        logger.LogWarning(exception, "[{prefix}] Exception {ExceptionType} with message {Message} detected on attempt {retry} of {retries}", nameof(logger), exception.GetType().Name, exception.Message, retry, 3);
                    }
                );

            var setupDirPath = Path.Combine(env.ContentRootPath, "Infrastructure", "Setup", "SeedFiles");

            await policy.ExecuteAsync(() => ProcessSeeding(context, setupDirPath, logger, force));
        }



        private async Task ProcessSeeding(CatalogContext context, string setupDirPath, ILogger logger, bool force)
        {
            Console.WriteLine("🚀 [Seeder] Seeding işlemi başladı...");
            Console.WriteLine($"📂 setupDirPath: {setupDirPath}");
            Console.WriteLine($"🧠 Database: {context.Database.GetDbConnection().ConnectionString}");

            if (force || !context.Personnels.Any())
            {
                var personnels = GetPersonnelsFromCsv(setupDirPath).ToList();
                Console.WriteLine($"[Seed] ✅ Personel sayısı: {personnels.Count}");

                await context.Personnels.AddRangeAsync(personnels);
                Console.WriteLine("[Seed] 💾 Personeller ekleniyor...");

                try
                {
                    await context.SaveChangesAsync();
                    logger.LogInformation("[Seed] ✅ Personeller başarıyla kaydedildi.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ Personelleri kaydederken hata oluştu!");
                }
            }
            else
            {
                Console.WriteLine("[Seed] ℹ️ Personeller zaten mevcut, atlandı.");
            }

            if (force || !context.AccountingCodes.Any())
            {
                var accountingCodes = GetAccountingCodesFromCsv(setupDirPath).ToList();
                Console.WriteLine($"[Seed] ✅ Muhasebe kodu sayısı: {accountingCodes.Count}");

                await context.AccountingCodes.AddRangeAsync(accountingCodes);
                Console.WriteLine("[Seed] 💾 Muhasebe kodları ekleniyor...");

                try
                {
                    await context.SaveChangesAsync();
                    Console.WriteLine("[Seed] ✅ Muhasebe kodları başarıyla kaydedildi.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ Muhasebe kodlarını kaydederken hata oluştu!");
                }
            }
            else
            {
                Console.WriteLine("[Seed] ℹ️ Muhasebe kodları zaten mevcut, atlandı.");
            }

            if (force || !context.Expenses.Any())
            {
                var expenses = GetExpensesFromFiles(setupDirPath).ToList();
                Console.WriteLine($"[Seed] ✅ Masraf kaydı sayısı: {expenses.Count}");

                await context.Expenses.AddRangeAsync(expenses);
                Console.WriteLine("[Seed] 💾 Masraflar ekleniyor...");

                try
                {
                    await context.SaveChangesAsync();
                    Console.WriteLine("[Seed] ✅ Masraflar başarıyla kaydedildi.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine( "❌ Masrafları kaydederken hata oluştu!");
                }
            }
            else
            {
                Console.WriteLine("[Seed] ℹ️ Masraflar zaten mevcut, atlandı.");
            }

            Console.WriteLine("🏁 [Seeder] Seeding işlemi tamamlandı.");
        }

        private IEnumerable<Personnel> GetPersonnelsFromCsv(string path)
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
                // Header'ı atla
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
                    // ; veya , seç
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
                        FullName = F(0),
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

                // yield'ı try-catch DIŞINDA çağır
                if (person != null)
                    yield return person;

                lineNumber++;
            }
        }


        //private IEnumerable<Personnel> GetPersonnelsFromCsv(string path)
        //{
        //    var filePath = Path.Combine(path, "Personnels.txt");

        //    if (!File.Exists(filePath))
        //    {
        //        Console.WriteLine($"[HATA] CSV dosyası bulunamadı: {filePath}");
        //        yield break;
        //    }

        //    var lines = File.ReadAllLines(filePath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)).Skip(1);
        //    int lineNumber = 2; // 1. satır header olduğu için

        //    foreach (var line in lines)
        //    {
        //        if (string.IsNullOrWhiteSpace(line))
        //        {
        //            Console.WriteLine($"[Uyarı] {lineNumber}. satır boş, atlanıyor.");
        //            lineNumber++;
        //            continue;
        //        }

        //        var fields = line.Split(';').Select(f => f.Trim()).ToList();

        //        // Eksik sütunları "-" ile tamamla
        //        while (fields.Count < 11)
        //            fields.Add("-");

        //        if (fields.Count > 15)
        //        {
        //            Console.WriteLine($"[Uyarı] {lineNumber}. satırda fazla sütun var. İlk 11 sütun alınacak.");
        //            fields = fields.Take(11).ToList();
        //        }

        //        yield return new Personnel
        //        {
        //            FullName = fields[0],
        //            NormalExpenseNumber = fields[1],
        //            SalaryExpenseNumber = fields[2],
        //            CaseExpenseNumber = fields[3],
        //            Company = fields[4],
        //            Department = fields[5],
        //            Unit = fields[6],
        //            NationalId = fields[7],
        //            FirstName = fields[8],
        //            LastName = fields[9],
        //            Title = fields[10],
        //            PhoneNumber = fields[11],
        //            ExpenseCenter = fields[12],
        //            Email = fields[13],
        //            IBAN = fields[14]
        //        };

        //        lineNumber++;
        //    }
        //}



        private IEnumerable<AccountingCode> GetAccountingCodesFromCsv(string path)
        {
            var filePath = Path.Combine(path, "AccountingCodes.txt");
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[HATA] CSV dosyası bulunamadı: {filePath}");
                yield break;
            }

            var allLines = File.ReadAllLines(filePath, new UTF8Encoding(false));
            if (allLines.Length == 0) yield break;

            // Header'ı okuyup ayraç tespiti
            var header = allLines[0];
            char delimiter = header.Contains(';') ? ';' : ',';

            for (int i = 1; i < allLines.Length; i++)
            {
                var raw = allLines[i];
                if (string.IsNullOrWhiteSpace(raw)) continue;

                // Başındaki boşlukları ve NBSP karakterlerini temizle
                var line = raw.Replace('\u00A0', ' ').TrimStart();

                // Ayracı ilk geçtiği yerden böl (açıklama içinde ayraç varsa bozulmasın)
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




        private IEnumerable<Expense> GetExpensesFromFiles(string contentPath)
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
                    PersonnelFullName = fields[1].Trim(),
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

                var relatedReceipts = receiptLines.Where(r => r[0] == expenseCode);
                foreach (var r in relatedReceipts)
                {
                    var receipt = new ReceiptItem
                    {
                        ExpenseCode = expenseCode,
                        Type = r[1].Trim(),
                        AccountingCode = r[2].Trim(),
                        AccountingCodeDescription = r[3].Trim(),
                        Description = r[4].Trim(),
                        Quantity = 1,
                        Unit = "Adet",
                        TotalAmount = decimal.Parse(r[5]),
                        TotalVat = decimal.Parse(r[6]),
                        ReceiptNumber = r[7].Trim(),
                        ReceiptDate = DateTime.Parse(r[8]),
                        ProductDetails = new List<ProductDetail>()
                    };

                    var relatedProducts = productLines.Where(p => p[0] == r[7].Trim());
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


