using System;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using CatalogService.Api.Infrastructure.Context;
using EFCore.BulkExtensions;
using CatalogService.Api.Features.Vehicles.DTO;
using CatalogService.Api.Features.Vehicles.Domain;

namespace CatalogService.Api.Features.Vehicles.Service
{
    public class VehicleService
    {
        private readonly CatalogContext dBContext;

        public VehicleService(CatalogContext dBContext)
        {
            this.dBContext = dBContext;
        }

        // LIST
        public async Task<List<VehicleDto>> GetAllVehicles()
        {
            return await dBContext.Vehicles
                .OrderBy(x => x.Plate)
                .Select(x => new VehicleDto
                {
                    Id = x.Id,
                    Plate = x.Plate,
                    Driver = x.Driver,
                    Unit = x.Unit,
                    Department = x.Department,
                    Description1 = x.Description1,
                    Region = x.Region,
                    Description2 = x.Description2,
                    Type = x.Type,
                    Brand = x.Brand,
                    Model = x.Model,
                    Gear = x.Gear,
                    Fuel = x.Fuel,
                    Fleet = x.Fleet
                })
                .ToListAsync();
        }

        // CREATE
        public bool CreateNewVehicle(VehicleDto model)
        {
            try
            {
                var entity = new Vehicle
                {
                    Plate = model.Plate?.Trim(),
                    Driver = model.Driver,
                    Unit = model.Unit,
                    Department = model.Department,
                    Description1 = model.Description1,
                    Region = model.Region,
                    Description2 = model.Description2,
                    Type = model.Type,
                    Brand = model.Brand,
                    Model = model.Model,
                    Gear = model.Gear,
                    Fuel = model.Fuel,
                    Fleet = model.Fleet
                };

                dBContext.Vehicles.Add(entity);
                var result = dBContext.SaveChanges();
                return result > 0;
            }
            catch
            {
                return false;
            }
        }

        // READ (FIND)
        public VehicleDto? FindVehicle(int id)
        {
            var entity = dBContext.Vehicles.Find(id);
            if (entity == null) return null;

            return new VehicleDto
            {
                Id = entity.Id,
                Plate = entity.Plate,
                Driver = entity.Driver,
                Unit = entity.Unit,
                Department = entity.Department,
                Description1 = entity.Description1,
                Region = entity.Region,
                Description2 = entity.Description2,
                Type = entity.Type,
                Brand = entity.Brand,
                Model = entity.Model,
                Gear = entity.Gear,
                Fuel = entity.Fuel,
                Fleet = entity.Fleet
            };
        }

        // UPDATE
        public bool UpdateVehicle(VehicleDto model)
        {
            try
            {
                var entity = dBContext.Vehicles.Find(model.Id);
                if (entity == null) return false;

                entity.Plate = model.Plate?.Trim();
                entity.Driver = model.Driver;
                entity.Unit = model.Unit;
                entity.Department = model.Department;
                entity.Description1 = model.Description1;
                entity.Region = model.Region;
                entity.Description2 = model.Description2;
                entity.Type = model.Type;
                entity.Brand = model.Brand;
                entity.Model = model.Model;
                entity.Gear = model.Gear;
                entity.Fuel = model.Fuel;
                entity.Fleet = model.Fleet;

                var result = dBContext.SaveChanges();
                return result > 0;
            }
            catch
            {
                return false;
            }
        }

        // DELETE
        public bool DeleteVehicle(int id)
        {
            try
            {
                var entity = dBContext.Vehicles.Find(id);
                if (entity == null) return false;

                dBContext.Vehicles.Remove(entity);
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
                // EF Core 7+ toplu silme (tek SQL)
                await dBContext.Vehicles.ExecuteDeleteAsync();

                if (resetIdentity)
                {
                    // Sadece SQL Server için: IDENTITY yeniden başlat (bir sonraki 1 olur)
                    await dBContext.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('Vehicles', RESEED, 0);");
                }
                return true;
            }
            catch
            {
                return false;
            }
        }


        // BULK IMPORT
        public async Task<bool> ImportVehicles(List<VehicleDto> vehicles)
        {
            try
            {
                // normalize plate (uppercase & trim)
                foreach (var v in vehicles)
                    v.Plate = (v.Plate ?? "").Trim().ToUpperInvariant();

                // plakayı unique kabul edelim
                var plates = vehicles.Select(v => v.Plate).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
                var existing = await dBContext.Vehicles
                    .Where(x => plates.Contains(x.Plate))
                    .ToListAsync();

                var toInsertOrUpdate = vehicles.Select(v =>
                {
                    var e = existing.FirstOrDefault(x => x.Plate == v.Plate);
                    if (e == null) e = new Vehicle { Plate = v.Plate };
                    e.Driver = v.Driver;
                    e.Unit = v.Unit;
                    e.Department = v.Department;
                    e.Description1 = v.Description1;
                    e.Region = v.Region;
                    e.Description2 = v.Description2;
                    e.Type = v.Type;
                    e.Brand = v.Brand;
                    e.Model = v.Model;
                    e.Gear = v.Gear;
                    e.Fuel = v.Fuel;
                    e.Fleet = v.Fleet;
                    return e;
                }).ToList();

                var bulkOptions = new BulkConfig
                {
                    UpdateByProperties = new List<string> { "Plate" } // plakaya göre upsert
                };

                await dBContext.BulkInsertOrUpdateAsync(toInsertOrUpdate, bulkOptions);
                return true;
            }
            catch
            {
                return false;
            }
        }


        // EXPORT to EXCEL
        public async Task<byte[]> ExportToExcel()
        {
            var datas = await GetAllVehicles();

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Vehicles");

                // Headers (Türkçe)
                ws.Cell(1, 1).Value = "ID";
                ws.Cell(1, 2).Value = "PLAKA";
                ws.Cell(1, 3).Value = "SÜRÜCÜ";
                ws.Cell(1, 4).Value = "BİRİM";
                ws.Cell(1, 5).Value = "BÖLÜM";
                ws.Cell(1, 6).Value = "AÇIKLAMA-1";
                ws.Cell(1, 7).Value = "BÖLGE";
                ws.Cell(1, 8).Value = "AÇIKLAMA-2";
                ws.Cell(1, 9).Value = "TİP";
                ws.Cell(1, 10).Value = "MARKA";
                ws.Cell(1, 11).Value = "MODEL";
                ws.Cell(1, 12).Value = "VİTES";
                ws.Cell(1, 13).Value = "YAKIT";
                ws.Cell(1, 14).Value = "FİLO";

                for (int i = 0; i < datas.Count; i++)
                {
                    var r = i + 2;
                    ws.Cell(r, 1).Value = datas[i].Id;
                    ws.Cell(r, 2).Value = datas[i].Plate;
                    ws.Cell(r, 3).Value = datas[i].Driver;
                    ws.Cell(r, 4).Value = datas[i].Unit;
                    ws.Cell(r, 5).Value = datas[i].Department;
                    ws.Cell(r, 6).Value = datas[i].Description1;
                    ws.Cell(r, 7).Value = datas[i].Region;
                    ws.Cell(r, 8).Value = datas[i].Description2;
                    ws.Cell(r, 9).Value = datas[i].Type;
                    ws.Cell(r, 10).Value = datas[i].Brand;
                    ws.Cell(r, 11).Value = datas[i].Model;
                    ws.Cell(r, 12).Value = datas[i].Gear;
                    ws.Cell(r, 13).Value = datas[i].Fuel;
                    ws.Cell(r, 14).Value = datas[i].Fleet;
                }

                ws.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
        }
    }
}
