using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;
using CatalogService.Api.Infrastructure.Auth;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Yapilacaklar.Services
{
    public interface IIsTarifiService
    {
        /// <summary>Nasıl yapılır sheet'inin sol listesi: iş başına bir satır (tarif grubu).</summary>
        Task<List<IsTarifiOzetDto>> ListeAsync(CancellationToken ct = default);

        Task<IsTarifiDetayDto> DetayAsync(string anahtar, CancellationToken ct = default);

        /// <summary>
        /// Tarife bağlanmamış kayıtlardan tarif üretir (<see cref="IsTarifiKurucu.Planla"/>):
        /// <paramref name="anahtar"/> boşsa bütün gruplar (seed), doluysa yalnız o grup ("Tarif oluştur").
        /// Farklı metinli kayıt BAĞLANMAZ, rapora girer.
        /// </summary>
        /// <param name="form">Prompt 15 "+ Tarif yaz": doluysa tarif bu metinden yazılır (sıfırdan tarif); boşsa
        /// eskisi gibi en dolu firma metninden. Metni BOŞ form reddedilir ("tarif boş yazılamaz").</param>
        /// <param name="firmaId">Yasal işte hiç prosedür satırı yoksa satırı açılacak firma (sayfanın seçimi).</param>
        Task<TarifUretimRaporuDto> UretAsync(string? anahtar, CancellationToken ct = default,
                                             IsTarifiKaydetDto? form = null, int? firmaId = null);

        Task<IsTarifiDto> GuncelleAsync(int tarifId, IsTarifiKaydetDto dto, CancellationToken ct = default);

        /// <summary>
        /// "Bu tarifi kullan": kullanıcının kararıyla kaydı tarife bağlar. Kaydın kendi metni SİLİNMEZ;
        /// tarifle farklıysa "Bu firmada farklı" olarak görünmeye devam eder.
        /// </summary>
        Task BaglaAsync(int tarifId, int firmaIsiId, CancellationToken ct = default);

        /// <summary>
        /// "N kaydın hepsini bağla": yalnız metni BOŞ ya da tarifle AYNI, bağlanmamış kayıtlar. Metni farklı
        /// kayıt burada da bağlanmaz (tek tek, metinler yan yana görülerek karar verilir). Otomatik değil:
        /// kullanıcı firmaları gördükten sonra çağırır.
        /// </summary>
        /// <returns>Bağlanan kayıt sayısı.</returns>
        Task<int> HepsiniBaglaAsync(int tarifId, IReadOnlyCollection<int> firmaIsiIdleri, CancellationToken ct = default);

        Task<FirmaIsiEkiDto> EkEkleAsync(int tarifId, FirmaIsiEkiOlusturDto dto, CancellationToken ct = default);

        /// <returns>Silinen ekin FileApi Id'si (istemci oradan da siler).</returns>
        Task<int> EkSilAsync(int tarifId, int ekId, CancellationToken ct = default);
    }

    /// <summary>
    /// İş tarifleri (Prompt 14). Tarif İŞ BAZINDADIR; firma <see cref="FirmaIsi.IsTarifiId"/> ile bağlanır.
    /// Grup = <see cref="IsTarifiKurucu.Anahtar"/> (yasalda kod + tekrar, özelde normalize başlık).
    /// Grubun tarifi, üyelerinin bağlı olduğu tarif.
    ///
    /// Anasayfadaki prosedür paneli (Prompt 8) DEĞİŞMEDİ: orada firmanın kendi metni görünmeye devam eder.
    /// </summary>
    public class IsTarifiService : IIsTarifiService
    {
        private readonly CatalogContext _db;
        private readonly TimeProvider _saat;
        private readonly IHttpCurrentUser? _kullanici;
        private readonly IYapilacaklarService? _isler;

        /// <param name="isler">Yasal işin prosedür satırını MEVCUT akışla açmak için (Prompt 15 "+ Tarif yaz").</param>
        public IsTarifiService(CatalogContext db, TimeProvider saat, IHttpCurrentUser? kullanici = null,
                               IYapilacaklarService? isler = null)
        {
            _db = db;
            _saat = saat;
            _kullanici = kullanici;
            _isler = isler;
        }

        private sealed record Grup(string Anahtar, string Ad, IsKaynagi Tip, string? Kod, IsTekrari Tekrar,
                                   List<FirmaIsi> Uyeler, IsTarifi? Tarif,
                                   List<YapilacaklarKurucu.FirmaGirdisi> YasalFirmalar);

        public async Task<List<IsTarifiOzetDto>> ListeAsync(CancellationToken ct = default)
        {
            var (gruplar, firmalar) = await GruplarAsync(ct);
            var aktif = firmalar.Select(f => f.FirmaId).ToHashSet();
            var tr = StringComparer.Create(IsTakvimHesabi.Tr, true);

            return gruplar
                .Select(g => new IsTarifiOzetDto
                {
                    Anahtar = g.Anahtar,
                    Ad = g.Ad,
                    KaynakTip = g.Tip,
                    MukellefiyetKodu = g.Kod,
                    Tekrar = g.Tekrar,
                    FirmaIdleri = (g.Tip == IsKaynagi.Yasal
                        ? g.YasalFirmalar.Select(f => f.FirmaId)
                        : g.Uyeler.Where(i => i.Aktif && aktif.Contains(i.FirmaId)).Select(i => i.FirmaId)).Distinct().ToList(),
                    FirmaSayisi = g.Tip == IsKaynagi.Yasal
                        ? g.YasalFirmalar.Count
                        : g.Uyeler.Where(i => i.Aktif && aktif.Contains(i.FirmaId)).Select(i => i.FirmaId).Distinct().Count(),
                    TarifId = g.Tarif?.Id,
                    EkSayisi = g.Tarif?.Ekler.Count ?? 0,
                    FarkliSayisi = g.Tarif is { } t
                        ? g.Uyeler.Count(i => aktif.Contains(i.FirmaId) && Baglanti(i, t, g.Tip) == TarifBaglantisi.Farkli)
                        : 0,
                    BaglanabilirSayisi = g.Tarif is { } t2
                        ? g.Uyeler.Count(i => aktif.Contains(i.FirmaId) && Baglanti(i, t2, g.Tip) == TarifBaglantisi.Bagsiz)
                        : 0
                })
                .OrderBy(o => o.KaynakTip)
                .ThenBy(o => o.Ad, tr)
                .ToList();
        }

        public async Task<IsTarifiDetayDto> DetayAsync(string anahtar, CancellationToken ct = default)
        {
            var (gruplar, firmalar) = await GruplarAsync(ct);
            var g = gruplar.FirstOrDefault(x => x.Anahtar == anahtar?.Trim())
                    ?? throw new KeyNotFoundException("Bu iş bulunamadı.");

            var sistemAdlari = await _db.Sistemler.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Ad, ct);
            var firmaAdi = firmalar.ToDictionary(f => f.FirmaId, f => f.Ad);
            var t = g.Tarif;

            var dto = new IsTarifiDetayDto
            {
                Anahtar = g.Anahtar,
                Ad = g.Ad,
                KaynakTip = g.Tip,
                MukellefiyetKodu = g.Kod,
                Tekrar = g.Tekrar,
                UretilebilirMi = t is null && g.Uyeler.Any(IsTarifiKurucu.Dolu),
                Tarif = t is null ? null : new IsTarifiDto
                {
                    Id = t.Id,
                    Ad = t.Ad,
                    SistemId = t.SistemId,
                    SistemAdi = t.SistemId is { } s ? sistemAdlari.GetValueOrDefault(s) : null,
                    MenuYolu = t.MenuYolu,
                    NasilYapilir = t.NasilYapilir,
                    Adimlar = YapilacaklarKurucu.Adimlar(t.NasilYapilir),
                    Ekler = t.Ekler.OrderBy(e => e.YuklemeZamani).ThenBy(e => e.Id).Select(Ek).ToList()
                }
            };

            foreach (var i in g.Uyeler.Where(i => firmaAdi.ContainsKey(i.FirmaId)))
            {
                var baglanti = Baglanti(i, t, g.Tip);
                dto.Firmalar.Add(new IsTarifiFirmaDto
                {
                    FirmaId = i.FirmaId,
                    FirmaAdi = firmaAdi[i.FirmaId],
                    FirmaIsiId = i.Id,
                    Baglanti = baglanti,
                    MenuYolu = i.MenuYolu,
                    NasilYapilir = i.NasilYapilir,
                    Adimlar = YapilacaklarKurucu.Adimlar(i.NasilYapilir),
                    FirmayaOzel = baglanti == TarifBaglantisi.Bagli && t is not null
                                  && IsTarifiKurucu.FirmayaOzelMetinVar(i, t.MenuYolu, t.NasilYapilir),
                    SistemAdi = i.SistemId is { } fs ? sistemAdlari.GetValueOrDefault(fs) : null,
                    Alicilar = i.Alicilar.OrderBy(a => a.Sira).ThenBy(a => a.Id).Select(YapilacaklarKurucu.Alici).ToList()
                });
            }

            // Yasal işte prosedür satırı açılmamış firmalar da işi yapar: ortak tarifi kullanırlar.
            var satiriOlan = dto.Firmalar.Select(f => f.FirmaId).ToHashSet();
            foreach (var f in g.YasalFirmalar.Where(f => !satiriOlan.Contains(f.FirmaId)))
                dto.Firmalar.Add(new IsTarifiFirmaDto
                {
                    FirmaId = f.FirmaId,
                    FirmaAdi = f.Ad,
                    Baglanti = t is null ? TarifBaglantisi.Bagsiz : TarifBaglantisi.Ortak
                });

            var tr = StringComparer.Create(IsTakvimHesabi.Tr, true);
            dto.Firmalar = dto.Firmalar.OrderBy(f => f.FirmaAdi, tr).ToList();
            return dto;
        }

        /// <summary>Firmanın işle ilişkisi. Tarif yokken herkes kendi metniyle (Bagsiz).</summary>
        private static TarifBaglantisi Baglanti(FirmaIsi i, IsTarifi? t, IsKaynagi tip)
        {
            if (i.IsTarifiId is not null) return TarifBaglantisi.Bagli;
            if (t is null) return TarifBaglantisi.Bagsiz;
            if (IsTarifiKurucu.Dolu(i))
                return IsTarifiKurucu.Uyumlu(i, t.MenuYolu, t.NasilYapilir) ? TarifBaglantisi.Bagsiz : TarifBaglantisi.Farkli;
            return tip == IsKaynagi.Yasal ? TarifBaglantisi.Ortak : TarifBaglantisi.Bagsiz;
        }

        public async Task<TarifUretimRaporuDto> UretAsync(string? anahtar, CancellationToken ct = default,
                                                     IsTarifiKaydetDto? form = null, int? firmaId = null)
        {
            var hedef = string.IsNullOrWhiteSpace(anahtar) ? null : anahtar.Trim();

            var isler = await _db.FirmaIsleri.ToListAsync(ct);
            if (hedef is not null && isler.Any(i => i.IsTarifiId is not null && IsTarifiKurucu.Anahtar(i) == hedef))
                throw new YapilacaklarKuralException("anahtar", "Bu işin tarifi zaten var.");

            // Prompt 15: "+ Tarif yaz" — formdaki metinden, firmalarda metin olmasa da.
            if (hedef is not null && form is not null) return await FormdanYazAsync(hedef, form, firmaId, ct);

            var planlar = IsTarifiKurucu.Planla(isler)
                .Where(p => hedef is null || p.Anahtar == hedef)
                .ToList();
            if (hedef is not null && planlar.All(p => p.Kaynak is null))
                throw new YapilacaklarKuralException("anahtar", "Bu işin hiçbir firmada metni yok; tarif boş yazılamaz.");

            var firmaAdlari = (await _db.Firmalar.AsNoTracking().ToListAsync(ct))
                .ToDictionary(f => f.Id, Anasayfa.Services.FirmaPaneliKurucu.Ad);

            var rapor = new TarifUretimRaporuDto { GrupSayisi = planlar.Count };
            var simdi = _saat.GetUtcNow().UtcDateTime;
            var uretilen = new List<(IsTarifi Tarif, IsTarifiKurucu.GrupPlani Plan)>();

            foreach (var plan in planlar.Where(p => p.Kaynak is not null))
            {
                var tarif = IsTarifiKurucu.Tarif(plan.Kaynak!, simdi);
                _db.IsTarifleri.Add(tarif);
                uretilen.Add((tarif, plan));
            }
            await _db.SaveChangesAsync(ct);   // Id'ler bağlamadan önce gerekli

            foreach (var (tarif, plan) in uretilen)
            {
                foreach (var isi in plan.Baglanacaklar) isi.IsTarifiId = tarif.Id;
                rapor.BaglananKayit += plan.Baglanacaklar.Count;

                if (plan.Farklilar.Count > 0)
                    rapor.Baglanmayanlar.Add(new TarifFarkiDto
                    {
                        Baslik = plan.Baslik,
                        Firmalar = plan.Farklilar.Select(i => firmaAdlari.GetValueOrDefault(i.FirmaId, $"#{i.FirmaId}")).Distinct().ToList(),
                        KayitSayisi = plan.Farklilar.Count,
                        FarkliMetinSayisi = plan.FarkliMetinSayisi
                    });
            }
            rapor.UretilenTarif = uretilen.Count;
            await _db.SaveChangesAsync(ct);

            return rapor;
        }

        /// <summary>
        /// "+ Tarif yaz" (Prompt 15): tarif FORMDAKİ metinden. Grubun bağlanmamış kayıtlarından metni boş ya da
        /// bu metinle aynı olanlar bağlanır; farklı metinli kayıt yine BAĞLANMAZ (sessiz birleştirme yok, Prompt 14).
        /// Yasal işte hiç prosedür satırı yoksa satır MEVCUT "yasal prosedür aç" akışıyla açılır — tarifin
        /// bağlanacağı yer olsun diye; kullanıcıya ikinci adım gösterilmez.
        /// </summary>
        private async Task<TarifUretimRaporuDto> FormdanYazAsync(string hedef, IsTarifiKaydetDto form, int? firmaId, CancellationToken ct)
        {
            var (ad, menu, nasil) = await FormDogrulaAsync(form, adZorunlu: false, ct);
            if (menu is null && nasil is null)
                throw new YapilacaklarKuralException(nameof(form.NasilYapilir), "Tarif boş yazılamaz: menü yolu ya da adımlardan en az biri dolu olmalı.");

            var uyeler = (await _db.FirmaIsleri.Where(i => i.IsTarifiId == null).ToListAsync(ct))
                .Where(i => IsTarifiKurucu.Anahtar(i) == hedef).ToList();

            if (uyeler.Count == 0 && YasalAnahtarCoz(hedef) is { } yasal)
            {
                if (_isler is null) throw new InvalidOperationException("Yasal prosedür akışı bağlı değil.");
                var firmalar = await YapilacaklarService.FirmaGirdileriAsync(_db, null, ct);
                var firma = firmalar.FirstOrDefault(f => f.FirmaId == firmaId && f.Kodlar.Contains(yasal.Kod))
                            ?? firmalar.FirstOrDefault(f => f.Kodlar.Contains(yasal.Kod))
                            ?? throw new YapilacaklarKuralException("anahtar", "Bu yasal iş hiçbir aktif firmada geçerli değil.");
                await _isler.YasalProsedurAcAsync(firma.FirmaId, yasal.Kod, yasal.Tekrar, ct);   // MEVCUT akış
                uyeler = (await _db.FirmaIsleri.Where(i => i.IsTarifiId == null).ToListAsync(ct))
                    .Where(i => IsTarifiKurucu.Anahtar(i) == hedef).ToList();
            }
            if (uyeler.Count == 0) throw new KeyNotFoundException("Bu iş bulunamadı.");

            var ilk = uyeler.OrderBy(i => i.Id).First();
            var baslik = ad ?? ilk.Baslik.Trim();
            var tarif = new IsTarifi
            {
                Ad = baslik.Length > 200 ? baslik[..200] : baslik,
                SistemId = form.SistemId,
                MenuYolu = menu,
                NasilYapilir = nasil,
                OlusturmaZamani = _saat.GetUtcNow().UtcDateTime
            };
            _db.IsTarifleri.Add(tarif);
            await _db.SaveChangesAsync(ct);

            var bagla = uyeler.Where(i => IsTarifiKurucu.Uyumlu(i, menu, nasil)).ToList();
            var farkli = uyeler.Except(bagla).ToList();
            foreach (var i in bagla) i.IsTarifiId = tarif.Id;
            await _db.SaveChangesAsync(ct);

            var firmaAdlari = (await _db.Firmalar.AsNoTracking().ToListAsync(ct)).ToDictionary(f => f.Id, Anasayfa.Services.FirmaPaneliKurucu.Ad);
            var rapor = new TarifUretimRaporuDto { GrupSayisi = 1, UretilenTarif = 1, BaglananKayit = bagla.Count };
            if (farkli.Count > 0)
                rapor.Baglanmayanlar.Add(new TarifFarkiDto
                {
                    Baslik = tarif.Ad,
                    Firmalar = farkli.Select(i => firmaAdlari.GetValueOrDefault(i.FirmaId, $"#{i.FirmaId}")).Distinct().ToList(),
                    KayitSayisi = farkli.Count,
                    FarkliMetinSayisi = farkli.Select(i => (IsTarifiKurucu.Normalize(i.MenuYolu), IsTarifiKurucu.Normalize(i.NasilYapilir))).Distinct().Count()
                });
            return rapor;
        }

        /// <summary>"y-0015-2" → (0015, Aylik); özel anahtarda null.</summary>
        private static (string Kod, IsTekrari Tekrar)? YasalAnahtarCoz(string anahtar)
        {
            var p = anahtar.Split('-');
            return p.Length == 3 && p[0] == "y" && byte.TryParse(p[2], out var t) && Enum.IsDefined(typeof(IsTekrari), t)
                ? (p[1], (IsTekrari)t) : null;
        }

        /// <summary>Tarif formunun doğrulaması — oluşturma ve düzenleme AYNI kural.</summary>
        private async Task<(string? Ad, string? Menu, string? Nasil)> FormDogrulaAsync(IsTarifiKaydetDto dto, bool adZorunlu, CancellationToken ct)
        {
            var ad = Bos(dto.Ad);
            if (ad is null && adZorunlu) throw new YapilacaklarKuralException(nameof(dto.Ad), "Tarifin adı boş olamaz.");
            if (ad is { Length: > 200 }) throw new YapilacaklarKuralException(nameof(dto.Ad), "Ad en çok 200 karakter olabilir.");
            var menu = Bos(dto.MenuYolu);
            if (menu is { Length: > 300 }) throw new YapilacaklarKuralException(nameof(dto.MenuYolu), "Menü yolu en çok 300 karakter olabilir.");
            var nasil = Bos(dto.NasilYapilir?.Replace("\r\n", "\n"));
            if (nasil is { Length: > 4000 }) throw new YapilacaklarKuralException(nameof(dto.NasilYapilir), "Tarif en çok 4000 karakter olabilir.");
            if (dto.SistemId is { } sid && !await _db.Sistemler.AnyAsync(s => s.Id == sid, ct))
                throw new YapilacaklarKuralException(nameof(dto.SistemId), "Sistem bulunamadı.");
            return (ad, menu, nasil);
        }

        public async Task<IsTarifiDto> GuncelleAsync(int tarifId, IsTarifiKaydetDto dto, CancellationToken ct = default)
        {
            var t = await _db.IsTarifleri.Include(x => x.Ekler).FirstOrDefaultAsync(x => x.Id == tarifId, ct)
                    ?? throw new KeyNotFoundException("Tarif bulunamadı.");

            var (adV, menu, nasil) = await FormDogrulaAsync(dto, adZorunlu: true, ct);
            var ad = adV!;

            t.Ad = ad;
            t.SistemId = dto.SistemId;
            t.MenuYolu = menu;
            t.NasilYapilir = nasil;
            await _db.SaveChangesAsync(ct);

            var sistemAdi = t.SistemId is { } s ? await _db.Sistemler.Where(x => x.Id == s).Select(x => x.Ad).FirstOrDefaultAsync(ct) : null;
            return new IsTarifiDto
            {
                Id = t.Id, Ad = t.Ad, SistemId = t.SistemId, SistemAdi = sistemAdi, MenuYolu = t.MenuYolu,
                NasilYapilir = t.NasilYapilir, Adimlar = YapilacaklarKurucu.Adimlar(t.NasilYapilir),
                Ekler = t.Ekler.OrderBy(e => e.YuklemeZamani).ThenBy(e => e.Id).Select(Ek).ToList()
            };
        }

        public async Task BaglaAsync(int tarifId, int firmaIsiId, CancellationToken ct = default)
        {
            var isi = await _db.FirmaIsleri.FirstOrDefaultAsync(i => i.Id == firmaIsiId, ct)
                      ?? throw new KeyNotFoundException("İş bulunamadı.");
            if (!await _db.IsTarifleri.AnyAsync(t => t.Id == tarifId, ct))
                throw new KeyNotFoundException("Tarif bulunamadı.");

            // Yalnız AYNI grubun tarifine: başka bir işin tarifine bağlamak yanlış birleştirme olur.
            var anahtar = IsTarifiKurucu.Anahtar(isi);
            var grubunTarifleri = (await _db.FirmaIsleri.AsNoTracking().Where(i => i.IsTarifiId == tarifId).ToListAsync(ct))
                .Select(IsTarifiKurucu.Anahtar);
            if (!grubunTarifleri.Contains(anahtar))
                throw new YapilacaklarKuralException("tarifId", "Bu tarif bu işe ait değil.");

            isi.IsTarifiId = tarifId;   // kendi metni SİLİNMEZ — farklıysa "Bu firmada farklı"
            await _db.SaveChangesAsync(ct);
        }

        public async Task<int> HepsiniBaglaAsync(int tarifId, IReadOnlyCollection<int> firmaIsiIdleri, CancellationToken ct = default)
        {
            var tarif = await _db.IsTarifleri.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tarifId, ct)
                        ?? throw new KeyNotFoundException("Tarif bulunamadı.");
            var grubunAnahtarlari = (await _db.FirmaIsleri.AsNoTracking().Where(i => i.IsTarifiId == tarifId).ToListAsync(ct))
                .Select(IsTarifiKurucu.Anahtar).ToHashSet();

            var isler = await _db.FirmaIsleri.Where(i => firmaIsiIdleri.Contains(i.Id)).ToListAsync(ct);
            var baglanan = 0;
            foreach (var isi in isler)
            {
                if (isi.IsTarifiId is not null) continue;
                if (!grubunAnahtarlari.Contains(IsTarifiKurucu.Anahtar(isi)))
                    throw new YapilacaklarKuralException("tarifId", $"\"{isi.Baslik}\" bu tarifin işi değil.");
                // Metni farklı kayıt toplu bağlanmaz — sessiz birleştirme yok.
                if (!IsTarifiKurucu.Uyumlu(isi, tarif.MenuYolu, tarif.NasilYapilir)) continue;
                isi.IsTarifiId = tarifId;
                baglanan++;
            }
            await _db.SaveChangesAsync(ct);
            return baglanan;
        }

        /// <summary>
        /// Bir firmanın işinin okuduğu tarif — anasayfa prosedür paneli ve Nasıl yapılır AYNI kuralı kullanır:
        /// bağlıysa kendi tarifi; yasal işte satırı yoksa ya da metni boşsa grubun ortak tarifi; yoksa tarif yok.
        /// </summary>
        public static async Task<IsTarifi?> TarifCozAsync(CatalogContext db, FirmaIsi? isi, string? yasalKod, IsTekrari tekrar,
                                                         CancellationToken ct)
        {
            if (isi?.IsTarifiId is { } id)
                return await db.IsTarifleri.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
            if (yasalKod is null || (isi is not null && IsTarifiKurucu.Dolu(isi))) return null;

            var grupTarifi = await db.FirmaIsleri.AsNoTracking()
                .Where(i => i.YasalMukellefiyetKodu == yasalKod && i.Tekrar == tekrar && i.IsTarifiId != null)
                .GroupBy(i => i.IsTarifiId)
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
                .Select(g => g.Key)
                .FirstOrDefaultAsync(ct);
            return grupTarifi is { } gid ? await db.IsTarifleri.AsNoTracking().FirstOrDefaultAsync(t => t.Id == gid, ct) : null;
        }

        public async Task<FirmaIsiEkiDto> EkEkleAsync(int tarifId, FirmaIsiEkiOlusturDto dto, CancellationToken ct = default)
        {
            // Prosedür ve dönem ekiyle AYNI kural: türler ve Belgeler kartının boyut sınırı.
            var (ad, contentType) = YapilacaklarService.EkDogrula(dto.FileId, dto.DosyaAdi, dto.ContentType, dto.Boyut);
            if (!await _db.IsTarifleri.AnyAsync(t => t.Id == tarifId, ct))
                throw new KeyNotFoundException("Tarif bulunamadı.");

            var ek = new IsTarifiEki
            {
                IsTarifiId = tarifId,
                FileId = dto.FileId,
                DosyaAdi = ad,
                ContentType = contentType,
                Boyut = dto.Boyut,
                YuklemeZamani = _saat.GetUtcNow().UtcDateTime,
                YukleyenKullaniciId = KullaniciId(),
                YukleyenKullaniciAdi = _kullanici?.UserName is { Length: > 100 } k ? k[..100] : _kullanici?.UserName
            };
            _db.IsTarifiEkleri.Add(ek);
            await _db.SaveChangesAsync(ct);
            return Ek(ek);
        }

        public async Task<int> EkSilAsync(int tarifId, int ekId, CancellationToken ct = default)
        {
            var ek = await _db.IsTarifiEkleri.FirstOrDefaultAsync(e => e.Id == ekId && e.IsTarifiId == tarifId, ct)
                     ?? throw new KeyNotFoundException("Ek bulunamadı.");
            _db.IsTarifiEkleri.Remove(ek);
            await _db.SaveChangesAsync(ct);
            return ek.FileId;
        }

        /// <summary>
        /// Bütün gruplar: firma işleri (özel + yasal prosedür satırları) ve takvimdeki yasal işler (en az
        /// bir aktif firmanın mükellefiyet kodu varsa — satırı açılmamış olsa da iş vardır).
        /// </summary>
        private async Task<(List<Grup> Gruplar, List<YapilacaklarKurucu.FirmaGirdisi> Firmalar)> GruplarAsync(CancellationToken ct)
        {
            var firmalar = await YapilacaklarService.FirmaGirdileriAsync(_db, null, ct);
            var aktif = firmalar.Select(f => f.FirmaId).ToHashSet();

            var isler = (await _db.FirmaIsleri.AsNoTracking().Include(i => i.Alicilar).ToListAsync(ct))
                .Where(i => aktif.Contains(i.FirmaId))
                .ToList();
            var tarifler = await _db.IsTarifleri.AsNoTracking().Include(t => t.Ekler).ToDictionaryAsync(t => t.Id, ct);

            // Takvim serisi başına ad: en son dönemin adı.
            var seriler = (await _db.VergiTakvimi.AsNoTracking().ToListAsync(ct))
                .GroupBy(t => (t.MukellefiyetKodu, t.Tekrar))
                .ToDictionary(s => s.Key, s => s.OrderByDescending(x => x.Yil).ThenByDescending(x => x.DonemNo).First().Ad);

            var gruplar = new List<Grup>();
            var uyeler = isler.ToLookup(IsTarifiKurucu.Anahtar);

            // Yasal: takvim serisi × firma kodları; serisi takvimde olmayan prosedür satırı da kendi grubunu alır.
            var yasalAnahtarlar = seriler.Keys.Select(k => (Anahtar: IsTarifiKurucu.YasalAnahtar(k.MukellefiyetKodu, k.Tekrar), k.MukellefiyetKodu, k.Tekrar))
                .ToList();
            foreach (var (anahtar, kod, tekrar) in yasalAnahtarlar)
            {
                var yasalFirmalar = firmalar.Where(f => f.Kodlar.Contains(kod)).ToList();
                var u = uyeler[anahtar].ToList();
                if (yasalFirmalar.Count == 0 && u.Count == 0) continue;
                gruplar.Add(new Grup(anahtar, seriler[(kod, tekrar)], IsKaynagi.Yasal, kod, tekrar, u, GrupTarifi(u, tarifler), yasalFirmalar));
            }

            var yasalSet = gruplar.Select(g => g.Anahtar).ToHashSet();
            foreach (var g in uyeler.Where(g => !yasalSet.Contains(g.Key)))
            {
                var u = g.OrderBy(i => i.Id).ToList();
                var ilk = u[0];
                var tarif = GrupTarifi(u, tarifler);
                // Tekrar grup anahtarında: grubun bütün üyeleri aynı tekrarda.
                gruplar.Add(ilk.YasalMukellefiyetKodu is { } kod
                    ? new Grup(g.Key, tarif?.Ad ?? ilk.Baslik.Trim(), IsKaynagi.Yasal, kod, ilk.Tekrar, u, tarif, new())
                    : new Grup(g.Key, tarif?.Ad ?? ilk.Baslik.Trim(), IsKaynagi.Ozel, null, ilk.Tekrar, u, tarif, new()));
            }

            return (gruplar, firmalar);
        }

        /// <summary>Grubun tarifi: üyelerin en çok bağlı olduğu tarif.</summary>
        private static IsTarifi? GrupTarifi(List<FirmaIsi> uyeler, IReadOnlyDictionary<int, IsTarifi> tarifler)
            => uyeler.Where(i => i.IsTarifiId is not null)
                     .GroupBy(i => i.IsTarifiId!.Value)
                     .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
                     .Select(g => tarifler.GetValueOrDefault(g.Key))
                     .FirstOrDefault(t => t is not null);

        private static FirmaIsiEkiDto Ek(IsTarifiEki e) => new()
        {
            Id = e.Id,
            FileId = e.FileId,
            DosyaAdi = e.DosyaAdi,
            ContentType = e.ContentType,
            Boyut = e.Boyut,
            YuklemeZamani = DateTime.SpecifyKind(e.YuklemeZamani, DateTimeKind.Utc),
            YukleyenKullaniciAdi = e.YukleyenKullaniciAdi
        };

        private static string? Bos(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private string? KullaniciId()
        {
            try
            {
                return _kullanici is { IsAuthenticated: true } ? _kullanici.UserId : null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }
}
