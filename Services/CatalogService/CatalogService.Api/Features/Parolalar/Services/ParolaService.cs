using System.Globalization;
using System.Text.RegularExpressions;
using CatalogService.Api.Features.Parolalar.Domain;
using CatalogService.Api.Features.Parolalar.Dtos;
using CatalogService.Api.Infrastructure.Auth;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Parolalar.Services
{
    public interface IParolaService
    {
        /// <summary>Tüm kayıtlar (üç tür), şifre değerleri OLMADAN.</summary>
        Task<List<ParolaDto>> ListeAsync(CancellationToken ct = default);

        Task<ParolaDto> EkleAsync(ParolaKaydetDto dto, CancellationToken ct = default);

        /// <summary>Boş bırakılan şifre alanları değişmez.</summary>
        Task<ParolaDto> GuncelleAsync(int id, ParolaKaydetDto dto, CancellationToken ct = default);

        /// <summary>Kayıt silinir; erişim kayıtları kalır.</summary>
        Task SilAsync(int id, CancellationToken ct = default);

        /// <summary>Tek şifre alanının değeri; <paramref name="islem"/> "goruntule" ya da "kopyala", erişim kaydına yazılır.</summary>
        Task<ParolaAlanDegeriDto> AlanAsync(int id, string alan, string? islem, CancellationToken ct = default);

        /// <summary>
        /// Yetkisiz (403) isteği erişim kaydına "reddedildi" olarak yazar: rotadaki id ve alan (varsa), kullanıcı, tarih.
        /// Kayıt varsa türü ve firma adı da; şifre hiçbir zaman.
        /// </summary>
        Task ReddiKaydetAsync(int? id, string? alan, CancellationToken ct = default);
    }

    /// <summary>Kural ihlali → 400 <c>{ field, message }</c>.</summary>
    public sealed class ParolaKuralException : Exception
    {
        public ParolaKuralException(string field, string message) : base(message) => Field = field;
        public string Field { get; }
    }

    /// <summary>SGK'da aynı firma + şube zaten var → 409 <c>{ field, message }</c>.</summary>
    public sealed class ParolaCakismaException : Exception
    {
        public ParolaCakismaException(string message) : base(message) { }
    }

    /// <summary>Türlere göre alanlar: hangi şifre alanı hangi türde var (hepsi o türde zorunlu).</summary>
    public static class ParolaKurallari
    {
        public const string AlanSifre = "sifre";
        public const string AlanParola = "parola";
        public const string AlanSistemSifresi = "sistemSifresi";
        public const string AlanIsyeriSifresi = "isyeriSifresi";

        public static readonly IReadOnlyList<string> Entegratorler = new[]
        {
            "Digital Planet", "eLogo", "İşNet", "Luca e-Fatura", "MySoft", "QNB eSolutions", "Uyumsoft", "Diğer"
        };

        public static IReadOnlyList<string> SifreAlanlari(ParolaTuru tur) => tur switch
        {
            ParolaTuru.Entegrator => new[] { AlanSifre },
            ParolaTuru.DijitalVergiDairesi => new[] { AlanParola, AlanSifre },
            ParolaTuru.Sgk => new[] { AlanSistemSifresi, AlanIsyeriSifresi },
            _ => Array.Empty<string>()
        };

        public static string AlanAdi(string alan) => alan switch
        {
            AlanSifre => "Şifre",
            AlanParola => "Parola",
            AlanSistemSifresi => "Sistem şifresi",
            AlanIsyeriSifresi => "İşyeri şifresi",
            _ => alan
        };
    }

    /// <summary>
    /// Yönetim › Parolalar. Şifreler <see cref="IParolaSifreleyici"/> ile şifreli saklanır; liste şifre döndürmez; tek
    /// alanın değeri yalnız <see cref="AlanAsync"/> ile ve her seferinde erişim kaydıyla. Ekle / düzenle / sil de
    /// erişim kaydına yazılır. Bu sınıf HİÇBİR şeyi loglamaz (şifre ya da gövde loglara düşmesin).
    /// </summary>
    public class ParolaService : IParolaService
    {
        private const int AzamiSifre = 200;

        private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
        private static readonly StringComparer TrSirala = StringComparer.Create(Tr, true);
        private static readonly Regex Bosluklar = new(@"\s+", RegexOptions.Compiled);

        private readonly CatalogContext _db;
        private readonly IParolaSifreleyici _sifreleyici;
        private readonly TimeProvider _saat;
        private readonly IHttpCurrentUser? _kullanici;

        public ParolaService(CatalogContext db, IParolaSifreleyici sifreleyici, TimeProvider saat, IHttpCurrentUser? kullanici = null)
        {
            _db = db;
            _sifreleyici = sifreleyici;
            _saat = saat;
            _kullanici = kullanici;
        }

        public async Task<List<ParolaDto>> ListeAsync(CancellationToken ct = default)
        {
            var kayitlar = await _db.Parolalar.AsNoTracking().ToListAsync(ct);
            var firmalar = await FirmalarAsync(kayitlar.Where(k => k.FirmaId is not null).Select(k => k.FirmaId!.Value), ct);

            return kayitlar
                .Select(k => Dto(k, firmalar))
                .OrderBy(d => d.Tur)
                .ThenBy(d => d.FirmaAdi, TrSirala)
                .ThenBy(d => d.Sube ?? string.Empty, TrSirala)
                .ToList();
        }

        public async Task<ParolaDto> EkleAsync(ParolaKaydetDto dto, CancellationToken ct = default)
        {
            var kayit = new Parola { Tur = TurDogrula(dto.Tur) };
            await UygulaAsync(kayit, dto, yeni: true, ct);

            var simdi = Simdi();
            kayit.OlusturmaTarihi = simdi;
            Damgala(kayit, simdi);

            await IslemIcindeAsync(async () =>
            {
                _db.Parolalar.Add(kayit);
                await _db.SaveChangesAsync(ct);
                _db.ParolaErisimKayitlari.Add(Erisim(kayit, ParolaIslemleri.Ekle, null, simdi));
                await _db.SaveChangesAsync(ct);
            }, ct);

            return Dto(kayit, await FirmalarAsync(kayit.FirmaId is { } f ? new[] { f } : Array.Empty<int>(), ct));
        }

        public async Task<ParolaDto> GuncelleAsync(int id, ParolaKaydetDto dto, CancellationToken ct = default)
        {
            var kayit = await _db.Parolalar.FirstOrDefaultAsync(p => p.Id == id, ct)
                        ?? throw new KeyNotFoundException("Kayıt bulunamadı.");
            if (dto.Tur != kayit.Tur)
                throw new ParolaKuralException("tur", "Kaydın türü değiştirilemez.");

            var degisen = await UygulaAsync(kayit, dto, yeni: false, ct);

            var simdi = Simdi();
            Damgala(kayit, simdi);
            _db.ParolaErisimKayitlari.Add(Erisim(kayit, ParolaIslemleri.Duzenle,
                degisen.Count > 0 ? string.Join(",", degisen) : null, simdi));
            await _db.SaveChangesAsync(ct);

            return Dto(kayit, await FirmalarAsync(kayit.FirmaId is { } f ? new[] { f } : Array.Empty<int>(), ct));
        }

        public async Task SilAsync(int id, CancellationToken ct = default)
        {
            var kayit = await _db.Parolalar.FirstOrDefaultAsync(p => p.Id == id, ct)
                        ?? throw new KeyNotFoundException("Kayıt bulunamadı.");

            _db.Parolalar.Remove(kayit);
            _db.ParolaErisimKayitlari.Add(Erisim(kayit, ParolaIslemleri.Sil, null, Simdi()));
            await _db.SaveChangesAsync(ct);
        }

        public async Task<ParolaAlanDegeriDto> AlanAsync(int id, string alan, string? islem, CancellationToken ct = default)
        {
            var islemAdi = (islem ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                ParolaIslemleri.Goruntule => ParolaIslemleri.Goruntule,
                ParolaIslemleri.Kopyala => ParolaIslemleri.Kopyala,
                _ => throw new ParolaKuralException("islem", "İşlem 'goruntule' ya da 'kopyala' olmalı.")
            };

            var kayit = await _db.Parolalar.FirstOrDefaultAsync(p => p.Id == id, ct)
                        ?? throw new KeyNotFoundException("Kayıt bulunamadı.");

            var alanAdi = ParolaKurallari.SifreAlanlari(kayit.Tur)
                              .FirstOrDefault(a => string.Equals(a, alan, StringComparison.OrdinalIgnoreCase))
                          ?? throw new ParolaKuralException("alan", "Bu kayıt türünde böyle bir şifre alanı yok.");

            var sifreli = SifreliDeger(kayit, alanAdi);
            var deger = string.IsNullOrEmpty(sifreli) ? string.Empty : _sifreleyici.Coz(sifreli);

            _db.ParolaErisimKayitlari.Add(Erisim(kayit, islemAdi, alanAdi, Simdi()));
            await _db.SaveChangesAsync(ct);

            return new ParolaAlanDegeriDto { Alan = alanAdi, Deger = deger };
        }

        public async Task ReddiKaydetAsync(int? id, string? alan, CancellationToken ct = default)
        {
            var kayit = id is { } i
                ? await _db.Parolalar.AsNoTracking()
                    .Where(p => p.Id == i)
                    .Select(p => new { p.Tur, p.FirmaAdi })
                    .FirstOrDefaultAsync(ct)
                : null;

            _db.ParolaErisimKayitlari.Add(new ParolaErisimKaydi
            {
                ParolaId = id ?? 0,
                Tur = kayit?.Tur ?? default,
                FirmaAdi = kayit?.FirmaAdi ?? string.Empty,
                Alan = Kirp(Temiz(alan), 100),
                Islem = ParolaIslemleri.Reddedildi,
                KullaniciId = int.TryParse(_kullanici?.UserId, out var k) ? k : null,
                Kullanici = Kirp(_kullanici?.UserName, 100),
                Tarih = Simdi()
            });
            await _db.SaveChangesAsync(ct);
        }

        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Gövdeyi doğrular ve kayda uygular; değişen şifre alanlarının ADLARINI döndürür.</summary>
        private async Task<List<string>> UygulaAsync(Parola kayit, ParolaKaydetDto dto, bool yeni, CancellationToken ct)
        {
            // Firma
            if (dto.FirmaId is { } firmaId)
            {
                var firma = await _db.Firmalar.AsNoTracking()
                                .Where(f => f.Id == firmaId)
                                .Select(f => new { f.Id, f.KisaAd, f.Unvan, f.Aktif })
                                .FirstOrDefaultAsync(ct)
                            ?? throw new ParolaKuralException("firmaId", "Firma bulunamadı.");

                // Pasif firma yeni kayıtta ve başka firmaya taşımada seçilemez; mevcut kaydın kendi pasif firması kalabilir.
                if (!firma.Aktif && (yeni || kayit.FirmaId != firma.Id))
                    throw new ParolaKuralException("firmaId", "Firma pasif; Firmalarım'daki aktif firmalardan birini seçin.");

                kayit.FirmaId = firma.Id;
                kayit.FirmaAdi = Kirp(FirmaAdiOf(firma.KisaAd, firma.Unvan), 200)!;
            }
            else
            {
                var elle = Temiz(dto.FirmaAdi)
                           ?? throw new ParolaKuralException("firmaAdi", "Firma seçin ya da firma adını yazın.");
                if (elle.Length > 200) throw new ParolaKuralException("firmaAdi", "Firma adı en çok 200 karakter.");
                kayit.FirmaId = null;
                kayit.FirmaAdi = elle;
            }

            // Kullanıcı adı / kodu
            kayit.KullaniciAdi = Temiz(dto.KullaniciAdi)
                                 ?? throw new ParolaKuralException("kullaniciAdi", kayit.Tur switch
                                 {
                                     ParolaTuru.DijitalVergiDairesi => "Kullanıcı kodu zorunlu.",
                                     ParolaTuru.Sgk => "Kullanıcı adı (TCKN) zorunlu.",
                                     _ => "Kullanıcı adı zorunlu."
                                 });
            if (kayit.KullaniciAdi.Length > 100) throw new ParolaKuralException("kullaniciAdi", "En çok 100 karakter.");

            var not = Temiz(dto.Not);
            if (not is { Length: > 1000 }) throw new ParolaKuralException("not", "Not en çok 1000 karakter.");
            kayit.Not = not;

            // Türe özgü alanlar (başka türünkiler yok sayılır ve temizlenir)
            kayit.Entegrator = null;
            kayit.PortalAdresi = null;
            kayit.Sube = null;
            kayit.KullaniciKoduEk = null;
            kayit.TekillikAnahtari = null;

            switch (kayit.Tur)
            {
                case ParolaTuru.Entegrator:
                    var ent = Temiz(dto.Entegrator) ?? throw new ParolaKuralException("entegrator", "Entegratör seçin.");
                    kayit.Entegrator = ParolaKurallari.Entegratorler.FirstOrDefault(e => string.Equals(e, ent, StringComparison.OrdinalIgnoreCase))
                                       ?? throw new ParolaKuralException("entegrator", "Entegratör listede yok; listede değilse 'Diğer'i seçin.");
                    kayit.PortalAdresi = PortalDogrula(dto.PortalAdresi);
                    break;

                case ParolaTuru.Sgk:
                    kayit.Sube = Temiz(dto.Sube);
                    if (kayit.Sube is { Length: > 100 }) throw new ParolaKuralException("sube", "Şube en çok 100 karakter.");
                    kayit.KullaniciKoduEk = Temiz(dto.KullaniciKoduEk);
                    if (kayit.KullaniciKoduEk is { Length: > 20 }) throw new ParolaKuralException("kullaniciKoduEk", "Kullanıcı kodu eki en çok 20 karakter.");
                    kayit.TekillikAnahtari = SgkAnahtari(kayit);

                    var anahtar = kayit.TekillikAnahtari;
                    var ayniVar = await _db.Parolalar.AsNoTracking()
                        .AnyAsync(p => p.TekillikAnahtari == anahtar && p.Id != kayit.Id, ct);
                    if (ayniVar)
                        throw new ParolaCakismaException(
                            $"{kayit.FirmaAdi}{(kayit.Sube is null ? "" : " · " + kayit.Sube)} için SGK kaydı zaten var. " +
                            "Şube adını yazın ya da mevcut kaydı düzenleyin.");
                    break;
            }

            // Şifreler: yenide hepsi zorunlu; düzenlemede boş bırakılan değişmez.
            var degisen = new List<string>();
            foreach (var alan in ParolaKurallari.SifreAlanlari(kayit.Tur))
            {
                var yeniDeger = DuzDeger(dto, alan);
                if (string.IsNullOrEmpty(yeniDeger))
                {
                    if (yeni) throw new ParolaKuralException(alan, $"{ParolaKurallari.AlanAdi(alan)} zorunlu.");
                    continue;
                }
                if (yeniDeger.Length > AzamiSifre)
                    throw new ParolaKuralException(alan, $"{ParolaKurallari.AlanAdi(alan)} en çok {AzamiSifre} karakter.");

                SifreliYaz(kayit, alan, _sifreleyici.Sifrele(yeniDeger));
                degisen.Add(alan);
            }
            return degisen;
        }

        private static string? PortalDogrula(string? deger)
        {
            var url = Temiz(deger);
            if (url is null) return null;
            if (url.Length > 500) throw new ParolaKuralException("portalAdresi", "Portal adresi en çok 500 karakter.");
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new ParolaKuralException("portalAdresi", "Portal adresi http:// ya da https:// ile başlayan bir adres olmalı.");
            return url;
        }

        /// <summary>SGK tekillik anahtarı: firma (Id ya da elle yazılan ad) + şube; büyük/küçük harf ve boşluk farkı sayılmaz.</summary>
        public static string SgkAnahtari(Parola p)
        {
            var firma = p.FirmaId is { } id ? $"F{id}" : "E:" + Normal(p.FirmaAdi);
            return Kirp($"{firma}|{Normal(p.Sube)}", 320)!;
        }

        private static string Normal(string? s) => Bosluklar.Replace((s ?? string.Empty).Trim(), " ").ToUpper(Tr);

        private static string? DuzDeger(ParolaKaydetDto dto, string alan) => alan switch
        {
            ParolaKurallari.AlanSifre => dto.Sifre,
            ParolaKurallari.AlanParola => dto.Parola,
            ParolaKurallari.AlanSistemSifresi => dto.SistemSifresi,
            ParolaKurallari.AlanIsyeriSifresi => dto.IsyeriSifresi,
            _ => null
        };

        private static string? SifreliDeger(Parola p, string alan) => alan switch
        {
            ParolaKurallari.AlanSifre => p.SifreSifreli,
            ParolaKurallari.AlanParola => p.ParolaSifreli,
            ParolaKurallari.AlanSistemSifresi => p.SistemSifresiSifreli,
            ParolaKurallari.AlanIsyeriSifresi => p.IsyeriSifresiSifreli,
            _ => null
        };

        private static void SifreliYaz(Parola p, string alan, string sifreli)
        {
            switch (alan)
            {
                case ParolaKurallari.AlanSifre: p.SifreSifreli = sifreli; break;
                case ParolaKurallari.AlanParola: p.ParolaSifreli = sifreli; break;
                case ParolaKurallari.AlanSistemSifresi: p.SistemSifresiSifreli = sifreli; break;
                case ParolaKurallari.AlanIsyeriSifresi: p.IsyeriSifresiSifreli = sifreli; break;
            }
        }

        private static ParolaTuru TurDogrula(ParolaTuru tur)
            => Enum.IsDefined(tur) ? tur : throw new ParolaKuralException("tur", "Geçersiz tür.");

        private static ParolaDto Dto(Parola k, IReadOnlyDictionary<int, (string Ad, bool Aktif)> firmalar)
        {
            (string Ad, bool Aktif)? bagli = k.FirmaId is { } f && firmalar.TryGetValue(f, out var firma) ? firma : null;
            return new ParolaDto
            {
                Id = k.Id,
                Tur = k.Tur,
                FirmaId = k.FirmaId,
                FirmaAdi = bagli?.Ad ?? k.FirmaAdi,
                FirmaPasif = bagli is { Aktif: false },
                ElleYazilan = k.FirmaId is null,
                Entegrator = k.Entegrator,
                PortalAdresi = k.PortalAdresi,
                KullaniciAdi = k.KullaniciAdi,
                KullaniciKoduEk = k.KullaniciKoduEk,
                Sube = k.Sube,
                Not = k.Not,
                SifreDolu = !string.IsNullOrEmpty(k.SifreSifreli),
                ParolaDolu = !string.IsNullOrEmpty(k.ParolaSifreli),
                SistemSifresiDolu = !string.IsNullOrEmpty(k.SistemSifresiSifreli),
                IsyeriSifresiDolu = !string.IsNullOrEmpty(k.IsyeriSifresiSifreli),
                GuncellemeTarihi = k.GuncellemeTarihi,
                GuncelleyenKullanici = k.GuncelleyenKullanici
            };
        }

        private async Task<Dictionary<int, (string Ad, bool Aktif)>> FirmalarAsync(IEnumerable<int> idler, CancellationToken ct)
        {
            var liste = idler.Distinct().ToList();
            if (liste.Count == 0) return new();
            var firmalar = await _db.Firmalar.AsNoTracking()
                .Where(f => liste.Contains(f.Id))
                .Select(f => new { f.Id, f.KisaAd, f.Unvan, f.Aktif })
                .ToListAsync(ct);
            return firmalar.ToDictionary(f => f.Id, f => (FirmaAdiOf(f.KisaAd, f.Unvan), f.Aktif));
        }

        private static string FirmaAdiOf(string? kisaAd, string? unvan)
            => string.IsNullOrWhiteSpace(kisaAd) ? (unvan ?? string.Empty).Trim() : kisaAd.Trim();

        private ParolaErisimKaydi Erisim(Parola k, string islem, string? alan, DateTime tarih) => new()
        {
            ParolaId = k.Id,
            Tur = k.Tur,
            FirmaAdi = k.FirmaAdi,
            Alan = alan,
            Islem = islem,
            KullaniciId = int.TryParse(_kullanici?.UserId, out var id) ? id : null,
            Kullanici = Kirp(_kullanici?.UserName, 100),
            Tarih = tarih
        };

        private void Damgala(Parola k, DateTime simdi)
        {
            k.GuncellemeTarihi = simdi;
            k.GuncelleyenKullaniciId = int.TryParse(_kullanici?.UserId, out var id) ? id : null;
            k.GuncelleyenKullanici = Kirp(_kullanici?.UserName, 100);
        }

        private DateTime Simdi() => _saat.GetLocalNow().DateTime;

        private async Task IslemIcindeAsync(Func<Task> is_, CancellationToken ct)
        {
            if (!_db.Database.IsRelational())
            {
                await is_();
                return;
            }
            var strateji = _db.Database.CreateExecutionStrategy();
            await strateji.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(ct);
                await is_();
                await tx.CommitAsync(ct);
            });
        }

        private static string? Temiz(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static string? Kirp(string? s, int n) => s is null ? null : s.Length <= n ? s : s[..n];
    }
}
