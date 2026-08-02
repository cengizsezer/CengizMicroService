using WebApp.Shared.Dto.Muhasebe;

namespace WebApp.Application.Services.Interfaces
{
    /// <summary>
    /// Muhasebe modülü uçları (hesap planı + rapor). Yazma çağrıları sunucunun ürettiği
    /// Türkçe hata mesajını <c>Hata</c> alanıyla geri verir; ekranda bu mesaj gösterilir.
    /// </summary>
    public interface IMuhasebeApi
    {
        // ---- Hesap planı ----

        /// <summary>Ağacın tamamı; düz liste, koda göre sıralı.</summary>
        Task<List<HesapPlaniDto>> GetHesapPlaniAsync(CancellationToken ct = default);

        Task<HesapPlaniDto?> GetHesapAsync(int id, CancellationToken ct = default);

        /// <summary>Üst hesabın altındaki ilk boş kod; ekleme diyaloğu bununla açılır.</summary>
        Task<(SonrakiKodDto? Veri, string? Hata)> GetSonrakiKodAsync(int ustId, CancellationToken ct = default);

        /// <summary>Grubun altındaki kullanılmamış kebir kodları (iş kuralı 7).</summary>
        Task<(List<BosKebirDto>? Veri, string? Hata)> GetBosKebirlerAsync(int grupId, CancellationToken ct = default);

        /// <summary>Banka muavini açarken kullanılan TCMB EFT katılımcı listesi.</summary>
        Task<List<BankaKoduDto>> GetBankaKodlariAsync(CancellationToken ct = default);

        Task<(HesapPlaniDto? Veri, string? Hata)> CreateHesapAsync(HesapPlaniCreateDto dto, CancellationToken ct = default);

        Task<(HesapPlaniDto? Veri, string? Hata)> UpdateHesapAsync(int id, HesapPlaniUpdateDto dto, CancellationToken ct = default);

        /// <summary>Hesabı ve alt ağacını pasife çeker (iş kuralı 8).</summary>
        Task<(HesapPlaniDto? Veri, string? Hata)> PasifeAlAsync(int id, CancellationToken ct = default);

        /// <summary>Fiş girişi seçim listesi: yalnızca hareket gören ve aktif hesaplar (iş kuralı 14).</summary>
        Task<List<HesapPlaniDto>> GetHareketGorenlerAsync(CancellationToken ct = default);

        // ---- Fiş ----

        Task<FisDto?> GetFisAsync(int id, CancellationToken ct = default);

        Task<List<FisOzetDto>> GetFisListeAsync(DateTime? bas = null, DateTime? bit = null,
                                                FisDurum? durum = null, int? hesapId = null,
                                                CancellationToken ct = default);

        Task<(FisDto? Veri, string? Hata)> CreateFisAsync(FisYazDto dto, CancellationToken ct = default);

        /// <summary>Yalnızca taslak fiş güncellenir (iş kuralı 15).</summary>
        Task<(FisDto? Veri, string? Hata)> UpdateFisAsync(int id, FisYazDto dto, CancellationToken ct = default);

        Task<(FisDto? Veri, string? Hata)> KesinlestirAsync(int id, CancellationToken ct = default);

        /// <summary>Yalnızca taslak fiş silinir (iş kuralı 15).</summary>
        Task<(bool Basarili, string? Hata)> DeleteFisAsync(int id, CancellationToken ct = default);

        /// <summary>Kesinleşmiş fişin borç/alacağını yer değiştirmiş yeni fişini üretir.</summary>
        Task<(FisDto? Veri, string? Hata)> TersKayitAsync(int id, TersKayitDto dto, CancellationToken ct = default);

        // ---- Masraf merkezi ----

        /// <summary>
        /// Fiş satırındaki masraf merkezi seçicisinin listesi. Varsayılan olarak yalnızca
        /// aktif merkezler gelir; geçmiş fişte pasif merkez varsa <paramref name="pasifDahil"/> kullanılır.
        /// </summary>
        Task<List<MasrafMerkeziSecenekDto>> GetMasrafMerkezleriAsync(bool pasifDahil = false, CancellationToken ct = default);

        /// <exception cref="System.Exception">Fırlatmaz; hata metni tuple'ın ikinci alanında döner.</exception>
        Task<(MasrafMerkeziSecenekDto? Veri, string? Hata)> CreateMasrafMerkeziAsync(MasrafMerkeziYazDto dto, CancellationToken ct = default);

        /// <summary>Silme yok; kullanılmayan merkez pasife çekilir, geçmiş kayıtlarda görünür.</summary>
        Task<(MasrafMerkeziSecenekDto? Veri, string? Hata)> MasrafMerkeziPasifeAlAsync(int id, CancellationToken ct = default);

        // ---- Rapor ----

        /// <summary>
        /// Mizan; hem hesap planı ağacının bakiye kolonunu hem mizan ekranını besler.
        /// Bakiye saklanmaz, her istekte hesaplanır (iş kuralı 18).
        /// </summary>
        Task<MizanDto?> GetMizanAsync(DateTime? bas = null, DateTime? bit = null, byte? seviye = null, CancellationToken ct = default);

        /// <summary>T cetveli verisi; üst hesapta alt ağacın tamamı toplanır (iş kuralı 19).</summary>
        Task<EkstreDto?> GetEkstreAsync(int hesapId, DateTime? bas = null, DateTime? bit = null, CancellationToken ct = default);

        /// <summary>Masraf merkezi dağılımı ve hesap kırılımı.</summary>
        Task<MasrafMerkeziRaporDto?> GetMasrafMerkeziRaporAsync(DateTime? bas = null, DateTime? bit = null, CancellationToken ct = default);
    }
}
