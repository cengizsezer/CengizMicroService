using CatalogService.Api.Features.Anasayfa.Dtos;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.FirmaBilgileri.Domain;
using CatalogService.Api.Features.FirmaBilgileri.Dtos;
using CatalogService.Api.Features.FirmaBilgileri.Services;

namespace CatalogService.Api.Features.Anasayfa.Services
{
    /// <summary>
    /// Anasayfa firma panelini kurar. <b>Saf fonksiyon</b>: veritabanı bilmez, hazır
    /// listeleri alır, listeyi ve uyarıları üretir. "Bugün" dışarıdan veriliyor —
    /// takvime bağlı bir hesabın testi gerçek saate bırakılırsa bir gün kendiliğinden
    /// kırmızıya döner.
    ///
    /// Uyarılar <b>burada</b> hesaplanıyor, istemcide değil: liste satırında görünen ile
    /// sağ panelde görünen aynı kural olsun ve iki yerde ayrışmasın diye.
    /// </summary>
    public static class FirmaPaneliKurucu
    {
        /// <summary>
        /// İmza yetkisi uyarısının eşiği. Bu süre, yeni sirküler çıkarmak için gereken
        /// zamanın karşılığı — daha kısası haber vermek için geç kalır.
        /// </summary>
        public const int ImzaUyariGun = 60;

        /// <summary>
        /// Uyarı çıkaran zorunlu sicil alanları, kullanıcıya yazıldığı adlarıyla.
        ///
        /// Liste bilerek <b>kısa</b>: mükellefiyet alanları (e-fatura, e-defter, işe
        /// başlama, mükellefiyet türleri) yeni eklendiği için her firmada boş — buraya
        /// konsaydı panel ilk gün baştan aşağı uyarı gösterir ve simge anlamını yitirirdi.
        /// </summary>
        public static readonly string[] ZorunluSicilAlanlari =
            { "Vergi dairesi", "Ticaret sicil no", "MERSİS no", "Adres" };

        /// <summary>
        /// Bir firmanın uyarıları. Sıra sabit: imza → pay oranı → eksik alan; ekran ilk
        /// uyarıyı ipucu olarak gösteriyor ve en aciliyetlisi başta olsun.
        /// </summary>
        public static List<FirmaUyariDto> Uyarilar(
            Firma firma,
            FirmaSicilBilgisi? sicil,
            IReadOnlyList<FirmaOrtak> ortaklar,
            IReadOnlyList<FirmaImzaYetkilisi> yetkililer,
            DateTime bugun)
        {
            var uyarilar = new List<FirmaUyariDto>();

            if (ImzaUyarisi(yetkililer, bugun) is { } imza) uyarilar.Add(imza);

            // %100 kuralı düzenleme ekranıyla TEK kaynaktan: aynı hesap, aynı tolerans.
            var ortaklik = FirmaBilgiService.Ortaklik(ortaklar);
            if (ortaklik.PayOraniUyarisi)
            {
                uyarilar.Add(new FirmaUyariDto
                {
                    Tur = FirmaUyariTuru.PayOraniTutmuyor,
                    Mesaj = $"Ortaklık pay oranları toplamı %{ortaklik.ToplamPayOrani:0.##} — %100 değil."
                });
            }

            // Kayıtlı (otomatik) sınıflandırma kodlarla çelişiyor: değer korunur, uyarı görünür.
            if (MukellefiyetSiniflandirici.Uyusmazlik(firma, sicil?.MukellefiyetTurleri) is { } uyusmazlik)
            {
                uyarilar.Add(new FirmaUyariDto { Tur = FirmaUyariTuru.SiniflandirmaUyusmuyor, Mesaj = uyusmazlik + "." });
            }

            var eksikler = EksikSicilAlanlari(firma, sicil);
            if (eksikler.Count > 0)
            {
                uyarilar.Add(new FirmaUyariDto
                {
                    Tur = FirmaUyariTuru.EksikSicilAlani,
                    Mesaj = "Sicil bilgisi eksik: " + string.Join(", ", eksikler) + "."
                });
            }

            return uyarilar;
        }

