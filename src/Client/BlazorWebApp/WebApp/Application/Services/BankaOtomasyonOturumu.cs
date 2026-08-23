using Blazored.SessionStorage;
using WebApp.Application.Services.Interfaces;
using WebApp.Application.Services.Yonetim;
using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Application.Services
{
    /// <summary>
    /// <inheritdoc cref="IBankaOtomasyonOturumu"/>
    /// </summary>
    public sealed class BankaOtomasyonOturumu : IBankaOtomasyonOturumu
    {
        private readonly IBankaOtomasyonDeposu _depo;
        private readonly IFirmaApiClient _firmalar;

        public BankaOtomasyonOturumu(IBankaOtomasyonDeposu depo, IFirmaApiClient firmalar)
        {
            _depo = depo;
            _firmalar = firmalar;
        }

        public FirmaDto? SeciliFirma { get; private set; }

        public int FirmaId => SeciliFirma?.Id ?? 0;

        public string FirmaAdi => Ad(SeciliFirma);

        public event Action? Degisti;

        public async Task GirAsync(FirmaDto firma)
        {
            if (firma is null || firma.Id <= 0) return;

            SeciliFirma = firma;
            await _depo.FirmaIdYazAsync(firma.Id);

            Degisti?.Invoke();
        }

        /// <summary>
        /// Seçim bellekte yoksa depodan geri alınır ve firma <b>kaynağından doğrulanır</b>:
        /// arada silinmiş/pasife alınmış bir firma için ekran açılmamalı, istek de
        /// atılmamalı (sunucu zaten 400 döndürürdü).
        /// </summary>
        public async Task<FirmaDto?> BaglamiHazirlaAsync()
        {
            if (SeciliFirma is not null) return SeciliFirma;

            var firmaId = await _depo.FirmaIdAsync();
            if (firmaId is not > 0) return null;

            try
            {
                SeciliFirma = await _firmalar.GetByIdAsync(firmaId.Value);
            }
            catch (Exception)
            {
                // Ağ/yetki hatası: seçim silinmez, kullanıcı tekrar deneyebilsin.
                return null;
            }

            if (SeciliFirma is null) await _depo.FirmaIdYazAsync(null);
            return SeciliFirma;
        }

        public async Task CikAsync()
        {
            SeciliFirma = null;
            await _depo.FirmaIdYazAsync(null);

            Degisti?.Invoke();
        }

        /// <summary>Listede ve başlıkta gösterilen ad: unvan varsa o, yoksa kısa ad.</summary>
        public static string Ad(FirmaDto? firma)
        {
            if (firma is null) return string.Empty;
            return string.IsNullOrWhiteSpace(firma.Unvan) ? firma.KisaAd : firma.Unvan;
        }
    }

    /// <summary>Seçimi tarayıcı oturum deposunda tutar; sekme kapanınca silinir.</summary>
    public sealed class SessionStorageBankaOtomasyonDeposu : IBankaOtomasyonDeposu
    {
        private const string Anahtar = "BankaOtomasyon.FirmaId";

        private readonly ISessionStorageService _depo;

        public SessionStorageBankaOtomasyonDeposu(ISessionStorageService depo) => _depo = depo;

        public async Task<int?> FirmaIdAsync() => await _depo.GetItemAsync<int?>(Anahtar);

        public async Task FirmaIdYazAsync(int? firmaId)
        {
            if (firmaId is not > 0)
                await _depo.RemoveItemAsync(Anahtar);
            else
                await _depo.SetItemAsync(Anahtar, firmaId.Value);
        }
    }
}
