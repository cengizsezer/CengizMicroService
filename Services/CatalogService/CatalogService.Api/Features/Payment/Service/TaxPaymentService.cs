using System;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using CatalogService.Api.Infrastructure.Context;
using EFCore.BulkExtensions;
using CatalogService.Api.Features.Payment.DTO;
using CatalogService.Api.Features.Payment.Entities;

namespace CatalogService.Api.Features.TaxPayments.Service
{
    public class TaxPaymentService
    {
        private readonly CatalogContext dBContext;

        public TaxPaymentService(CatalogContext dBContext)
        {
            this.dBContext = dBContext;
        }

        public async Task<List<TaxPaymentEntityDto>> GetAllTaxPayments()
        {
            return await dBContext.TaxPayments
                .OrderBy(x => x.TaxpayerName)
                .ThenBy(x => x.TahakkukNo)
                .Select(x => new TaxPaymentEntityDto
                {
                    Id = x.Id,
                    TahakkukNo = x.TahakkukNo,
                    TaxNumber = x.TaxNumber,
                    Amount = x.Amount,
                    TaxpayerName = x.TaxpayerName,
                    TaxType = x.TaxType,
                    CreatedBy = x.CreatedBy,
                    Description = x.Description
                })
                .ToListAsync();
        }

        public bool CreateNewTaxPayment(TaxPaymentEntityDto model)
        {
            try
            {
                var entity = new TaxPaymentEntity
                {
                    TahakkukNo = model.TahakkukNo?.Trim() ?? string.Empty,
                    TaxNumber = model.TaxNumber?.Trim() ?? string.Empty,
                    Amount = model.Amount,
                    TaxpayerName = model.TaxpayerName?.Trim() ?? string.Empty,
                    TaxType = model.TaxType?.Trim() ?? string.Empty,
                    CreatedBy = model.CreatedBy?.Trim() ?? string.Empty,
                    Description = model.Description?.Trim() ?? string.Empty
                };

                dBContext.TaxPayments.Add(entity);
                var result = dBContext.SaveChanges();
                return result > 0;
            }
            catch
            {
                return false;
            }
        }

        public TaxPaymentEntityDto? FindTaxPayment(int id)
        {
            var entity = dBContext.TaxPayments.Find(id);
            if (entity == null) return null;

            return new TaxPaymentEntityDto
            {
                Id = entity.Id,
                TahakkukNo = entity.TahakkukNo,
                TaxNumber = entity.TaxNumber,
                Amount = entity.Amount,
                TaxpayerName = entity.TaxpayerName,
                TaxType = entity.TaxType,
                CreatedBy = entity.CreatedBy,
                Description = entity.Description
            };
        }

        public bool UpdateTaxPayment(TaxPaymentEntityDto model)
        {
            try
            {
                var entity = dBContext.TaxPayments.Find(model.Id);
                if (entity == null) return false;

                entity.TahakkukNo = model.TahakkukNo?.Trim() ?? string.Empty;
                entity.TaxNumber = model.TaxNumber?.Trim() ?? string.Empty;
                entity.Amount = model.Amount;
                entity.TaxpayerName = model.TaxpayerName?.Trim() ?? string.Empty;
                entity.TaxType = model.TaxType?.Trim() ?? string.Empty;
                entity.CreatedBy = model.CreatedBy?.Trim() ?? string.Empty;
                entity.Description = model.Description?.Trim() ?? string.Empty;

                var result = dBContext.SaveChanges();
                return result > 0;
            }
            catch
            {
                return false;
            }
        }

