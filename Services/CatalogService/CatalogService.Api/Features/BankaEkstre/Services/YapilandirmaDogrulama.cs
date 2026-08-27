using System.Text.RegularExpressions;
using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    /// <summary>
    /// Sabit kural / açıklama şablonu / unvan deseni tablolarının ortak denetimleri.
    /// Üçü de aynı üç şeyi doğruluyor: ayrıştırıcı tanımlı mı, regex derleniyor mu,
    /// hesap kodu planda var mı. Tek yerde durur ki bir denetim eklendiğinde üçünde
    /// birden geçerli olsun.
    /// </summary>
    public static class YapilandirmaDogrulama
    {
        /// <summary>Boş ayrıştırıcının ekrandaki karşılığı.</summary>
        public const string TumBankalar = "Tüm bankalar";

        /// <summary>
        /// Bozuk desen tüm ekstreyi durdurmasın diye çalışma zamanında da kullanılan
        /// zaman aşımı; denemede aynı sınır uygulanır ki ekranda görülen davranış
        /// gerçek işlemeyle aynı olsun.
        /// </summary>
        public static readonly TimeSpan RegexZamanAsimi = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Ayrıştırıcı seçimini normalize eder. <b>Boş = tüm bankalar</b>: kayıt her
        /// bankada geçerli olur. Doluysa kayıtlı bir ayrıştırıcı olmak zorunda —
        /// yazım hatası ("VAKIFBAK_VADESIZ") kaydı sessizce ölü bırakırdı.
        /// </summary>
        public static string ParserNormalize(IEkstreParserSecici secici, string? parserTipi, string field)
        {
            if (string.IsNullOrWhiteSpace(parserTipi)) return string.Empty;

            var parser = secici.Sec(parserTipi)
                ?? throw new BankaEkstreKuralException(field,
                    $"Tanımsız ayrıştırıcı: '{parserTipi.Trim()}'. Boş bırakılırsa kayıt tüm bankalarda geçerli olur; " +
                    "seçilebilir tipler: " + string.Join(", ", secici.Hepsi.Select(p => p.ParserTipi)) + ".");

            // Kayıtlı tipin kendi yazımı saklanır; eşleştirme sırasında tam eşitlik aranıyor.
            return parser.ParserTipi;
        }

        /// <summary>Listede gösterilecek ad. Ayrıştırıcı kaldırılmışsa ham tip yazılır, kayıt gizlenmez.</summary>
        public static string ParserAdi(IEkstreParserSecici secici, string parserTipi)
            => string.IsNullOrWhiteSpace(parserTipi)
                ? TumBankalar
                : secici.Sec(parserTipi)?.Ad ?? parserTipi;

        /// <summary>
        /// Zorunlu bir desen/şablon alanını kırpar ve sınırı aşarsa hata verir.
        ///
        /// <see cref="Normalizasyon.Kirp"/> <b>kullanılmaz</b>: o, ardışık boşlukları teke
        /// indiriyor ve sınırı aşan metni sessizce kesiyor. Desen alanlarında ikisi de
        /// yıkıcı — regex'te boşluk anlamlı, sessiz kesme ise yarım bir desen kaydeder.
        /// </summary>
        public static string DesenKirp(string? deger, int enFazla, string field, string ad)
        {
            var temiz = (deger ?? string.Empty).Trim();

            if (temiz.Length == 0)
                throw new BankaEkstreKuralException(field, $"{ad} boş olamaz.");

            if (temiz.Length > enFazla)
                throw new BankaEkstreKuralException(field, $"{ad} en fazla {enFazla} karakter olabilir.");

            return temiz;
        }

        /// <summary>
        /// Desen .NET regex'i olarak derleniyor mu? Geçersiz desen kaydedilmez: kaydedilseydi
        /// çalışma zamanında sessizce atlanır ve kullanıcı desenin neden hiç tutmadığını anlamazdı.
        /// </summary>
        public static void RegexDogrula(string? desen, string field)
        {
            if (string.IsNullOrWhiteSpace(desen))
                throw new BankaEkstreKuralException(field, "Desen boş olamaz.");

            try
            {
                _ = new Regex(desen, RegexOptions.CultureInvariant, RegexZamanAsimi);
            }
            catch (ArgumentException ex)
            {
                throw new BankaEkstreKuralException(field, $"Geçersiz regex: {ex.Message}");
            }
        }

        /// <summary>
        /// Kategori seçimi geçerli mi? Boş bırakılabilir (kategori zorunlu değil), doluysa
        /// tanımlı bir kategori olmalı — silinmiş bir kategorinin Id'si kayda yazılırsa
        /// kural hiçbir kategoride görünmez ve kullanıcı nedenini göremez.
        /// </summary>
        public static async Task<int?> KategoriDogrulaAsync(
            CatalogContext db, int? kategoriId, string field, CancellationToken ct)
        {
            if (kategoriId is not int id || id <= 0) return null;

            var varMi = await db.EkstreIslemKategorileri.AnyAsync(k => k.Id == id, ct);
            if (!varMi)
                throw new BankaEkstreKuralException(field, "Seçilen işlem kategorisi bulunamadı.");

            return id;
        }

        /// <summary>
        /// Id → ad sözlüğü; listelerde kategori adını göstermek için. Kategori sayısı
        /// yirmi civarında olduğu için tablo bir kerede okunur.
        /// </summary>
        public static async Task<Dictionary<int, string>> KategoriAdlariAsync(CatalogContext db, CancellationToken ct)
            => await db.EkstreIslemKategorileri.AsNoTracking()
                       .ToDictionaryAsync(k => k.Id, k => k.Ad, ct);

        /// <summary>Sözlükten güvenli okuma; kategori silinmişse boş döner.</summary>
        public static string? KategoriAdi(IReadOnlyDictionary<int, string>? adlar, int? kategoriId)
            => kategoriId is int id && adlar is not null && adlar.TryGetValue(id, out var ad) ? ad : null;

        /// <summary>
        /// Kod hesap planında var mı? Kural bir daha sorulmadan uygulandığı için geçersiz
        /// kod kaydedilmez — yanlış yazılmış bir kod her ay sessizce yanlış hesaba yazardı.
        /// Plan hiç yüklenmemişse denetim atlanır (kurulum sırası bozulmasın) ve null döner.
        /// </summary>
        ///
        /// Plan <b>firma bazlıdır</b>: global kural tabloları (sabit kural, vergi kodu) bile
        /// kodu seçili firmanın planına karşı doğrular — kod formatı ORKA'da firmadan firmaya
        /// değişiyor ve kullanıcı kuralı hangi firmadaysa oradaki planla yazıyor.
        /// <returns>Plandaki kayıt; plan boşsa null.</returns>
        public static async Task<HesapPlaniKaydi?> HesapKoduDogrulaAsync(
            CatalogContext db, int firmaId, string? hesapKodu, string field, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(hesapKodu))
                throw new BankaEkstreKuralException(field, "Hesap kodu boş olamaz.");

            var kod = Normalizasyon.HesapKoduNormalize(hesapKodu);

            var plan = db.EkstreHesapPlani.Where(h => h.FirmaId == firmaId);

            if (!await plan.AnyAsync(ct)) return null;

            var kayit = await plan.AsNoTracking()
                .FirstOrDefaultAsync(h => h.Kod == kod && h.Aktif, ct);

            if (kayit is null)
                throw new BankaEkstreKuralException(field,
                    $"'{kod}' hesap planında yok. Kodu hesap planından seçin veya önce planı güncelleyin.");

            return kayit;
        }

        /// <summary>
        /// Sabit kuralın ana grup listesini ("195, 196") doğrular ve saklama biçimine
        /// çevirir. Boş bırakılırsa <c>null</c> döner: kural tek gruplu kalır.
        ///
        /// Her parça ana gruba indirgenir, yani kullanıcı tam kod yazsa da ("195 01")
        /// kural çalışır. Liste <b>yalnız</b> alt hesabı kullanıcıdan beklenen kurallarda
        /// anlamlı; başka bir kuralda doldurulursa sessizce yok saymak yerine hata verilir,
        /// yoksa kullanıcı yazdığı şeyin neden hiçbir etkisi olmadığını göremezdi.
        /// </summary>
        public static string? AnaGruplarDogrula(string? anaGruplar, bool altHesapGerekli, string field)
        {
            var gruplar = Normalizasyon.AnaGruplariAyir(anaGruplar);

            if (gruplar.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(anaGruplar))
                    throw new BankaEkstreKuralException(field,
                        "Ana grup listesi okunamadı. Grupları virgülle ayırarak yazın: \"195, 196\".");

                return null;
            }

            if (!altHesapGerekli)
                throw new BankaEkstreKuralException(field,
                    "Ana grup listesi yalnız \"kural yalnız ana grubu belirliyor\" seçiliyken kullanılır; " +
                    "ya seçeneği işaretleyin ya da listeyi boşaltın.");

            var birlesik = Normalizasyon.AnaGruplariBirlestir(gruplar);

            if (birlesik.Length > 200)
                throw new BankaEkstreKuralException(field, "Ana grup listesi 200 karakteri aşamaz.");

            return birlesik;
        }
    }
}
