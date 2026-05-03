using CatalogService.Api.Features.Mukellefler.Domain;
using CatalogService.Api.Features.Mukellefler.Dtos;
using CatalogService.Api.Infrastructure.Context;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Mukellefler.Services
{
    public class MukellefImportService : IMukellefImportService
    {
        private const int HeaderScanRowLimit = 20;

        private static readonly Dictionary<string, string[]> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(Mukellef.SozlesmeNo)]     = new[] { "sözleşme no", "sozlesme no", "sözleşmeno" },
            [nameof(Mukellef.SozlesmeTarihi)] = new[] { "sözleşme tarihi", "sozlesme tarihi", "sözleşmetarihi" },
            [nameof(Mukellef.Unvan)]          = new[] { "müşteri ünvanı", "musteri unvani", "ünvan", "unvan", "ünvanı" },
            [nameof(Mukellef.VergiKimlikNo)]  = new[] { "müşteri vergi no", "musteri vergi no", "vergi no", "vergi kimlik no", "vkn", "tckn" },
            [nameof(Mukellef.Durum)]          = new[] { "durumu", "durum", "sözleşme durumu" },
            [nameof(Mukellef.IptalFeshTarihi)]= new[] { "i̇ptal-fesih tarihi", "iptal-fesih tarihi", "iptal fesih tarihi", "i̇ptal/fesih tarihi", "iptal/fesih tarihi", "iptal tarihi", "fesih tarihi" },
            [nameof(Mukellef.Hizmet)]         = new[] { "hizmet", "hizmet türü" },
            [nameof(Mukellef.Telefon)]        = new[] { "phone number", "telefon", "telefon no", "tel" },
            [nameof(Mukellef.Email)]          = new[] { "email", "e-posta", "e posta", "eposta", "mail" }
        };

        private readonly CatalogContext _context;

        public MukellefImportService(CatalogContext context)
        {
            _context = context;
        }

        public async Task<MukellefImportResultDto> ImportAsync(
            Stream xlsxStream,
            MukellefImportMode mode,
            CancellationToken ct = default)
        {
            var result = new MukellefImportResultDto();

            using var workbook = new XLWorkbook(xlsxStream);
            var sheet = workbook.Worksheets.FirstOrDefault()
                        ?? throw new InvalidDataException("Excel dosyasında sayfa bulunamadı.");

            var (headerRow, columnMap) = DetectHeaderRow(sheet);
            if (headerRow < 0)
                throw new InvalidDataException("Sözleşme No header satırı bulunamadı (ilk 20 satır tarandı).");

            EnsureRequiredColumns(columnMap);

            var firstDataRow = headerRow + 1;
            var lastUsedRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow;

            var parsed = new List<(int ExcelRow, Mukellef Entity)>();

            for (int r = firstDataRow; r <= lastUsedRow; r++)
            {
                ct.ThrowIfCancellationRequested();
                var row = sheet.Row(r);

                if (IsRowEmpty(row, columnMap))
                    continue;

                result.TotalRows++;

                var rowResult = ParseRow(row, r, columnMap);
                if (rowResult.Errors.Count > 0)
                {
                    result.Errors.AddRange(rowResult.Errors);
                    continue;
                }

                parsed.Add((r, rowResult.Entity!));
            }

            if (parsed.Count == 0)
                return result;

            DetectIntraFileDuplicates(parsed, result);

            var sozlesmeNos = parsed.Select(p => p.Entity.SozlesmeNo).Distinct().ToList();
            var existing = await _context.Mukellefler
                .Where(m => sozlesmeNos.Contains(m.SozlesmeNo))
                .ToDictionaryAsync(m => m.SozlesmeNo, ct);

            var now = DateTime.UtcNow;

            foreach (var (excelRow, entity) in parsed)
            {
                if (existing.TryGetValue(entity.SozlesmeNo, out var existingEntity))
                {
                    if (mode == MukellefImportMode.SkipExisting)
                    {
                        result.Skipped++;
                        continue;
                    }

                    existingEntity.SozlesmeTarihi = entity.SozlesmeTarihi;
                    existingEntity.Unvan = entity.Unvan;
                    existingEntity.VergiKimlikNo = entity.VergiKimlikNo;
                    existingEntity.Durum = entity.Durum;
                    existingEntity.IptalFeshTarihi = entity.IptalFeshTarihi;
                    existingEntity.Hizmet = entity.Hizmet;
                    existingEntity.Telefon = entity.Telefon;
                    existingEntity.Email = entity.Email;
                    existingEntity.UpdatedAt = now;
                    result.Updated++;
                }
                else
                {
                    entity.CreatedAt = now;
                    _context.Mukellefler.Add(entity);
                    existing[entity.SozlesmeNo] = entity;
                    result.Inserted++;
                }
            }

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                result.Errors.Add(new MukellefImportError
                {
                    Row = 0,
                    Field = null,
                    Message = $"DB kayıt hatası: {ex.InnerException?.Message ?? ex.Message}"
                });
                result.Inserted = 0;
                result.Updated = 0;
            }

            return result;
        }

        private static (int headerRow, Dictionary<string, int> columnMap) DetectHeaderRow(IXLWorksheet sheet)
        {
            var lastUsed = sheet.LastRowUsed()?.RowNumber() ?? 0;
            var scanLimit = Math.Min(HeaderScanRowLimit, lastUsed);

            for (int r = 1; r <= scanLimit; r++)
            {
                var row = sheet.Row(r);
                var bText = (row.Cell(2).GetString() ?? string.Empty).Trim();
                var cText = (row.Cell(3).GetString() ?? string.Empty).Trim();

                if (ContainsCi(bText, "sözleşme no") || ContainsCi(cText, "sözleşme no") ||
                    ContainsCi(bText, "sozlesme no") || ContainsCi(cText, "sozlesme no"))
                {
                    return (r, BuildColumnMap(row));
                }
            }

            return (-1, new Dictionary<string, int>());
        }

        private static Dictionary<string, int> BuildColumnMap(IXLRow headerRow)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var lastCol = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;

            for (int c = 1; c <= lastCol; c++)
            {
                var cell = headerRow.Cell(c).GetString()?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(cell)) continue;

                foreach (var (field, aliases) in HeaderAliases)
                {
                    if (map.ContainsKey(field)) continue;
                    if (aliases.Any(a => string.Equals(cell, a, StringComparison.OrdinalIgnoreCase)) ||
                        aliases.Any(a => cell.Contains(a, StringComparison.OrdinalIgnoreCase)))
                    {
                        map[field] = c;
                        break;
                    }
                }
            }

            return map;
        }

        private static void EnsureRequiredColumns(Dictionary<string, int> map)
        {
            string[] required =
            {
                nameof(Mukellef.SozlesmeNo),
                nameof(Mukellef.SozlesmeTarihi),
                nameof(Mukellef.Unvan),
                nameof(Mukellef.VergiKimlikNo)
            };

            var missing = required.Where(r => !map.ContainsKey(r)).ToList();
            if (missing.Count > 0)
                throw new InvalidDataException($"Eksik kolon(lar): {string.Join(", ", missing)}");
        }

        private static bool IsRowEmpty(IXLRow row, Dictionary<string, int> map)
        {
            foreach (var col in map.Values)
            {
                var v = row.Cell(col).GetString()?.Trim();
                if (!string.IsNullOrEmpty(v)) return false;
            }
            return true;
        }

        private record RowParseOutcome(Mukellef? Entity, List<MukellefImportError> Errors);

        private static RowParseOutcome ParseRow(IXLRow row, int excelRow, Dictionary<string, int> map)
        {
            var errors = new List<MukellefImportError>();

            string GetStr(string field) =>
                map.TryGetValue(field, out var col) ? (row.Cell(col).GetString() ?? string.Empty).Trim() : string.Empty;

            DateTime? GetDate(string field)
            {
                if (!map.TryGetValue(field, out var col)) return null;
                var cell = row.Cell(col);
                if (cell.IsEmpty()) return null;

                if (cell.TryGetValue<DateTime>(out var dt)) return dt;

                var raw = cell.GetString().Trim();
                if (string.IsNullOrEmpty(raw)) return null;
                if (DateTime.TryParse(raw, out var parsed)) return parsed;

                errors.Add(new MukellefImportError { Row = excelRow, Field = field, Message = $"Tarih parse edilemedi: '{raw}'" });
                return null;
            }

            var sozlesmeNo = GetStr(nameof(Mukellef.SozlesmeNo));
            var unvan = GetStr(nameof(Mukellef.Unvan));
            var vkn = GetStr(nameof(Mukellef.VergiKimlikNo));
            var sozlesmeTarihi = GetDate(nameof(Mukellef.SozlesmeTarihi));

            if (string.IsNullOrWhiteSpace(sozlesmeNo))
                errors.Add(new MukellefImportError { Row = excelRow, Field = nameof(Mukellef.SozlesmeNo), Message = "SozlesmeNo boş." });
            if (string.IsNullOrWhiteSpace(unvan))
                errors.Add(new MukellefImportError { Row = excelRow, Field = nameof(Mukellef.Unvan), Message = "Unvan boş." });
            if (string.IsNullOrWhiteSpace(vkn))
                errors.Add(new MukellefImportError { Row = excelRow, Field = nameof(Mukellef.VergiKimlikNo), Message = "VergiKimlikNo boş." });
            else if (vkn.Length < 10 || vkn.Length > 11)
                errors.Add(new MukellefImportError { Row = excelRow, Field = nameof(Mukellef.VergiKimlikNo), Message = $"VergiKimlikNo 10-11 karakter olmalı (gelen: {vkn.Length})." });
            if (sozlesmeTarihi is null)
                errors.Add(new MukellefImportError { Row = excelRow, Field = nameof(Mukellef.SozlesmeTarihi), Message = "SozlesmeTarihi boş veya geçersiz." });

            if (errors.Count > 0)
                return new RowParseOutcome(null, errors);

            var durum = ParseDurum(GetStr(nameof(Mukellef.Durum)));
            var iptalTarihi = GetDate(nameof(Mukellef.IptalFeshTarihi));
            var hizmet = GetStr(nameof(Mukellef.Hizmet));
            var telefon = GetStr(nameof(Mukellef.Telefon));
            var emailRaw = GetStr(nameof(Mukellef.Email));
            var email = string.IsNullOrWhiteSpace(emailRaw) ? null : emailRaw;

            var entity = new Mukellef
            {
                SozlesmeNo = sozlesmeNo,
                SozlesmeTarihi = sozlesmeTarihi!.Value,
                Unvan = unvan,
                VergiKimlikNo = vkn,
                Durum = durum,
                IptalFeshTarihi = iptalTarihi,
                Hizmet = hizmet,
                Telefon = telefon,
                Email = email
            };

            return new RowParseOutcome(entity, errors);
        }

        private static MukellefDurumu ParseDurum(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return MukellefDurumu.DevamEdiyor;
            var s = raw.Replace(" ", string.Empty).Replace("İ", "I").ToLowerInvariant();
            if (s.Contains("devam")) return MukellefDurumu.DevamEdiyor;
            if (s.Contains("fesh") || s.Contains("fesih")) return MukellefDurumu.FeshOldu;
            if (s.Contains("iptal")) return MukellefDurumu.IptalEdildi;
            return MukellefDurumu.DevamEdiyor;
        }

        private static bool ContainsCi(string haystack, string needle) =>
            haystack?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false;

        private static void DetectIntraFileDuplicates(
            List<(int ExcelRow, Mukellef Entity)> parsed,
            MukellefImportResultDto result)
        {
            var sozGroups = parsed
                .GroupBy(p => p.Entity.SozlesmeNo)
                .Where(g => g.Count() > 1);

            foreach (var g in sozGroups)
            {
                foreach (var p in g.Skip(1))
                {
                    result.Errors.Add(new MukellefImportError
                    {
                        Row = p.ExcelRow,
                        Field = nameof(Mukellef.SozlesmeNo),
                        Message = $"Aynı dosyada tekrarlanan SozlesmeNo: {g.Key}"
                    });
                }
            }

            var dupRows = sozGroups.SelectMany(g => g.Skip(1).Select(p => p.ExcelRow)).ToHashSet();
            parsed.RemoveAll(p => dupRows.Contains(p.ExcelRow));

            var vknGroups = parsed
                .GroupBy(p => p.Entity.VergiKimlikNo)
                .Where(g => g.Count() > 1);

            foreach (var g in vknGroups)
            {
                foreach (var p in g.Skip(1))
                {
                    result.Errors.Add(new MukellefImportError
                    {
                        Row = p.ExcelRow,
                        Field = nameof(Mukellef.VergiKimlikNo),
                        Message = $"Aynı dosyada tekrarlanan VergiKimlikNo: {g.Key}"
                    });
                }
            }

            var dupVknRows = vknGroups.SelectMany(g => g.Skip(1).Select(p => p.ExcelRow)).ToHashSet();
            parsed.RemoveAll(p => dupVknRows.Contains(p.ExcelRow));
        }
    }
}