        public bool DeleteTaxPayment(int id)
        {
            try
            {
                var entity = dBContext.TaxPayments.Find(id);
                if (entity == null) return false;

                dBContext.TaxPayments.Remove(entity);
                var result = dBContext.SaveChanges();
                return result > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteAllAsync(bool resetIdentity = false)
        {
            try
            {
                await dBContext.TaxPayments.ExecuteDeleteAsync();

                if (resetIdentity)
                {
                    await dBContext.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('[pkf].[TaxPayments]', RESEED, 0);");
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ImportTaxPayments(List<TaxPaymentEntityDto> items)
        {
            try
            {
                foreach (var x in items)
                {
                    x.TahakkukNo = (x.TahakkukNo ?? "").Trim();
                    x.TaxNumber = (x.TaxNumber ?? "").Trim();
                    x.TaxType = (x.TaxType ?? "").Trim();
                    x.TaxpayerName = (x.TaxpayerName ?? "").Trim();
                    x.CreatedBy = (x.CreatedBy ?? "").Trim();
                    x.Description = (x.Description ?? "").Trim();
                }

                var tahakkukNos = items
                    .Select(x => x.TahakkukNo)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Distinct()
                    .ToList();

                var existing = await dBContext.TaxPayments
                    .Where(x => tahakkukNos.Contains(x.TahakkukNo))
                    .ToListAsync();

                var toInsertOrUpdate = items.Select(x =>
                {
                    var e = existing.FirstOrDefault(y =>
                        y.TahakkukNo == x.TahakkukNo &&
                        y.TaxNumber == x.TaxNumber &&
                        y.TaxType == x.TaxType);

                    if (e == null)
                    {
                        e = new TaxPaymentEntity
                        {
                            TahakkukNo = x.TahakkukNo,
                            TaxNumber = x.TaxNumber,
                            TaxType = x.TaxType
                        };
                    }

                    e.Amount = x.Amount;
                    e.TaxpayerName = x.TaxpayerName;
                    e.CreatedBy = x.CreatedBy;
                    e.Description = x.Description;

                    return e;
                }).ToList();

                var bulkOptions = new BulkConfig
                {
                    UpdateByProperties = new List<string> { "TahakkukNo", "TaxNumber", "TaxType" }
                };

                await dBContext.BulkInsertOrUpdateAsync(toInsertOrUpdate, bulkOptions);
                return true;
            }
            catch
            {
                return false;
            }
        }


        public List<TaxPaymentEntityDto> ParseExcel(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using var wb = new XLWorkbook(stream);

            var ws = wb.Worksheet(1);
            var rows = ws.RangeUsed().RowsUsed().Skip(1);

            var list = new List<TaxPaymentEntityDto>();

            foreach (var row in rows)
            {
                decimal ParseDecimal(string val)
                {
                    if (string.IsNullOrWhiteSpace(val)) return 0;
                    val = val.Replace(".", "").Replace(",", ".");
                    return decimal.TryParse(val, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
                }

                var dto = new TaxPaymentEntityDto
                {
                    TahakkukNo = row.Cell(2).GetString(),
                    TaxNumber = row.Cell(3).GetString(),
                    Amount = ParseDecimal(row.Cell(4).GetString()),
                    TaxpayerName = row.Cell(5).GetString(),
                    TaxType = row.Cell(6).GetString(),
                    CreatedBy = row.Cell(7).GetString(),
                    Description = row.Cell(8).GetString()
                };

                if (!string.IsNullOrWhiteSpace(dto.TahakkukNo))
                    list.Add(dto);
            }

            return list;
        }

        public async Task<byte[]> ExportToExcel()
        {
            var datas = await GetAllTaxPayments();

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("TaxPayments");

                ws.Cell(1, 1).Value = "ID";
                ws.Cell(1, 2).Value = "TAHAKKUK NO";
                ws.Cell(1, 3).Value = "VERGI NUMARASI";
                ws.Cell(1, 4).Value = "TUTAR";
                ws.Cell(1, 5).Value = "MUKELLEF";
                ws.Cell(1, 6).Value = "VERGI TURU";
                ws.Cell(1, 7).Value = "GIREN KISI";
                ws.Cell(1, 8).Value = "ACIKLAMA";

                for (int i = 0; i < datas.Count; i++)
                {
                    var r = i + 2;
                    ws.Cell(r, 1).Value = datas[i].Id;
                    ws.Cell(r, 2).Value = datas[i].TahakkukNo;
                    ws.Cell(r, 3).Value = datas[i].TaxNumber;
                    ws.Cell(r, 4).Value = datas[i].Amount;
                    ws.Cell(r, 5).Value = datas[i].TaxpayerName;
                    ws.Cell(r, 6).Value = datas[i].TaxType;
                    ws.Cell(r, 7).Value = datas[i].CreatedBy;
                    ws.Cell(r, 8).Value = datas[i].Description;
                }

                ws.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
        }
    }
}