using CatalogService.Api.Features.KdvBeyanname.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sovos.IncomingInvoiceWorker.Data;
using Sovos.InvoiceWorker.Core.Interfaces;

namespace Sovos.IncomingInvoiceWorker.Services;

public class IncomingInvoiceOrchestrator : IIncomingInvoiceOrchestrator
{
    private readonly CatalogContext _catalog;
    private readonly IncomingWorkerSovosDbContext _sovos;
    private readonly IIncomingInvoiceScraper _scraper;
    private readonly IGelenFaturaUpsertService _upsert;
    private readonly ICredentialProtector _protector;
    private readonly ILogger<IncomingInvoiceOrchestrator> _logger;

    public IncomingInvoiceOrchestrator(
        CatalogContext catalog,
        IncomingWorkerSovosDbContext sovos,
        IIncomingInvoiceScraper scraper,
        IGelenFaturaUpsertService upsert,
        ICredentialProtector protector,
        ILogger<IncomingInvoiceOrchestrator> logger)
    {
        _catalog = catalog;
        _sovos = sovos;
        _scraper = scraper;
        _upsert = upsert;
        _protector = protector;
        _logger = logger;
    }

    public async Task<long> RunForFirmaAsync(
        int firmaId, DateTime fromDate, DateTime toDate, CancellationToken ct)
    {
        var firma = await _catalog.Firmalar.FirstOrDefaultAsync(f => f.Id == firmaId, ct)
            ?? throw new ArgumentException($"Firma bulunamadı: Id={firmaId}");

        // FirmaId üzerinden DP credentials çek — SovosCompanies.FirmaId ile bağlı.
        var company = await _sovos.Companies
            .FirstOrDefaultAsync(c => c.FirmaId == firmaId, ct)
            ?? throw new InvalidOperationException(
                $"Firma '{firma.Unvan}' için Dijital Planet hesabı tanımlı değil. " +
                "Fatura Kontrol sayfasından firma için DP kullanıcı/şifre tanımlayın.");

        var tarama = new KdvBeyannameTarama
        {
            FirmaId          = firmaId,
            BaslangicTarihi  = fromDate,
            BitisTarihi      = toDate,
            Durum            = KdvBeyannameTaramaDurumu.Calisiyor,
            BaslangicAt      = DateTime.UtcNow
        };
        _catalog.KdvBeyannameTaramalar.Add(tarama);
        await _catalog.SaveChangesAsync(ct);

        try
        {
            string decryptedPassword;
            try
            {
                decryptedPassword = _protector.Decrypt(company.EncryptedPassword);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "DP şifresi decrypt edilemedi. DataProtection key folder'ı paylaşımını kontrol edin.",
                    ex);
            }

            var scraped = await _scraper.FetchIncomingInvoicesAsync(
                company, decryptedPassword, fromDate, toDate, ct);

            await _upsert.UpsertAsync(firmaId, scraped, ct);

            tarama.Durum               = KdvBeyannameTaramaDurumu.Tamamlandi;
            tarama.BitisAt             = DateTime.UtcNow;
            tarama.BulunanFaturaSayisi = scraped.Count;
            await _catalog.SaveChangesAsync(ct);

            _logger.LogInformation(
                "FirmaId {FirmaId}: Tarama tamamlandı (TaramaId={TaramaId}, {Count} fatura).",
                firmaId, tarama.Id, scraped.Count);
        }
        catch (Exception ex)
        {
            tarama.Durum       = KdvBeyannameTaramaDurumu.Hatali;
            tarama.BitisAt     = DateTime.UtcNow;
            tarama.HataMesaji  = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            await _catalog.SaveChangesAsync(CancellationToken.None);

            _logger.LogError(ex,
                "FirmaId {FirmaId}: Tarama hatası (TaramaId={TaramaId})",
                firmaId, tarama.Id);
            throw;
        }

        return tarama.Id;
    }
}