        /// <summary>
        /// İmza yetkisi uyarısı, firmanın <b>en geç biten</b> yetkisine bakarak.
        ///
        /// Tek tek satırlara bakılmadı: süresi dolmuş yetkili kaydı silinmiyor (geçmişe
        /// dönük belge kontrolü için duruyor), dolayısıyla "herhangi biri dolmuşsa uyar"
        /// kuralı yürürlükteki sirküleri olan firmalarda da sonsuza kadar alarm verirdi.
        /// Sorulan soru şu: <i>bu firmayı bugün kim imzalayabiliyor ve ne kadar süreyle?</i>
        ///
        /// Bitişi boş olan yetkili süresiz sayılır ve uyarıyı kaldırır. Hiç yetkili
        /// yoksa bu uyarı çıkmaz — dolacak bir yetki yok; eksik veri ayrı bir konu
        /// ("imza yetkilisi yok" eksik listesinde).
        ///
        /// Kayıtlı yetkililerin <b>hepsinin</b> süresi dolmuşsa uyarı "Geçerli imza yetkilisi
        /// yok"tur: tek yetkilisi olan ve o yetkisi dolmuş firma en tehlikeli durum.
        /// </summary>
        private static FirmaUyariDto? ImzaUyarisi(IReadOnlyList<FirmaImzaYetkilisi> yetkililer, DateTime bugun)
        {
            if (yetkililer.Count == 0) return null;

            // Süresiz yetkili varsa firma imzasız kalmıyor.
            if (yetkililer.Any(y => y.YetkiBitis is null)) return null;

            var enGec = yetkililer.Max(y => y.YetkiBitis!.Value.Date);
            var kalan = (enGec - bugun.Date).Days;

            if (kalan < 0)
            {
                return new FirmaUyariDto
                {
                    Tur = FirmaUyariTuru.GecerliYetkiliYok,
                    Mesaj = $"Geçerli imza yetkilisi yok — son yetki {Math.Abs(kalan)} gün önce doldu ({enGec:dd.MM.yyyy})."
                };
            }

            if (kalan >= ImzaUyariGun) return null;

            var mesaj = kalan == 0
                    ? "İmza yetkisi bugün doluyor."
                    : $"İmza yetkisi {kalan} gün sonra doluyor ({enGec:dd.MM.yyyy}).";

            return new FirmaUyariDto { Tur = FirmaUyariTuru.ImzaYetkisiBitiyor, Mesaj = mesaj };
        }

        /// <summary>Boş olan zorunlu sicil alanları, <see cref="ZorunluSicilAlanlari"/> sırasıyla.</summary>
        public static List<string> EksikSicilAlanlari(Firma firma, FirmaSicilBilgisi? sicil)
        {
            var eksik = new List<string>();

            if (string.IsNullOrWhiteSpace(firma.VergiDairesi)) eksik.Add(ZorunluSicilAlanlari[0]);
            if (string.IsNullOrWhiteSpace(firma.TicaretSicilNo)) eksik.Add(ZorunluSicilAlanlari[1]);
            if (string.IsNullOrWhiteSpace(sicil?.MersisNo)) eksik.Add(ZorunluSicilAlanlari[2]);
            if (string.IsNullOrWhiteSpace(sicil?.Adres)) eksik.Add(ZorunluSicilAlanlari[3]);

            return eksik;
        }

        /// <summary>
        /// Sınıflandırma kartı. "0010'dan otomatik" notu için kaynağı otomatik olan alanın
        /// hangi koddan geldiği bugünkü mükellefiyet metninden okunuyor — öneri kuralıyla
        /// aynı fonksiyon, ayrı bir kopya yok.
        /// </summary>
        public static FirmaPaneliSiniflandirmaDto Siniflandirma(
            Firma firma, FirmaSicilBilgisi? sicil, IEnumerable<MizanYuklemesi> mizanlar)
        {
            var kaynakKod = MukellefiyetSiniflandirici.Oner(sicil?.MukellefiyetTurleri).KaynakKod;

            return new FirmaPaneliSiniflandirmaDto
            {
                DefterUsulu = firma.DefterUsulu,
                VergiTuru = firma.VergiTuru,
                DefterUsuluOtomatikKod = firma.DefterUsuluKaynagi == SiniflandirmaKaynagi.Otomatik ? kaynakKod : null,
                VergiTuruOtomatikKod = firma.VergiTuruKaynagi == SiniflandirmaKaynagi.Otomatik ? kaynakKod : null,
                MizanFormati = firma.MizanFormati,
                OrkaFirmaKodu = firma.OrkaFirmaKodu,
                HesapDonemi = firma.HesapDonemi,
                OzelDonemBas = firma.OzelDonemBas,
                OzelDonemBit = firma.OzelDonemBit,
                SonMizan = SonMizan(mizanlar),
                UyusmazlikMesaji = MukellefiyetSiniflandirici.Uyusmazlik(firma, sicil?.MukellefiyetTurleri),
                FirmaTipi = firma.FirmaTipi,
                SgkTesvikKademesi = firma.SgkTesvikKademesi,
                MuhtasarDonemi = firma.MuhtasarDonemi,
                SgkIsverenOrani = SgkTesvikOranlari.Oran(firma.SgkTesvikKademesi),
                SgkKademeleri = SgkTesvikOranlari.Liste
                    .Select(k => new SgkKademeSecenegiDto { Kademe = k.Kademe, Ad = k.Ad, IsverenOrani = k.IsverenOrani })
                    .ToList()
            };
        }

