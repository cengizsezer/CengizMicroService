using WebApp.Application.Services;
using WebApp.Application.Services.Interfaces;
using WebApp.Application.Services.Yonetim;
using WebApp.Shared.Dto.Yonetim;

namespace WebApp.UnitTests.BankaEkstre
{
    /// <summary>
    /// Banka Otomasyon'un firma bağlamı. Sınanan iddia şu: seçilen firma
    /// <c>catalog.Firmalar</c> kaydıdır ve modül boyunca kapsam olarak o kullanılır.
    ///
    /// Modül eskiden <see cref="IAppSessionManager.SelectFirmAsync"/> ile TENANT'ı
    /// çeviriyordu. pkfadmin tek tenant'a bağlı olduğu için firma listesi tek satıra
    /// düşüyor, sekiz firmanın verisi aynı kovaya yazılıyordu (bkz. KARARLAR §68).
    /// Artık tenant'a hiç dokunulmuyor; kapsam isteğe <c>?firmaId=</c> olarak ekleniyor.
    /// Bu yüzden testler de "token çevrildi mi" yerine "seçim doğru firma mı, kalıcı mı,
    /// yenilemede kaynağından doğrulanıyor mu" sorularını sınıyor.
    /// </summary>
    public class BankaOtomasyonOturumuTests
    {
        private static readonly FirmaDto Aday = new()
        {
            Id = 4,
            Unvan = "PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ",
            KisaAd = "PKF ADAY DENETİM",
            VergiKimlikNo = "0070511435",
            Aktif = true
        };

        private static readonly FirmaDto Smmm = new()
        {
            Id = 5,
            Unvan = "PKF İSTANBUL SERBEST MUHASEBECİ VE MALİ MÜŞAVİRLİK A.Ş.",
            KisaAd = "PKF SMMM",
            VergiKimlikNo = "7300717173",
            Aktif = true
        };

        private static (BankaOtomasyonOturumu Oturum, SahteFirmaKaynagi Kaynak, SahteDepo Depo) Kur()
        {
            var kaynak = new SahteFirmaKaynagi(Aday, Smmm);
            var depo = new SahteDepo();
            return (new BankaOtomasyonOturumu(depo, kaynak), kaynak, depo);
        }

        [Fact]
        public async Task Girilen_firma_kapsam_olur_ve_hatirlanir()
        {
            var (oturum, _, depo) = Kur();

            await oturum.GirAsync(Aday);

            Assert.Equal(4, oturum.FirmaId);
            Assert.Equal(Aday.Unvan, oturum.FirmaAdi);

            // Seçim oturum boyunca hatırlanır: sayfa yenilense de aynı firmaya dönülür.
            Assert.Equal(4, await depo.FirmaIdAsync());
        }

        [Fact]
        public async Task Sekme_degisiminde_firma_kaynagina_tekrar_gidilmez()
        {
            var (oturum, kaynak, _) = Kur();

            await oturum.GirAsync(Aday);
            kaynak.OkunanIdler.Clear();

            // Aktar → Tanımlar → Aktar: her sekme açılışta bağlamı hazırlar.
            Assert.Equal(Aday, await oturum.BaglamiHazirlaAsync());
            Assert.Equal(Aday, await oturum.BaglamiHazirlaAsync());
            Assert.Equal(Aday, await oturum.BaglamiHazirlaAsync());

            // Seçim bellekte: boşuna istek atılmadı.
            Assert.Empty(kaynak.OkunanIdler);
        }

        [Fact]
        public async Task Sayfa_yenilenince_secim_depodan_geri_gelir()
        {
            var kaynak = new SahteFirmaKaynagi(Aday, Smmm);
            var depo = new SahteDepo();

            await new BankaOtomasyonOturumu(depo, kaynak).GirAsync(Aday);

            // Yenileme: servis sıfırdan kurulur, seçim yalnız depodan gelebilir.
            var yeni = new BankaOtomasyonOturumu(depo, kaynak);
            var firma = await yeni.BaglamiHazirlaAsync();

            Assert.Equal(4, firma?.Id);
            Assert.Equal(4, yeni.FirmaId);

            // Firma kaynağından doğrulandı: arada silinmiş bir firmayla ekran açılmasın.
            Assert.Equal(new[] { 4 }, kaynak.OkunanIdler);
        }

