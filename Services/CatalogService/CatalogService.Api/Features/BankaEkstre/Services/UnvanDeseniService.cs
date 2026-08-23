using System.Text.RegularExpressions;
using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    public interface IUnvanDeseniService
    {
        Task<List<UnvanDeseniDto>> GetHepsiAsync(CancellationToken ct = default);
        Task<UnvanDeseniDto> CreateAsync(UnvanDeseniYazDto dto, CancellationToken ct = default);
        Task<UnvanDeseniDto?> UpdateAsync(int id, UnvanDeseniYazDto dto, CancellationToken ct = default);
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);

        /// <summary>Deneme kutusu: desen verilen metinde ne yakalıyor?</summary>
        DesenDenemeSonucDto Dene(DesenDenemeIstegiDto istek);
    }

    /// <summary>
    /// Unvan çıkarma desenlerinin yönetimi. Desenler banka bazlıdır: boş
    /// <see cref="UnvanDeseni.ParserTipi"/> tüm bankalarda geçerli, dolusu yalnız o bankada.
    /// Vakıfbank'ın "sorgu numaralı … tarafından" kalıbı Ziraat ekstresinde başka bir yeri
    /// yakalayabilir; bu yüzden filtre şart.
    ///
    /// Geçersiz regex kaydedilmez: çalışma zamanında <see cref="UnvanCikarici"/> bozuk
    /// deseni sessizce atlıyor, kullanıcı desenin neden hiç tutmadığını göremezdi.
    /// </summary>
    public class UnvanDeseniService : IUnvanDeseniService
    {
        private readonly CatalogContext _db;
        private readonly IEkstreParserSecici _parserSecici;
        private readonly IUnvanCikarici _unvanCikarici;

        public UnvanDeseniService(CatalogContext db, IEkstreParserSecici parserSecici, IUnvanCikarici unvanCikarici)
        {
            _db = db;
            _parserSecici = parserSecici;
            _unvanCikarici = unvanCikarici;
        }

        public async Task<List<UnvanDeseniDto>> GetHepsiAsync(CancellationToken ct = default)
        {
            var kayitlar = await _db.EkstreUnvanDesenleri.AsNoTracking()
                .OrderBy(d => d.Sira).ThenBy(d => d.Id)
                .ToListAsync(ct);

            return kayitlar.Select(Esle).ToList();
        }

        public async Task<UnvanDeseniDto> CreateAsync(UnvanDeseniYazDto dto, CancellationToken ct = default)
        {
            var kayit = new UnvanDeseni();
            await UygulaAsync(kayit, dto, null, ct);

            _db.EkstreUnvanDesenleri.Add(kayit);
            await _db.SaveChangesAsync(ct);

            return Esle(kayit);
        }

        public async Task<UnvanDeseniDto?> UpdateAsync(int id, UnvanDeseniYazDto dto, CancellationToken ct = default)
        {
            var kayit = await _db.EkstreUnvanDesenleri.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (kayit is null) return null;

            await UygulaAsync(kayit, dto, id, ct);
            await _db.SaveChangesAsync(ct);

            return Esle(kayit);
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var kayit = await _db.EkstreUnvanDesenleri.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (kayit is null) return false;

            _db.EkstreUnvanDesenleri.Remove(kayit);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        /// <summary>
        /// Denemede iki sonuç birden döner: regex'in <b>ham</b> yakalaması ve
        /// <see cref="UnvanCikarici"/>'nın kabul ettiği unvan. İkisi ayrı, çünkü çıkarıcı
        /// yakalamayı eleyebiliyor (üç harften kısa, IBAN künyesi, "Ad Soyad/Unvan:" alanı).
        /// Kullanıcı "desen tuttu ama unvan çıkmadı" durumunu ancak böyle görür.
        /// </summary>
        public DesenDenemeSonucDto Dene(DesenDenemeIstegiDto istek)
        {
            var sonuc = new DesenDenemeSonucDto();
            var desen = (istek.Desen ?? string.Empty).Trim();

            if (desen.Length == 0)
            {
                sonuc.Hata = "Desen boş.";
                return sonuc;
            }

            Regex regex;
            try
            {
                // Çalışma zamanıyla aynı seçenekler: büyük/küçük harf duyarlı, zaman aşımlı.
                regex = new Regex(desen, RegexOptions.CultureInvariant, YapilandirmaDogrulama.RegexZamanAsimi);
            }
            catch (ArgumentException ex)
            {
                sonuc.Hata = $"Geçersiz regex: {ex.Message}";
                return sonuc;
            }

            sonuc.Gecerli = true;

            var metin = istek.OrnekMetin ?? string.Empty;
            if (metin.Length == 0)
            {
                sonuc.Not = "Deneme metni boş; bir ekstre açıklaması yapıştırın.";
                return sonuc;
            }

            var grupNo = istek.GrupNo < 0 ? 0 : istek.GrupNo;

            Match eslesme;
            try
            {
                eslesme = regex.Match(metin);
            }
            catch (RegexMatchTimeoutException)
            {
                sonuc.Hata = "Desen bu metinde zaman aşımına uğradı; çalışma zamanında da atlanır.";
                return sonuc;
            }

            sonuc.Eslesti = eslesme.Success;

            if (!eslesme.Success)
            {
                sonuc.Not = "Desen bu metinde tutmadı.";
                return sonuc;
            }

            if (grupNo != 0 && grupNo >= eslesme.Groups.Count)
            {
                sonuc.Not = $"Desen tuttu ama {grupNo} numaralı yakalama grubu yok " +
                            $"(desende {eslesme.Groups.Count - 1} grup var).";
                return sonuc;
            }

            sonuc.HamYakalanan = eslesme.Groups[grupNo].Value;

            // Çıkarıcının kendi elemeleri: tek desenlik bir liste verilir, sonuç birebir
            // gerçek işlemedeki davranıştır.
            var tekDesen = new List<UnvanDeseni>
            {
                new() { ParserTipi = string.Empty, Desen = desen, GrupNo = grupNo, Sira = 0, Aktif = true }
            };

            sonuc.Unvan = _unvanCikarici.Cikar(metin, tekDesen).Unvan;

            if (string.IsNullOrWhiteSpace(sonuc.Unvan))
                sonuc.Not = "Desen tuttu ama yakalama unvan sayılmadı: üç harften kısa, IBAN künyesi " +
                            "veya hesap sahibinin kendi unvan alanı olabilir.";

            return sonuc;
        }

        private async Task UygulaAsync(UnvanDeseni kayit, UnvanDeseniYazDto dto, int? mevcutId, CancellationToken ct)
        {
            var parser = YapilandirmaDogrulama.ParserNormalize(_parserSecici, dto.ParserTipi, nameof(dto.ParserTipi));

            var desen = YapilandirmaDogrulama.DesenKirp(dto.Desen, 400, nameof(dto.Desen), "Regex deseni");
            YapilandirmaDogrulama.RegexDogrula(desen, nameof(dto.Desen));

            if (dto.GrupNo < 0)
                throw new BankaEkstreKuralException(nameof(dto.GrupNo), "Grup numarası negatif olamaz.");

            await TekilligiDogrulaAsync(parser, desen, mevcutId, ct);

            kayit.ParserTipi = parser;
            kayit.Desen = desen;
            kayit.GrupNo = dto.GrupNo;
            kayit.Aciklama = string.IsNullOrWhiteSpace(dto.Aciklama) ? null : Normalizasyon.Kirp(dto.Aciklama, 200);
            kayit.Sira = dto.Sira;
            kayit.Aktif = dto.Aktif;
        }

        /// <summary>
        /// Aynı ayrıştırıcı için aynı desen iki kez kaydedilmez; ikincisi hiç denenmez
        /// (ilk tutan kazanır) ve düzenlenince neden değişmediği anlaşılmazdı.
        /// Karşılaştırma harf duyarlı: desenler büyük/küçük harf duyarlı çalışıyor.
        /// </summary>
        private async Task TekilligiDogrulaAsync(string parser, string desen, int? haricId, CancellationToken ct)
        {
            var cakisma = await _db.EkstreUnvanDesenleri.AsNoTracking()
                .AnyAsync(d => d.ParserTipi == parser && d.Desen == desen && (haricId == null || d.Id != haricId), ct);

            if (cakisma)
                throw new BankaEkstreKuralException(nameof(UnvanDeseni.Desen),
                    "Bu ayrıştırıcı için aynı desen zaten kayıtlı; mevcut kaydı düzenleyin.");
        }

        private UnvanDeseniDto Esle(UnvanDeseni d) => new()
        {
            Id = d.Id,
            ParserTipi = d.ParserTipi,
            ParserAdi = YapilandirmaDogrulama.ParserAdi(_parserSecici, d.ParserTipi),
            Desen = d.Desen,
            GrupNo = d.GrupNo,
            Aciklama = d.Aciklama,
            Sira = d.Sira,
            Aktif = d.Aktif
        };
    }
}