        /// <summary>
        /// En son yüklenen mizan (yükleme zamanına göre). Özet yalnız ağaç kaydında var;
        /// ağaçsız eski yüklemelerde özet alanları boş gelir, ekran "—" yazar.
        /// </summary>
        public static FirmaPaneliMizanDurumuDto? SonMizan(IEnumerable<MizanYuklemesi> mizanlar)
        {
            var son = mizanlar
                .OrderByDescending(m => m.YuklenmeZamani ?? DateTime.MinValue)
                .ThenByDescending(m => m.VeriYili)
                .FirstOrDefault();

            if (son is null) return null;

            return new FirmaPaneliMizanDurumuDto
            {
                Yil = son.VeriYili,
                Donem = son.Donem,
                AnalizYili = son.AnalizYili,
                YuklenmeZamani = son.YuklenmeZamani is { } z ? DateTime.SpecifyKind(z, DateTimeKind.Utc) : null,
                SatirSayisi = son.SatirSayisi,
                SeviyeSayisi = son.SeviyeSayisi,
                BorcToplam = son.BorcToplam,
                AlacakToplam = son.AlacakToplam
            };
        }

        /// <summary>
        /// Kırmızı "!": firmanın <b>hiç</b> mizanı yok. Yalnız bu.
        ///
        /// Eskiden "cari takvim yılının mizanı yok" idi; Ocak–Nisan arasında önceki yıl
        /// kapatılırken neredeyse bütün firmaları kırmızı yapıyor, rozetin en çok gerektiği
        /// ayda hiçbir şey ifade etmiyordu. Cari dönemi eksik ama geçmiş dönemi olan firma
        /// artık eksik listesinde bir madde (<see cref="CariMizanEksik"/>).
        /// </summary>
        public static bool MizanYok(IEnumerable<MizanYuklemesi> mizanlar)
            => !mizanlar.Any();

        /// <summary>Geçmiş dönem mizanı var ama cari dönemin mizanı yok.</summary>
        public static bool CariMizanEksik(IEnumerable<MizanYuklemesi> mizanlar, int cariDonemYili)
        {
            var liste = mizanlar.ToList();
            return liste.Count > 0 && !liste.Any(m => m.VeriYili == cariDonemYili);
        }

        /// <summary>
        /// Firmanın bugünü içeren hesap dönemi. Takvim yılında (ya da hesap dönemi seçilmemişse)
        /// 1 Ocak – 31 Aralık. Özel hesap döneminde OzelDonemBas alanının gün/ayından başlayan
        /// on iki aylık dönem, bugünü içerecek yıla kaydırılır.
        ///
        /// <c>Yil</c> — dönemin mizanının saklandığı yıl: özel hesap döneminde dönemin
        /// <b>kapandığı</b> takvim yılı (01.07.2025–30.06.2026 → 2026).
        /// </summary>
        public static (DateTime Bas, DateTime Bit, int Yil) CariDonem(Firma firma, DateTime bugun)
        {
            var gun = bugun.Date;

            if (firma.HesapDonemi == HesapDonemi.OzelHesapDonemi && firma.OzelDonemBas is { } ob)
            {
                var bas = GunAy(gun.Year, ob.Month, ob.Day);
                if (bas > gun) bas = GunAy(gun.Year - 1, ob.Month, ob.Day);

                var bit = bas.AddYears(1).AddDays(-1);
                return (bas, bit, OzelDonemYili(bas, bit));
            }

            return (new DateTime(gun.Year, 1, 1), new DateTime(gun.Year, 12, 31), gun.Year);
        }

