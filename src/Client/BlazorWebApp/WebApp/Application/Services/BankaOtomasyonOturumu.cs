using Blazored.SessionStorage;
using WebApp.Application.Services.Interfaces;
using WebApp.Domain.Models.User;
using WebApp.Manager;

namespace WebApp.Application.Services
{
    /// <summary>
    /// <inheritdoc cref="IBankaOtomasyonOturumu"/>
    /// </summary>
    public sealed class BankaOtomasyonOturumu : IBankaOtomasyonOturumu
    {
        private readonly IAppSessionManager _oturum;
        private readonly IBankaOtomasyonDeposu _depo;

        /// <summary>
        /// Tenant'ı kendimiz değiştirirken <see cref="IAppSessionManager.FirmChanged"/>
        /// yine tetiklenir. Bayrak olmasaydı bu kendi olayımızı "dışarıdan gelen çakışma"
        /// sanıp sonsuz döngüye girerdik.
        /// </summary>
        private bool _uyguluyoruz;

        public BankaOtomasyonOturumu(IAppSessionManager oturum, IBankaOtomasyonDeposu depo)
        {
            _oturum = oturum;
            _depo = depo;

            _oturum.FirmChanged += GenelFirmaDegisti;
        }

        public FirmaDto? SeciliFirma { get; private set; }

        public bool Aktif { get; set; }

        public event Action? Degisti;
        public event Action<string>? Uyari;

        public async Task GirAsync(FirmaDto firma)
        {
            if (firma is null) return;

            SeciliFirma = firma;
            await _depo.FirmaNoYazAsync(firma.FirmaNo);
            await TenantUygulaAsync(firma);

            Degisti?.Invoke();
        }

        public async Task<FirmaDto?> BaglamiHazirlaAsync()
        {
            if (SeciliFirma is null)
            {
                // Sayfa yenilendiğinde bu servis sıfırdan kurulur; seçim depodan geri gelir.
                var firmaNo = await _depo.FirmaNoAsync();
                if (!string.IsNullOrWhiteSpace(firmaNo))
                    SeciliFirma = _oturum.Firms.FirstOrDefault(f => Ayni(f?.FirmaNo, firmaNo));
            }

            if (SeciliFirma is null) return null;

            // Modül dışında firma değişmiş olabilir (menüden başka ekrana gidip üstteki
            // FİRMA DEĞİŞTİR kullanıldıysa). Modüle dönüldüğünde bağlam geri alınır;
            // aksi halde ekran Aday yazarken istek SMMM'ye giderdi.
            if (!Ayni(_oturum.SelectedFirm?.FirmaNo, SeciliFirma.FirmaNo))
                await TenantUygulaAsync(SeciliFirma);

            return SeciliFirma;
        }

        public async Task CikAsync()
        {
            SeciliFirma = null;
            Aktif = false;
            await _depo.FirmaNoYazAsync(null);

            Degisti?.Invoke();
        }

        /// <summary>
        /// Tenant'ı gerçekten değiştirir: yeni access token üretilir ve seçili firma
        /// başlığı/başlık header'ı güncellenir. Buradan sonraki her istek bu firmaya gider.
        /// </summary>
        private async Task TenantUygulaAsync(FirmaDto firma)
        {
            _uyguluyoruz = true;
            try
            {
                await _oturum.SelectFirmAsync(firma);
            }
            finally
            {
                _uyguluyoruz = false;
            }
        }

        /// <summary>
        /// Üstteki genel FİRMA DEĞİŞTİR kullanıldı. Modül ekranı açıkken sayfadaki seçim
        /// kazanır: bağlam geri alınır ve kullanıcı uyarılır. Modül kapalıyken karışılmaz.
        /// </summary>
        private async void GenelFirmaDegisti(FirmaDto gelen)
        {
            if (_uyguluyoruz || !Aktif || SeciliFirma is null) return;
            if (Ayni(gelen?.FirmaNo, SeciliFirma.FirmaNo)) return;

            var istenen = SeciliFirma;
            await TenantUygulaAsync(istenen);

            Uyari?.Invoke(
                $"Firma değişikliği uygulanmadı: Banka Otomasyon \"{istenen.Ad}\" firmasında açık. " +
                $"\"{gelen?.Ad}\" firmasına geçmek için firma listesine dönün.");

            Degisti?.Invoke();
        }

        private static bool Ayni(string? a, string? b)
            => !string.IsNullOrWhiteSpace(a) && string.Equals(a, b, StringComparison.Ordinal);
    }

    /// <summary>Seçimi tarayıcı oturum deposunda tutar; sekme kapanınca silinir.</summary>
    public sealed class SessionStorageBankaOtomasyonDeposu : IBankaOtomasyonDeposu
    {
        private const string Anahtar = "BankaOtomasyon.FirmaNo";

        private readonly ISessionStorageService _depo;

        public SessionStorageBankaOtomasyonDeposu(ISessionStorageService depo) => _depo = depo;

        public async Task<string?> FirmaNoAsync() => await _depo.GetItemAsync<string>(Anahtar);

        public async Task FirmaNoYazAsync(string? firmaNo)
        {
            if (string.IsNullOrWhiteSpace(firmaNo))
                await _depo.RemoveItemAsync(Anahtar);
            else
                await _depo.SetItemAsync(Anahtar, firmaNo);
        }
    }
}
