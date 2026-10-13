using CatalogService.Api.Features.Declarations.Dtos;
using CatalogService.Api.Features.Declarations.Entities;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Declarations.Services
{
    /// <summary>
    /// Beyanname kayıt/düzenleme. Firma = Firmalarım (<see cref="Declaration.FirmaId"/>, zorunlu); CompanyName görüntü
    /// için firmanın adından doldurulur (istemcinin gönderdiğine güvenilmez). Eski CustomerCompanyId okunmaz: yeni
    /// kayıtta 0, düzenlemede olduğu gibi kalır.
    ///
    /// Yeni kayıt ve firma DEĞİŞTİRİLEN düzenleme yalnız aktif firmaya yapılır; pasif firmanın mevcut kaydı kendi
    /// firmasıyla düzenlenebilir.
    /// </summary>
    public class DeclarationCommandService : IDeclarationCommandService
    {
        private readonly CatalogContext _context;

        public DeclarationCommandService(CatalogContext context)
        {
            _context = context;
        }

        public async Task<int> CreateAsync(CreateDeclarationRequest request)
        {
            var firma = await FirmaAsync(request.FirmaId, aktifOlmali: true);

            var entity = new Declaration
            {
                TenantNo = request.TenantNo?.Trim() ?? string.Empty,
                CompanyName = firma.Ad,
                DeclarationType = request.DeclarationType?.Trim() ?? string.Empty,
                Year = request.Year,
                Month = request.Month,
                Amount = request.Amount,
                DueDate = request.DueDate,
                DeclarationStatus = request.DeclarationStatus,
                PaymentStatus = request.PaymentStatus,
                PaymentDate = request.PaymentDate,
                Note = request.Note,
                FirmaId = firma.Id,
                CustomerCompanyId = 0
            };

            _context.Declarations.Add(entity);
            await _context.SaveChangesAsync();

            return entity.Id;
        }

        public async Task UpdateAsync(int id, UpdateDeclarationRequest request)
        {
            var entity = await _context.Declarations.FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null)
                throw new Exception("Declaration not found");

            // Firma değişmiyorsa pasif firmanın kaydı da düzenlenebilir; değişiyorsa yeni firma aktif olmalı.
            var firma = await FirmaAsync(request.FirmaId, aktifOlmali: request.FirmaId != entity.FirmaId);

            entity.TenantNo = request.TenantNo?.Trim() ?? string.Empty;
            entity.CompanyName = firma.Ad;
            entity.DeclarationType = request.DeclarationType?.Trim() ?? string.Empty;
            entity.Year = request.Year;
            entity.Month = request.Month;
            entity.Amount = request.Amount;
            entity.DueDate = request.DueDate;
            entity.DeclarationStatus = request.DeclarationStatus;
            entity.PaymentStatus = request.PaymentStatus;
            entity.PaymentDate = request.PaymentDate;
            entity.Note = request.Note;
            entity.FirmaId = firma.Id;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _context.Declarations.FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null)
                throw new Exception("Declaration not found");

            _context.Declarations.Remove(entity);
            await _context.SaveChangesAsync();
        }

        private async Task<BeyannameFirmasi> FirmaAsync(int? firmaId, bool aktifOlmali)
        {
            if (firmaId is not int id)
                throw new BeyannameKuralException("firmaId", "Firma seçiniz.");

            var firma = await _context.Firmalar.AsNoTracking()
                .Where(f => f.Id == id)
                .Select(f => new { f.Id, f.KisaAd, f.Unvan, f.VergiKimlikNo, f.Aktif })
                .FirstOrDefaultAsync()
                ?? throw new BeyannameKuralException("firmaId", "Firma bulunamadı.");

            if (aktifOlmali && !firma.Aktif)
                throw new BeyannameKuralException("firmaId", "Firma pasif; beyanname yalnız Firmalarım'daki aktif firmalara eklenir.");

            return new BeyannameFirmasi(firma.Id, BeyannameFirmasi.AdOf(firma.KisaAd, firma.Unvan), firma.VergiKimlikNo, firma.Aktif);
        }
    }
}
