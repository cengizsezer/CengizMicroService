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
        /// yoksa bu uyarı çıkmaz — dolacak bir yetki yok; eksik veri ayrı bir konu.
        /// </summary>
        private static FirmaUyariDto? ImzaUyarisi(IReadOnlyList<FirmaImzaYetkilisi> yetkililer, DateTime bugun)
        {
            if (yetkililer.Count == 0) return null;

            // Süresiz yetkili varsa firma imzasız kalmıyor.
            if (yetkililer.Any(y => y.YetkiBitis is null)) return null;

            var enGec = yetkililer.Max(y => y.YetkiBitis!.Value.Date);
            var kalan = (enGec - bugun.Date).Days;

            if (kalan >= ImzaUyariGun) return null;

            var mesaj = kalan < 0
                ? $"İmza yetkisi {Math.Abs(kalan)} gün önce doldu ({enGec:dd.MM.yyyy})."
                : kalan == 0
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
            int? seciliFirmaId)
        {
            var panel = new FirmaPaneliDto();

            var sirali = firmalar.OrderBy(Ad, StringComparer.CurrentCultureIgnoreCase).ToList();

            var uyariHaritasi = new Dictionary<int, List<FirmaUyariDto>>();

            foreach (var firma in sirali)
            {
                siciller.TryGetValue(firma.Id, out var sicil);

                var firmaUyarilari = Uyarilar(firma, sicil,
                                              ortaklar[firma.Id].ToList(),
                                              yetkililer[firma.Id].ToList(),
                                              bugun);

                uyariHaritasi[firma.Id] = firmaUyarilari;

                panel.Firmalar.Add(new FirmaPaneliOzetDto
                {
                    FirmaId = firma.Id,
                    Ad = Ad(firma),
                    Unvan = firma.Unvan,
                    VergiKimlikNo = firma.VergiKimlikNo,
                    Uyarilar = firmaUyarilari
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

                Mukellefiyet = new FirmaMukellefiyetDto
                {
                    VergiKimlikNo = secili.VergiKimlikNo,
                    VergiDairesi = secili.VergiDairesi,
                    MukellefiyetTurleri = seciliSicil?.MukellefiyetTurleri,
                    EFatura = seciliSicil?.EFatura,
                    EDefter = seciliSicil?.EDefter,
                    IseBaslamaTarihi = seciliSicil?.IseBaslamaTarihi,
                    NaceKodu = seciliSicil?.NaceKodu
                },

                Sicil = new FirmaPaneliSicilDto
                {
                    TicaretSicilNo = secili.TicaretSicilNo,
                    MersisNo = seciliSicil?.MersisNo,
                    Sermaye = seciliSicil?.Sermaye,
                    SermayeParaBirimi = seciliSicil?.SermayeParaBirimi,
                    KurulusTarihi = seciliSicil?.KurulusTarihi,
                    Adres = seciliSicil?.Adres,
                    OrkaFirmaKodu = secili.OrkaFirmaKodu
                },

                Ortaklik = FirmaBilgiService.Ortaklik(
                    ortaklar[secili.Id].OrderBy(o => o.Sira).ThenBy(o => o.Id).ToList()),

                Yetkililer = yetkililer[secili.Id]
                    .OrderBy(y => y.Sira).ThenBy(y => y.Id)
                    .Select(y => Yetkili(y, bugun))
                    .ToList(),

                Belgeler = seciliBelgeler.ToList()
            };

            return panel;
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
