using System.Globalization;
using WebApp.Shared.Dto.Parolalar;
using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Pages.Yonetim.Parolalar
{
    /// <summary>Kartta / formda bir alan. <c>Sifre</c> ise değeri listede yoktur, ayrı uçtan (erişim kaydıyla) gelir.</summary>
    public sealed record PwAlan(string Anahtar, string Etiket, bool Sifre, bool Zorunlu, bool Mono = false, string? Ipucu = null);

    /// <summary>Bir sekme (tür): taslaktaki TURLER.</summary>
    public sealed record PwTur(ParolaTuru Tur, string Kod, string Ad, string Tekil, string Alt, IReadOnlyList<PwAlan> Alanlar);

    /// <summary>Firma açılır kutusunun seçeneği: "f:12" (Firmalarım) ya da "elle".</summary>
    public sealed record PwFirmaSecenegi(string Deger, string Ad);

    /// <summary>
    /// Parolalar sayfasının saf kuralları (testli): sekmeler ve alanları, arama + firma süzgeci + sıralama, firma
    /// seçenekleri. Şifre DEĞERİ burada hiç yok — yalnız alan tanımları.
    /// </summary>
    public static class ParolaListesi
    {
        public const string Elle = "elle";

        private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
        private static readonly StringComparer TrSirala = StringComparer.Create(Tr, true);

        public static readonly IReadOnlyList<string> Entegratorler = new[]
        {
            "Digital Planet", "eLogo", "İşNet", "Luca e-Fatura", "MySoft", "QNB eSolutions", "Uyumsoft", "Diğer"
        };

        // Şifre alanı anahtarları sunucudaki ParolaKurallari ile aynı (alan ucu bu adları bekler).
        public static readonly IReadOnlyList<PwTur> Turler = new[]
        {
            new PwTur(ParolaTuru.Entegrator, "ent", "Entegratör", "entegratör kaydı", "e-Fatura / e-Arşiv portalı giriş bilgileri", new[]
            {
                new PwAlan("portal", "Portal adresi", false, false, Ipucu: "Giriş sayfasının adresi"),
                new PwAlan("kullanici", "Kullanıcı adı", false, true, Mono: true),
                new PwAlan("sifre", "Şifre", true, true)
            }),
            new PwTur(ParolaTuru.DijitalVergiDairesi, "dvd", "Dijital Vergi Dairesi", "Dijital VD kaydı",
                "GİB Dijital Vergi Dairesi / İnteraktif Vergi Dairesi girişi", new[]
            {
                new PwAlan("kullanici", "Kullanıcı kodu", false, true, Mono: true, Ipucu: "GİB kullanıcı kodu"),
                new PwAlan("parola", "Parola", true, true),
                new PwAlan("sifre", "Şifre", true, true)
            }),
            new PwTur(ParolaTuru.Sgk, "sgk", "SGK", "SGK işyeri kaydı", "e-Bildirge işyeri girişi; şubeler ayrı kayıt", new[]
            {
                new PwAlan("ek", "Kullanıcı kodu (ek)", false, false, Mono: true, Ipucu: "Ör. 2, 22"),
                new PwAlan("kullanici", "Kullanıcı adı (TCKN)", false, true, Mono: true),
                new PwAlan("sistemSifresi", "Sistem şifresi", true, true),
                new PwAlan("isyeriSifresi", "İşyeri şifresi", true, true)
            })
        };

        public static PwTur TurBul(ParolaTuru tur) => Turler.First(t => t.Tur == tur);

        public static PwTur? KodBul(string? kod) => Turler.FirstOrDefault(t => t.Kod == kod);

        /// <summary>Kartta görünen firma adı: pasif firmaya "(pasif)" eki.</summary>
        public static string FirmaGorunenAd(ParolaDto p) => p.FirmaPasif ? $"{p.FirmaAdi} (pasif)" : p.FirmaAdi;

        /// <summary>Listede değeri olmayan (düz) alanın metni; şifre alanları için null.</summary>
        public static string? DuzDeger(ParolaDto p, string anahtar) => anahtar switch
        {
            "portal" => p.PortalAdresi,
            "kullanici" => p.KullaniciAdi,
            "ek" => p.KullaniciKoduEk,
            _ => null
        };

        public static bool SifreDolu(ParolaDto p, string anahtar) => anahtar switch
        {
            "sifre" => p.SifreDolu,
            "parola" => p.ParolaDolu,
            "sistemSifresi" => p.SistemSifresiDolu,
            "isyeriSifresi" => p.IsyeriSifresiDolu,
            _ => false
        };

        /// <summary>
        /// Sekmenin kartları: firma süzgeci (görünen ad), arama (firma, kullanıcı adı, entegratör, şube; Türkçe büyük/küçük
        /// harf duyarsız), sıralama firma → şube.
        /// </summary>
        public static List<ParolaDto> Suz(IEnumerable<ParolaDto> liste, ParolaTuru tur, string? arama, string? firma)
        {
            var q = (arama ?? string.Empty).Trim().ToLower(Tr);
            return liste
                .Where(p => p.Tur == tur)
                .Where(p => string.IsNullOrEmpty(firma) || FirmaGorunenAd(p) == firma)
                .Where(p => q.Length == 0 || string.Join(" ", p.FirmaAdi, p.KullaniciAdi, p.Entegrator, p.Sube).ToLower(Tr).Contains(q))
                .OrderBy(p => p.FirmaAdi, TrSirala)
                .ThenBy(p => p.Sube ?? string.Empty, TrSirala)
                .ToList();
        }

        /// <summary>Firma süzgecinin seçenekleri: kayıtlardaki firmalar (tüm sekmeler), görünen adla, tekil ve sıralı.</summary>
        public static List<string> SuzgecFirmalari(IEnumerable<ParolaDto> liste)
            => liste.Select(FirmaGorunenAd).Distinct().OrderBy(a => a, TrSirala).ToList();

        /// <summary>
        /// Formdaki firma seçenekleri: Firmalarım'daki AKTİF firmalar; düzenlenen kaydın kendi firması pasifse o da
        /// "(pasif)" ekiyle; en sonda "Firmalarım'da yok, elle yazacağım".
        /// </summary>
        public static List<PwFirmaSecenegi> FormFirmalari(IEnumerable<FirmaDto> firmalar, ParolaDto? duzenlenen)
        {
            var secenekler = firmalar
                .Where(f => f.Aktif || (duzenlenen?.FirmaId is { } id && f.Id == id))
                .Select(f => (f.Aktif, Secenek: new PwFirmaSecenegi($"f:{f.Id}", f.Aktif ? Ad(f) : $"{Ad(f)} (pasif)")))
                .OrderBy(x => !x.Aktif)
                .ThenBy(x => x.Secenek.Ad, TrSirala)
                .Select(x => x.Secenek)
                .ToList();
            secenekler.Add(new PwFirmaSecenegi(Elle, "Firmalarım'da yok, elle yazacağım"));
            return secenekler;
        }

        public static string Ad(FirmaDto f) => string.IsNullOrWhiteSpace(f.KisaAd) ? f.Unvan.Trim() : f.KisaAd.Trim();

        /// <summary>Portal adresinin kısa gösterimi: şema ve sondaki "/" atılır.</summary>
        public static string KisaAdres(string url)
        {
            var s = url.Trim();
            if (s.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) s = s[8..];
            else if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) s = s[7..];
            return s.TrimEnd('/');
        }
    }
}
