using CatalogService.Api.Features.KdvBeyanname.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sovos.IncomingInvoiceWorker.Data;
using Sovos.InvoiceWorker.Core.Interfaces;

namespace Sovos.IncomingInvoiceWorker.Services;

public class IncomingInvoiceOrchestrator : IIncomingInvoiceOrchestrator
{
    // HataMesaji DB kolonu sınırı — uzun exception/stack trace metni buradan cap'lenir.
    private const int HataMesajiMaxLength = 500;

    private readonly CatalogContext _catalog;
    private readonly IncomingWorkerSovosDbContext _sovos;
    private readonly IIncomingInvoiceScraper _scraper;
    private readonly IGelenFaturaUpsertService _upsert;
    private readonly ICredentialProtector _protector;
    private readonly IFirmaLock _firmaLock;
    private readonly ILogger<IncomingInvoiceOrchestrator> _logger;

    public IncomingInvoiceOrchestrator(
        CatalogContext catalog,
        IncomingWorkerSovosDbContext sovos,
        IIncomingInvoiceScraper scraper,
        IGelenFaturaUpsertService upsert,
        ICredentialProtector protector,
        IFirmaLock firmaLock,
        ILogger<IncomingInvoiceOrchestrator> logger)
    {
        _catalog = catalog;
        _sovos = sovos;
        _scraper = scraper;
        _upsert = upsert;
        _protector = protector;
        _firmaLock = firmaLock;
        _logger = logger;
    }

    public async Task<long> RunForFirmaAsync(
        int firmaId, DateTime fromDate, DateTime toDate, CancellationToken ct)
    {
        // Tarama kaydını EN BAŞTA oluştur. Firma/DP kontrolü patlasa bile kayıt
        // 'Hatali' işaretlenebilsin — aksi halde tarama kaydı hiç oluşmaz, UI
        // polling son taramayı göremez ve sonsuza dek "Taranıyor..." gösterir.
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
            var firma = await _catalog.Firmalar.FirstOrDefaultAsync(f => f.Id == firmaId, ct)
                ?? throw new ArgumentException($"Firma bulunamadı: Id={firmaId}");

            // FirmaId üzerinden DP credentials çek — SovosCompanies.FirmaId ile bağlı.
            var company = await _sovos.Companies
                .FirstOrDefaultAsync(c => c.FirmaId == firmaId, ct)
                ?? throw new InvalidOperationException(
                    $"Firma '{firma.Unvan}' için Dijital Planet hesabı tanımlı değil. " +
                    "Fatura Kontrol sayfasından firma için DP kullanıcı/şifre tanımlayın.");

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

            // Firma kilidi — aynı DP hesabına manuel PDF çekimi ile eşzamanlı
            // oturum açılmasını engelle (aynı process içinde).
            using var firmaLock = await _firmaLock.AcquireAsync(firmaId, ct);

            var scraped = await _scraper.FetchIncomingInvoicesAsync(
                company, decryptedPassword, fromDate, toDate, ct);

            // Yeni tarama sonucu geldi — bu firma + tarih aralığındaki eski faturaları
            // temizle ki grid'de eski/yeni karışmasın. Boş sonuçta da temizlenir
            // (eski faturalar görünmesin). Scrape patlarsa buraya hiç gelinmez,
            // dolayısıyla başarısız taramada eski veri korunur.
            await _catalog.GelenFaturalar
                .Where(g => g.FirmaId == firmaId
                            && g.FaturaTarihi >= fromDate
                            && g.FaturaTarihi <= toDate)
                .ExecuteDeleteAsync(ct);

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
            // Endpoint zaten 202 döndü; re-throw ETME — sadece tarama kaydını
            // 'Hatali' işaretle ki UI polling dursun ve kullanıcıya hata gösterilsin.
            tarama.Durum       = KdvBeyannameTaramaDurumu.Hatali;
            tarama.BitisAt     = DateTime.UtcNow;
            tarama.HataMesaji  = ex.Message.Length > HataMesajiMaxLength
                ? ex.Message[..HataMesajiMaxLength]
                : ex.Message;
            await _catalog.SaveChangesAsync(CancellationToken.None);

            _logger.LogError(ex,
                "FirmaId {FirmaId}: Tarama hatası (TaramaId={TaramaId})",
                firmaId, tarama.Id);
        }

        return tarama.Id;
    }
}
