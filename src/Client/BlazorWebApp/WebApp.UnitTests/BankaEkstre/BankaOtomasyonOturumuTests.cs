using WebApp.Application.Services;
using WebApp.Application.Services.Interfaces;
using WebApp.Domain.Models.User;
using WebApp.Manager;

namespace WebApp.UnitTests.BankaEkstre
{
    /// <summary>
    /// Banka Otomasyon'un firma bağlamı. Sınanan iddia şu: ekranda seçilen firma
    /// <b>gerçekten</b> tenant'ı belirler. Ekran "PKF Aday" gösterirken isteğin SMMM
    /// tenant'ıyla gitmesi, verinin yanlış firmaya yazılması demek.
    ///
    /// Tenant, <see cref="IAppSessionManager.SelectFirmAsync"/> ile üretilen token'ın
    /// <c>tn</c> claim'inden gelir; bu yüzden testler o çağrının doğru firmayla ve
    /// doğru anda yapıldığını doğrular.
    /// </summary>
    public class BankaOtomasyonOturumuTests
    {
        private static readonly FirmaDto Aday = new() { Ad = "PKF Aday", Vkn = "1111111111", FirmaNo = "201" };
        private static readonly FirmaDto Smmm = new() { Ad = "PKF SMMM", Vkn = "2222222222", FirmaNo = "106" };

        private static (BankaOtomasyonOturumu Oturum, SahteOturumYoneticisi Yonetici, SahteDepo Depo) Kur()
        {
            var yonetici = new SahteOturumYoneticisi(Aday, Smmm);
            var depo = new SahteDepo();
            return (new BankaOtomasyonOturumu(yonetici, depo), yonetici, depo);
        }

        [Fact]
        public async Task Girilen_firma_tenant_baglamini_degistirir()
        {
            var (oturum, yonetici, depo) = Kur();

            await oturum.GirAsync(Aday);

            Assert.Equal("201", yonetici.SelectedFirm?.FirmaNo);
            Assert.Equal(new[] { "201" }, yonetici.SecilenFirmaNolari);
            Assert.Equal("PKF Aday", oturum.SeciliFirma?.Ad);

            // Seçim oturum boyunca hatırlanır: sayfa yenilense de aynı firmaya dönülür.
            Assert.Equal("201", await depo.FirmaNoAsync());
        }

        [Fact]
        public async Task Sekme_degisiminde_firma_tekrar_sorulmaz()
        {
            var (oturum, yonetici, _) = Kur();

            await oturum.GirAsync(Aday);
            yonetici.SecilenFirmaNolari.Clear();

            // Aktar → Tanımlar → Aktar: her sekme açılışta bağlamı hazırlar.
            Assert.Equal(Aday, await oturum.BaglamiHazirlaAsync());
            Assert.Equal(Aday, await oturum.BaglamiHazirlaAsync());
            Assert.Equal(Aday, await oturum.BaglamiHazirlaAsync());

            // Bağlam zaten Aday; boşuna token yenilenmedi ve kullanıcıya soru sorulmadı.
            Assert.Empty(yonetici.SecilenFirmaNolari);
        }

        [Fact]
        public async Task Sayfa_yenilenince_secim_depodan_geri_gelir_ve_baglam_uygulanir()
        {
            var yonetici = new SahteOturumYoneticisi(Aday, Smmm);
            var depo = new SahteDepo();

            await new BankaOtomasyonOturumu(yonetici, depo).GirAsync(Aday);

            // Yenileme: servis sıfırdan kurulur, üstelik token bu arada SMMM'ye kaymış olsun.
            await yonetici.SelectFirmAsync(Smmm);
            var yeni = new BankaOtomasyonOturumu(yonetici, depo);

            var firma = await yeni.BaglamiHazirlaAsync();

            Assert.Equal("201", firma?.FirmaNo);
            Assert.Equal("201", yonetici.SelectedFirm?.FirmaNo);
        }

        [Fact]
        public async Task Secim_yoksa_baglam_hazirlanamaz()
        {
            var (oturum, _, _) = Kur();

            // Ekran bunu null görünce firma listesine yönlendirir; sessizce bir firmanın
            // verisi açılmaz.
            Assert.Null(await oturum.BaglamiHazirlaAsync());
        }