        /// <summary>
        /// Depodaki firma artık tanımlı değilse (silinmiş ya da erişim kalkmış) bağlam
        /// hazırlanamaz ve seçim temizlenir; ekran firma listesine yönlendirir.
        /// </summary>
        [Fact]
        public async Task Artik_tanimli_olmayan_firma_icin_baglam_hazirlanamaz()
        {
            var kaynak = new SahteFirmaKaynagi(Aday, Smmm);
            var depo = new SahteDepo();
            await depo.FirmaIdYazAsync(999);

            var oturum = new BankaOtomasyonOturumu(depo, kaynak);

            Assert.Null(await oturum.BaglamiHazirlaAsync());
            Assert.Null(await depo.FirmaIdAsync());
        }

        [Fact]
        public async Task Secim_yoksa_baglam_hazirlanamaz()
        {
            var (oturum, _, _) = Kur();

            // Ekran bunu null görünce firma listesine yönlendirir; sessizce bir firmanın
            // verisi açılmaz.
            Assert.Null(await oturum.BaglamiHazirlaAsync());
            Assert.Equal(0, oturum.FirmaId);
        }

        [Fact]
        public async Task Firma_listesine_donunce_secim_birakilir()
        {
            var (oturum, _, depo) = Kur();

            await oturum.GirAsync(Aday);
            await oturum.CikAsync();

            Assert.Null(oturum.SeciliFirma);
            Assert.Equal(0, oturum.FirmaId);
            Assert.Null(await depo.FirmaIdAsync());
        }

        /// <summary>
        /// Ekranda gösterilen ad: unvan varsa o, yoksa kısa ad. Firma seçim ekranı da
        /// aynı kuralı kullanıyor ki başlık ile listedeki satır aynı şeyi yazsın.
        /// </summary>
        [Fact]
        public void Ad_unvan_yoksa_kisa_ada_duser()
        {
            Assert.Equal(Aday.Unvan, BankaOtomasyonOturumu.Ad(Aday));
            Assert.Equal("YALNIZ KISA AD",
                         BankaOtomasyonOturumu.Ad(new FirmaDto { Id = 9, KisaAd = "YALNIZ KISA AD" }));
            Assert.Equal(string.Empty, BankaOtomasyonOturumu.Ad(null));
        }

        // ---- Sahteler ----

        /// <summary>
        /// Raporlar'ın da kullandığı firma kaynağı (<c>/catalog/firmalar</c>) taklidi.
        /// Hangi id'lerin sorulduğu kaydediliyor: gereksiz istek atılmadığı sınanıyor.
        /// </summary>
        private sealed class SahteFirmaKaynagi : IFirmaApiClient
        {
            private readonly List<FirmaDto> _firmalar;

            public SahteFirmaKaynagi(params FirmaDto[] firmalar) => _firmalar = firmalar.ToList();

            public List<int> OkunanIdler { get; } = new();

            public Task<List<FirmaDto>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default)
                => Task.FromResult(_firmalar.ToList());

            public Task<FirmaDto?> GetByIdAsync(int id, CancellationToken ct = default)
            {
                OkunanIdler.Add(id);
                return Task.FromResult(_firmalar.FirstOrDefault(f => f.Id == id));
            }

            public Task<FirmaDto> CreateAsync(FirmaCreateDto dto, CancellationToken ct = default)
                => throw new NotSupportedException();

            public Task<FirmaDto> UpdateAsync(int id, FirmaUpdateDto dto, CancellationToken ct = default)
                => throw new NotSupportedException();

            public Task DeleteAsync(int id, CancellationToken ct = default)
                => throw new NotSupportedException();
        }

        private sealed class SahteDepo : IBankaOtomasyonDeposu
        {
            private int? _firmaId;

            public Task<int?> FirmaIdAsync() => Task.FromResult(_firmaId);

            public Task FirmaIdYazAsync(int? firmaId)
            {
                _firmaId = firmaId is > 0 ? firmaId : null;
                return Task.CompletedTask;
            }
        }
    }
}