        // Ozel hesap donemi yili kapanis yilina gore. Mizan dosyasinin kendi
        // adlandirmasiyla dogrulanmadi.
        // (Karar tek yerde: gerçek bir özel hesap dönemli mizan görülünce burada değişecek;
        //  yıl eşleşmezse yüklenen mizan yanlış döneme bağlanır.)
        public static int OzelDonemYili(DateTime bas, DateTime bit) => bit.Year;

        /// <summary>29 Şubat artık yıl dışına düşerse ayın son günü.</summary>
        private static DateTime GunAy(int yil, int ay, int gun)
            => new(yil, ay, Math.Min(gun, DateTime.DaysInMonth(yil, ay)));

        /// <summary>
        /// Rozete ve şeride giren "uyarı"lar: imza yetkisi, pay oranı, geçerli yetkili yok.
        /// Eksik sicil alanı uyarısı dışarıda — o zaten eksik listesinde sayılıyor.
        /// </summary>
        public static List<FirmaUyariDto> RaydakiUyarilar(IEnumerable<FirmaUyariDto> uyarilar)
            => uyarilar.Where(u => u.Tur != FirmaUyariTuru.EksikSicilAlani).ToList();

        /// <summary>Yetkisi bugün geçerli yetkili: bitişi boş (süresiz) ya da bitiş günü bugün/sonrası.</summary>
        public static int GecerliYetkiliSayisi(IEnumerable<FirmaImzaYetkilisi> yetkililer, DateTime bugun)
            => yetkililer.Count(y => y.YetkiBitis is not { } bitis || bitis.Date >= bugun.Date);

        /// <summary>Listede ve başlıkta kullanılan ad: kısa ad varsa o, yoksa unvan.</summary>
        public static string Ad(Firma firma)
            => string.IsNullOrWhiteSpace(firma.KisaAd) ? firma.Unvan : firma.KisaAd;

