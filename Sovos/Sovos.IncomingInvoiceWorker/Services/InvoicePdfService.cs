using System.Net.Http.Headers;
using System.Text.Json;
using CatalogService.Api.Features.KdvBeyanname.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Sovos.IncomingInvoiceWorker.Data;
using Sovos.InvoiceWorker.Core.Interfaces;

namespace Sovos.IncomingInvoiceWorker.Services;

public class InvoicePdfService : IInvoicePdfService
{
    private const string FileFolder = "gelen-fatura-pdf";

    private readonly CatalogContext _catalog;
    private readonly IncomingWorkerSovosDbContext _sovos;
    private readonly IIncomingInvoiceScraper _scraper;
    private readonly ICredentialProtector _protector;
    private readonly IFirmaLock _firmaLock;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<InvoicePdfService> _logger;

    public InvoicePdfService(
        CatalogContext catalog,
        IncomingWorkerSovosDbContext sovos,
        IIncomingInvoiceScraper scraper,
        ICredentialProtector protector,
        IFirmaLock firmaLock,
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<InvoicePdfService> logger)
    {
        _catalog = catalog;
        _sovos = sovos;
        _scraper = scraper;
        _protector = protector;
        _firmaLock = firmaLock;
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
    }

    public async Task<FaturaPdfDto> GetOrFetchAsync(
        int firmaId, string faturaNo, int yil, int ay, CancellationToken ct)
    {
        faturaNo = faturaNo.Trim();

        // 1) Cache: zaten çekilmişse Sovos'a hiç gitme.
        var existing = await FindMappingAsync(firmaId, faturaNo, ct);
        if (existing is not null)
        {
            _logger.LogInformation(
                "PDF cache hit: FirmaId={FirmaId} FaturaNo={FaturaNo} FileId={FileId}",
                firmaId, faturaNo, existing.FileId);
            return ToDto(existing, cached: true);
        }

        // 2) Firma kilidi — aynı DP hesabına eşzamanlı oturum açılmasını engelle.
        using var _ = await _firmaLock.AcquireAsync(firmaId, ct);

        // 3) Kilit içinde tekrar kontrol (bekleyip sonra girmiş olabiliriz).
        existing = await FindMappingAsync(firmaId, faturaNo, ct);
        if (existing is not null)
            return ToDto(existing, cached: true);

        // 4) Firma + credential
        var company = await _sovos.Companies.FirstOrDefaultAsync(c => c.FirmaId == firmaId, ct)
            ?? throw new InvalidOperationException(
                $"FirmaId {firmaId} için Dijital Planet hesabı tanımlı değil.");

        string decryptedPassword;
        try { decryptedPassword = _protector.Decrypt(company.EncryptedPassword); }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "DP şifresi decrypt edilemedi. DataProtection key paylaşımını kontrol edin.", ex);
        }

        // 5) Fatura verilen ayda grid'de listelensin diye ayın başı-sonu aralığı.
        var fromDate = new DateTime(yil, ay, 1);
        var toDate = fromDate.AddMonths(1).AddDays(-1);

        // 6) Portaldan çek (login + nav + filtre + sağ tık → Tek Tek PDF İndir → ZIP → PDF).
        var pdf = await _scraper.DownloadInvoicePdfAsync(
            company, decryptedPassword, fromDate, toDate, faturaNo, ct);

        // 7) FileApiService'e (MinIO) yükle → FileId.
        var (fileId, storedName) = await UploadToFileApiAsync(pdf.PdfBytes, faturaNo, ct);

        // 8) Eşlemeyi kaydet.
        var mapping = new GelenFaturaPdf
        {
            FirmaId = firmaId,
            FaturaNo = faturaNo,
            FileId = fileId,
            FileName = storedName ?? pdf.FileName,
            OlusturmaTarihi = DateTime.UtcNow
        };
        _catalog.GelenFaturaPdfleri.Add(mapping);
        await _catalog.SaveChangesAsync(ct);

        _logger.LogInformation(
            "PDF çekildi + kaydedildi: FirmaId={FirmaId} FaturaNo={FaturaNo} FileId={FileId}",
            firmaId, faturaNo, fileId);

        return ToDto(mapping, cached: false);
    }

    private Task<GelenFaturaPdf?> FindMappingAsync(int firmaId, string faturaNo, CancellationToken ct)
        => _catalog.GelenFaturaPdfleri
            .FirstOrDefaultAsync(x => x.FirmaId == firmaId && x.FaturaNo == faturaNo, ct);

    private static FaturaPdfDto ToDto(GelenFaturaPdf m, bool cached) => new()
    {
        FaturaNo = m.FaturaNo,
        FileId = m.FileId,
        FileName = m.FileName,
        Cached = cached
    };

    // FileApiService POST /api/file/v1/uploads (multipart) → { data: { id, fileName, ... } }
    private async Task<(int fileId, string? fileName)> UploadToFileApiAsync(
        byte[] pdfBytes, string faturaNo, CancellationToken ct)
    {
        var baseUrl = _config["FileApiService:BaseUrl"]
            ?? throw new InvalidOperationException("FileApiService:BaseUrl yapılandırılmadı.");

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", $"{faturaNo}.pdf");
        content.Add(new StringContent(FileFolder), "folder");

        var client = _httpFactory.CreateClient("file-api");
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(60);

        var resp = await client.PostAsync("api/file/v1/uploads", content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"FileApiService upload başarısız ({(int)resp.StatusCode}): {body}");

        return ParseUploadResponse(body);
    }

    // HttpDataResponse<GenericUploadResultDto> — wrapper property/casing'e dayanmadan
    // savunmacı parse: "id" (int) taşıyan objeyi bul (önce root/data, sonra özyinelemeli).
    private static (int fileId, string? fileName) ParseUploadResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var target = FindObjectWithId(doc.RootElement)
            ?? throw new InvalidOperationException(
                $"FileApiService yanıtından FileId çıkarılamadı: {json}");

        var idEl = GetPropCI(target, "id")!.Value;
        var fileId = idEl.GetInt32();

        string? fileName = TryGetPropCI(target, "fileName", out var fnEl)
                           && fnEl.ValueKind == JsonValueKind.String
            ? fnEl.GetString()
            : null;

        return (fileId, fileName);
    }

    // Bir "id" (int) property'si taşıyan ilk objeyi döner (BFS: root → data → derinlik).
    private static JsonElement? FindObjectWithId(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;

        if (GetPropCI(el, "id") is { } idEl
            && idEl.ValueKind == JsonValueKind.Number
            && idEl.TryGetInt32(out _))
            return el;

        foreach (var p in el.EnumerateObject())
        {
            var found = FindObjectWithId(p.Value);
            if (found is not null) return found;
        }
        return null;
    }

    private static JsonElement? GetPropCI(JsonElement obj, string name)
        => TryGetPropCI(obj, name, out var v) ? v : null;

    private static bool TryGetPropCI(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var p in obj.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }
        value = default;
        return false;
    }
}