        [Fact]
        public async Task Genel_firma_degisikligi_modul_acikken_sayfadaki_secime_yenilir()
        {
            var (oturum, yonetici, _) = Kur();

            await oturum.GirAsync(Aday);
            oturum.Aktif = true;

            var uyarilar = new List<string>();
            oturum.Uyari += uyarilar.Add;

            // Üstteki genel FİRMA DEĞİŞTİR ile SMMM seçildi.
            await yonetici.SelectFirmAsync(Smmm);

            Assert.Equal("201", yonetici.SelectedFirm?.FirmaNo);   // sayfadaki seçim kazandı
            Assert.Equal("201", oturum.SeciliFirma?.FirmaNo);

            var uyari = Assert.Single(uyarilar);
            Assert.Contains("PKF Aday", uyari);
            Assert.Contains("PKF SMMM", uyari);
        }

        [Fact]
        public async Task Modul_kapaliyken_genel_firma_degisikligine_karisilmaz()
        {
            var (oturum, yonetici, _) = Kur();

            await oturum.GirAsync(Aday);
            oturum.Aktif = false;   // kullanıcı modülden çıktı (firma listesi ya da başka ekran)

            var uyarilar = new List<string>();
            oturum.Uyari += uyarilar.Add;

            await yonetici.SelectFirmAsync(Smmm);

            Assert.Equal("106", yonetici.SelectedFirm?.FirmaNo);
            Assert.Empty(uyarilar);
        }

        [Fact]
        public async Task Firma_listesine_donunce_secim_birakilir()
        {
            var (oturum, _, depo) = Kur();

            await oturum.GirAsync(Aday);
            await oturum.CikAsync();

            Assert.Null(oturum.SeciliFirma);
            Assert.False(oturum.Aktif);
            Assert.Null(await depo.FirmaNoAsync());
        }

        // ---- Sahteler ----

        /// <summary>
        /// Gerçek yöneticinin tenant'a dair davranışını taklit eder: SelectFirmAsync
        /// seçili firmayı değiştirir ve FirmChanged'i tetikler. Üretimde bu çağrı aynı
        /// zamanda yeni access token'ı (tn claim'i) üretir.
        /// </summary>
        private sealed class SahteOturumYoneticisi : IAppSessionManager
        {
            public SahteOturumYoneticisi(params FirmaDto[] firmalar) => Firms = firmalar;

            /// <summary>Tenant'ın kaç kez ve hangi firmaya çevrildiği.</summary>
            public List<string> SecilenFirmaNolari { get; } = new();

            public string Token { get; private set; } = string.Empty;
            public string RefreshToken => string.Empty;
            public bool RememberMe => false;
            public FirmaDto SelectedFirm { get; private set; } = default!;
            public IReadOnlyList<FirmaDto> Firms { get; }
            public bool IsAuthenticated => true;

            public event Action? AuthChanged;
            public event Action<FirmaDto>? FirmChanged;

            public Task InitializeFromLoginAsync(LoginResponseModel login, bool rememberMe) => Task.CompletedTask;
            public Task<bool> EnsureFirmSelectedAsync() => Task.FromResult(true);
            public Task RestoreAsync() => Task.CompletedTask;
            public Task ClearAsync() => Task.CompletedTask;

            public Task SelectFirmAsync(FirmaDto firm)
            {
                if (firm is null) return Task.CompletedTask;

                SecilenFirmaNolari.Add(firm.FirmaNo);
                SelectedFirm = firm;
                Token = $"token-tn-{firm.FirmaNo}";

                FirmChanged?.Invoke(firm);
                AuthChanged?.Invoke();
                return Task.CompletedTask;
            }
        }

        private sealed class SahteDepo : IBankaOtomasyonDeposu
        {
            private string? _firmaNo;

            public Task<string?> FirmaNoAsync() => Task.FromResult(_firmaNo);

            public Task FirmaNoYazAsync(string? firmaNo)
            {
                _firmaNo = string.IsNullOrWhiteSpace(firmaNo) ? null : firmaNo;
                return Task.CompletedTask;
            }
        }
    }
}