        /// <summary>
        /// Paneli kurar.
        ///
        /// <paramref name="seciliFirmaId"/> verilmemişse (ya da listede yoksa) <b>ilk
        /// firma</b> seçilir: ekran ilk açılışta boş sağ panelle gelmesin ve seçim için
        /// ikinci bir istek gerekmesin.
        ///
        /// Belgeler yalnız seçili firma için isteniyor; liste satırının belgeye ihtiyacı yok.
        /// </summary>
        public static FirmaPaneliDto Kur(
            DateTime bugun,
            IReadOnlyList<Firma> firmalar,
            IReadOnlyDictionary<int, FirmaSicilBilgisi> siciller,
            ILookup<int, FirmaOrtak> ortaklar,
            ILookup<int, FirmaImzaYetkilisi> yetkililer,
            IReadOnlyList<FirmaBelgesiDto> seciliBelgeler,
            int? seciliFirmaId,
            ILookup<int, MizanYuklemesi>? mizanlar = null,
            IReadOnlyList<FirmaSistemAtamasiDto>? seciliSistemler = null,
            IReadOnlySet<int>? muhasebeProgramiOlanlar = null)
        {
            muhasebeProgramiOlanlar ??= new HashSet<int>();
            var panel = new FirmaPaneliDto();
            mizanlar ??= Array.Empty<MizanYuklemesi>().ToLookup(m => m.FirmaId);

            var sirali = firmalar.OrderBy(Ad, StringComparer.CurrentCultureIgnoreCase).ToList();

            var uyariHaritasi = new Dictionary<int, List<FirmaUyariDto>>();
            var eksikHaritasi = new Dictionary<int, List<EksikAlanDto>>();

            foreach (var firma in sirali)
            {
                siciller.TryGetValue(firma.Id, out var sicil);

                var firmaUyarilari = Uyarilar(firma, sicil,
                                              ortaklar[firma.Id].ToList(),
                                              yetkililer[firma.Id].ToList(),
                                              bugun);

                uyariHaritasi[firma.Id] = firmaUyarilari;

                // Raydaki sayı ile şerit AYNI listeden (FirmaEksikBilgi).
                var cariYil = CariDonem(firma, bugun).Yil;
                var eksikler = FirmaEksikBilgi.Eksikler(new FirmaKunyeGirdisi(
                    firma, sicil, ortaklar[firma.Id].Count(), yetkililer[firma.Id].Count(),
                    CariMizanEksik(mizanlar[firma.Id], cariYil),
                    muhasebeProgramiOlanlar.Contains(firma.Id),
                    GecerliYetkiliSayisi(yetkililer[firma.Id], bugun)));
                eksikHaritasi[firma.Id] = eksikler;

                // Rozet YALNIZ gerekli alanları sayar (Prompt 12); yeni firma toleransı rengi/metni değiştirir.
                var gerekli = eksikler.Where(e => e.Gerekli).ToList();
                var raydakiUyari = RaydakiUyarilar(firmaUyarilari).Count;
                var yeni = FirmaEksikBilgi.YeniFirma(firma.CreatedAt, bugun);

                panel.Firmalar.Add(new FirmaPaneliOzetDto
                {
                    FirmaId = firma.Id,
                    Ad = Ad(firma),
                    Unvan = firma.Unvan,
                    VergiKimlikNo = firma.VergiKimlikNo,
                    Uyarilar = firmaUyarilari,
                    EksikSayisi = eksikler.Count,
                    UyariSayisi = raydakiUyari,
                    GerekliEksikSayisi = gerekli.Count,
                    GerekliEksikler = gerekli.Select(e => e.Alan).ToList(),
                    YeniFirma = yeni,
                    Rozet = FirmaEksikBilgi.Rozet(gerekli.Count, raydakiUyari, yeni),
                    MizanYok = MizanYok(mizanlar[firma.Id]),
                    MizanFormati = MizanFormatlari.Secili(firma.MizanFormati) ? firma.MizanFormati : null
                });
            }

            var secili = sirali.FirstOrDefault(f => f.Id == seciliFirmaId) ?? sirali.FirstOrDefault();
            if (secili is null) return panel;

            siciller.TryGetValue(secili.Id, out var seciliSicil);

            panel.Secili = new FirmaPaneliDetayDto
            {
                FirmaId = secili.Id,
                Ad = Ad(secili),
                Unvan = secili.Unvan,
                Uyarilar = uyariHaritasi[secili.Id],
                Eksikler = eksikHaritasi[secili.Id],
                GerekliEksikSayisi = eksikHaritasi[secili.Id].Count(e => e.Gerekli),
                YeniFirma = FirmaEksikBilgi.YeniFirma(secili.CreatedAt, bugun),

                Mukellefiyet = new FirmaMukellefiyetDto
                {
                    VergiKimlikNo = secili.VergiKimlikNo,
                    VergiDairesi = secili.VergiDairesi,
                    MukellefiyetTurleri = seciliSicil?.MukellefiyetTurleri,
                    Kodlar = MukellefiyetCipleri(seciliSicil?.MukellefiyetTurleri, out var kalanMetin),
                    KalanMetin = kalanMetin,
                    EFatura = seciliSicil?.EFatura,
                    EDefter = seciliSicil?.EDefter,
                    IseBaslamaTarihi = seciliSicil?.IseBaslamaTarihi,
                    NaceKodu = seciliSicil?.NaceKodu
                },

                Siniflandirma = Siniflandirma(secili, seciliSicil, mizanlar[secili.Id]),

                Sistemler = new FirmaPaneliSistemlerDto
                {
                    Atamalar = (seciliSistemler ?? Array.Empty<FirmaSistemAtamasiDto>()).ToList(),
                    SistemNotu = secili.SistemNotu
                },

                Sicil = new FirmaPaneliSicilDto
                {
                    TicaretSicilNo = secili.TicaretSicilNo,
                    MersisNo = seciliSicil?.MersisNo,
                    Sermaye = seciliSicil?.Sermaye,
                    SermayeParaBirimi = seciliSicil?.SermayeParaBirimi,
                    KurulusTarihi = seciliSicil?.KurulusTarihi,
                    Adres = seciliSicil?.Adres
                },

                Ortaklik = FirmaBilgiService.Ortaklik(
                    ortaklar[secili.Id].OrderBy(o => o.Sira).ThenBy(o => o.Id).ToList()),

                Yetkililer = yetkililer[secili.Id]
                    .OrderBy(y => y.Sira).ThenBy(y => y.Id)
                    .Select(y => Yetkili(y, bugun))
                    .ToList(),

                YetkiRozeti = YetkiRozeti(yetkililer[secili.Id], bugun),

                Takip = new FirmaPaneliTakipDto
                {
                    SorumluKullaniciId = secili.SorumluKullaniciId,
                    SorumluKullaniciAdi = secili.SorumluKullaniciAdi,
                    Donemler = Donemler(mizanlar[secili.Id], CariDonem(secili, bugun).Yil)
                },

                Belgeler = seciliBelgeler.ToList()
            };

            return panel;
        }

