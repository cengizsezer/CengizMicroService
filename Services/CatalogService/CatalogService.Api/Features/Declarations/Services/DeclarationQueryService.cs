using CatalogService.Api.Features.Declarations.Dtos;
using CatalogService.Api.Features.Declarations.Entities;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Declarations.Services
{
    /// <summary>
    /// Beyanname Takip sorguları. Firma = Firmalarım (<see cref="Declaration.FirmaId"/>); eski CustomerCompanyId
    /// okunmaz. Pasif firmanın kayıtları varsayılan gizli (<c>pasifleriGoster</c> ile görünür); FirmaId'si boş kayıtlar
    /// her zaman "Eşleşmeyen firma" grubunda, kendi CompanyName'leriyle.
    /// </summary>
    public class DeclarationQueryService : IDeclarationQueryService
    {
        private readonly CatalogContext _context;

        public DeclarationQueryService(CatalogContext context)
        {
            _context = context;
        }

        public async Task<List<CompanyMonthlySummaryDto>> GetMonthlySummaryAsync(
            int year,
            int month,
            int? firmaId = null,
            string? declarationType = null,
            bool pasifleriGoster = false)
        {
            var query = _context.Declarations
                .AsNoTracking()
                .Where(x => x.Year == year && x.Month == month);

            if (firmaId.HasValue)
            {
                query = query.Where(x => x.FirmaId == firmaId.Value);
            }

            if (!string.IsNullOrWhiteSpace(declarationType))
            {
                declarationType = declarationType.Trim();
                query = query.Where(x => x.DeclarationType == declarationType);
            }

            var declarations = await query
                .OrderBy(x => x.DueDate)
                .ToListAsync();

            var firmalar = (await BeyannameFirmasi.HepsiAsync(_context)).ToDictionary(f => f.Id);

            return Gorunur(declarations, firmalar, pasifleriGoster)
                .GroupBy(x => x.FirmaId)
                .Select(g =>
                {
                    var firma = g.Key is int id && firmalar.TryGetValue(id, out var f) ? f : null;
                    return new CompanyMonthlySummaryDto
                    {
                        FirmaId = g.Key,
                        FirmaPasif = firma is { Aktif: false },
                        Eslesmeyen = g.Key is null,
                        TenantNo = g.First().TenantNo,
                        CompanyName = firma?.Ad ?? BeyannameFirmasi.EslesmeyenAdi,
                        Year = year,
                        Month = month,
                        DeclarationCount = g.Count(),
                        ApprovedCount = g.Count(x => x.DeclarationStatus == DeclarationStatus.Approved),
                        PaidCount = g.Count(x => x.PaymentStatus == PaymentStatus.Paid),
                        TotalAmount = g.Sum(x => x.Amount),
                        PaidAmount = g.Where(x => x.PaymentStatus == PaymentStatus.Paid).Sum(x => x.Amount),
                        PendingAmount = g.Where(x => x.PaymentStatus != PaymentStatus.Paid).Sum(x => x.Amount),
                        Declarations = g.Select(Dto).OrderBy(x => x.DueDate).ToList()
                    };
                })
                // Eşleşmeyen grup en sonda.
                .OrderBy(x => x.Eslesmeyen)
                .ThenBy(x => x.CompanyName, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), true))
                .ToList();
        }

        public async Task<YearlyTaxSummaryDto> GetYearlySummaryAsync(int year, int? firmaId = null, bool pasifleriGoster = false)
        {
            var query = _context.Declarations
                .AsNoTracking()
                .Where(x => x.Year == year);

            if (firmaId.HasValue)
            {
                query = query.Where(x => x.FirmaId == firmaId.Value);
            }

            var firmalar = (await BeyannameFirmasi.HepsiAsync(_context)).ToDictionary(f => f.Id);
            var declarations = Gorunur(await query.ToListAsync(), firmalar, pasifleriGoster).ToList();

            return new YearlyTaxSummaryDto
            {
                Year = year,
                TotalAmount = declarations.Sum(x => x.Amount),
                PaidAmount = declarations.Where(x => x.PaymentStatus == PaymentStatus.Paid).Sum(x => x.Amount),
                PendingAmount = declarations.Where(x => x.PaymentStatus != PaymentStatus.Paid).Sum(x => x.Amount),
                TotalCompanyCount = declarations
                    .Select(x => x.FirmaId)
                    .Distinct()
                    .Count(),
                TotalDeclarationCount = declarations.Count
            };
        }

        public async Task<List<CompanyYearlySummaryDto>> GetCompanyYearlySummaryAsync(int year, bool pasifleriGoster = false)
        {
            var firmalar = (await BeyannameFirmasi.HepsiAsync(_context)).ToDictionary(f => f.Id);
            var declarations = Gorunur(
                await _context.Declarations.AsNoTracking().Where(x => x.Year == year).ToListAsync(),
                firmalar, pasifleriGoster);

            return declarations
                .GroupBy(x => x.FirmaId)
                .Select(g => new CompanyYearlySummaryDto
                {
                    FirmaId = g.Key,
                    TenantNo = g.First().TenantNo,
                    CompanyName = g.Key is int id && firmalar.TryGetValue(id, out var f) ? f.Ad : BeyannameFirmasi.EslesmeyenAdi,
                    Year = year,
                    DeclarationCount = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount),
                    PaidAmount = g.Where(x => x.PaymentStatus == PaymentStatus.Paid).Sum(x => x.Amount),
                    PendingAmount = g.Where(x => x.PaymentStatus != PaymentStatus.Paid).Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.TotalAmount)
                .ToList();
        }

        /// <summary>
        /// Görünürlük kuralı (Takip ve Özet aynı): pasif firmanın kaydı yalnız <paramref name="pasifleriGoster"/> ile;
        /// FirmaId'si boş kayıt her zaman (Eşleşmeyen firma). Firmalarım'da artık olmayan bir FirmaId (FK Restrict ile
        /// olamaz) eşleşmeyen sayılmaz — gizlenir.
        /// </summary>
        public static IEnumerable<Declaration> Gorunur(IEnumerable<Declaration> kayitlar,
                                                        IReadOnlyDictionary<int, BeyannameFirmasi> firmalar,
                                                        bool pasifleriGoster)
            => kayitlar.Where(d => d.FirmaId is not int id
                                   || (firmalar.TryGetValue(id, out var f) && (f.Aktif || pasifleriGoster)));

        private static DeclarationDto Dto(Declaration x) => new()
        {
            Id = x.Id,
            CustomerCompanyId = x.CustomerCompanyId,
            FirmaId = x.FirmaId,
            TenantNo = x.TenantNo,
            CompanyName = x.CompanyName,
            DeclarationType = x.DeclarationType,
            Year = x.Year,
            Month = x.Month,
            Amount = x.Amount,
            DueDate = x.DueDate,
            DeclarationStatus = x.DeclarationStatus,
            PaymentStatus = x.PaymentStatus,
            PaymentDate = x.PaymentDate,
            Note = x.Note
        };
    }
}