        /// <summary>
        /// Mükellefiyet kartının kod çipleri: ayıklanan kodlar (sınıflandırmanın kaynağı olan
        /// 0010/0001 işaretli) ve kod sayılamayan kalan metin — kalan sessizce yutulmaz.
        /// </summary>
        public static List<MukellefiyetKoduDto> MukellefiyetCipleri(string? metin, out string? kalan)
        {
            var sonuc = MukellefiyetSiniflandirici.Ayikla(metin);
            var kaynak = MukellefiyetSiniflandirici.Oner(metin).KaynakKod;

            kalan = sonuc.Kalan;
            return sonuc.Kodlar.Select(k => new MukellefiyetKoduDto
            {
                Kod = k.Kod,
                Ad = k.Ad,
                KisaAd = MukellefiyetSiniflandirici.KisaAd(k),
                SiniflandirmaKaynagi = k.Kod == kaynak
            }).ToList();
        }

        /// <summary>Rozet eşikleri (gün): bitişe bundan az kalan yetki sayılır / kritik sayılır.</summary>
        public const int YetkiRozetGun = 365;
        public const int YetkiKritikGun = 90;

        /// <summary>
        /// İmza yetkilileri başlık rozeti. Süresi <b>dolmuş</b> kayıt sayılmaz: silinmeyip
        /// geçmiş için duran eski sirküler rozeti sonsuza kadar kırmızı tutardı. Süresiz
        /// yetkili de sayılmaz.
        /// </summary>
        public static FirmaYetkiRozetiDto? YetkiRozeti(IEnumerable<FirmaImzaYetkilisi> yetkililer, DateTime bugun)
        {
            var liste = yetkililer.ToList();

            // Kayıtlı yetkililerin hepsi dolmuşsa rozet "Geçerli yetkili yok" (kritik).
            if (liste.Count > 0 && !liste.Any(y => y.YetkiBitis is null || y.YetkiBitis.Value.Date >= bugun.Date))
                return new FirmaYetkiRozetiDto { Sayi = 0, Kritik = true, GecerliYok = true };

            var kalanlar = liste
                .Where(y => y.YetkiBitis is not null)
                .Select(y => (y.YetkiBitis!.Value.Date - bugun.Date).Days)
                .Where(k => k >= 0 && k < YetkiRozetGun)
                .ToList();

            return kalanlar.Count == 0
                ? null
                : new FirmaYetkiRozetiDto { Sayi = kalanlar.Count, Kritik = kalanlar.Any(k => k < YetkiKritikGun) };
        }

        /// <summary>
        /// Takip kartındaki dönem çipleri: mizanı yüklü yıllar (yeni → eski). Cari yıl
        /// yüklü değilse başa gri (yüklü değil) çip olarak eklenir.
        /// </summary>
        public static List<FirmaDonemCipDto> Donemler(IEnumerable<MizanYuklemesi> mizanlar, int cariYil)
        {
            var yillar = mizanlar.Select(m => m.VeriYili).Distinct().OrderByDescending(y => y)
                .Select(y => new FirmaDonemCipDto { Yil = y, Yuklu = true })
                .ToList();

            if (!yillar.Any(d => d.Yil == cariYil))
                yillar.Add(new FirmaDonemCipDto { Yil = cariYil, Yuklu = false });

            return yillar.OrderByDescending(d => d.Yil).ToList();
        }

        /// <summary>
        /// Yetkili satırı. <see cref="FirmaPaneliYetkiliDto.KalanGun"/> sunucuda
        /// hesaplanıyor — istemcinin saatine bırakılsaydı iki kullanıcı aynı kaydı farklı
        /// görürdü (düzenleme ekranındaki <c>SuresiDoldu</c> ile aynı gerekçe).
        /// </summary>
        public static FirmaPaneliYetkiliDto Yetkili(FirmaImzaYetkilisi y, DateTime bugun) => new()
        {
            Ad = y.Ad,
            Tckn = y.Tckn,
            Gorev = y.Gorev,
            TemsilSekli = y.TemsilSekli,
            YetkiBitis = y.YetkiBitis,
            KalanGun = y.YetkiBitis is { } bitis ? (bitis.Date - bugun.Date).Days : null,
            // Bitiş GÜNÜ dahil geçerli; düzenleme ekranıyla aynı kural.
            SuresiDoldu = y.YetkiBitis is { } b && b.Date < bugun.Date
        };
    }
}
