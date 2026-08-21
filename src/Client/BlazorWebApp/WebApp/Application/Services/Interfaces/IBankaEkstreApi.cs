using WebApp.Shared.Dto.BankaEkstre;

namespace WebApp.Application.Services.Interfaces
{
    /// <summary>Banka ekstresi işleme modülü istemcisi.</summary>
    public interface IBankaEkstreApi
    {
        // Banka hesapları
        Task<List<BankaHesabiDto>> GetHesaplarAsync(bool pasifDahil = false, CancellationToken ct = default);
        Task<List<ParserSecenekDto>> GetParserlerAsync(CancellationToken ct = default);
        Task<(BankaHesabiDto? Veri, string? Hata)> CreateHesapAsync(BankaHesabiYazDto dto, CancellationToken ct = default);
        Task<(BankaHesabiDto? Veri, string? Hata)> UpdateHesapAsync(int id, BankaHesabiYazDto dto, CancellationToken ct = default);
        Task<string?> DeleteHesapAsync(int id, CancellationToken ct = default);

        // Ekstre
        Task<List<EkstreYuklemeDto>> GetYuklemelerAsync(CancellationToken ct = default);
        Task<EkstreYuklemeDto?> GetYuklemeAsync(int id, CancellationToken ct = default);
        Task<(EkstreYuklemeDto? Veri, string? Hata)> YukleAsync(int bankaHesabiId, Stream icerik, string dosyaAdi, CancellationToken ct = default);
        Task<List<EkstreSatirDto>> GetSatirlarAsync(int ekstreId, SatirDurum? durum = null, CancellationToken ct = default);
        Task<(EkstreSatirDto? Veri, string? Hata)> OnaylaAsync(int satirId, string hesapKodu, CancellationToken ct = default);
        Task<(EkstreSatirDto? Veri, string? Hata)> DigerBankadaAsync(int satirId, CancellationToken ct = default);
        Task<(DisaAktarimSonucDto? Veri, string? Hata)> DisaAktarAsync(int ekstreId, CancellationToken ct = default);

        /// <summary>Dışa aktarımın birinci parçası: açıklama kolonu değiştirilmiş orijinal dosya.</summary>
        Task<(string? DosyaAdi, byte[]? Icerik, string? Hata)> DuzeltilmisEkstreAsync(int ekstreId, CancellationToken ct = default);

        Task<string?> SilAsync(int ekstreId, CancellationToken ct = default);

        // Hesap planı
        Task<List<HesapPlaniKaydiDto>> HesapPlaniAraAsync(string? q, string? anaGrup = null, int enFazla = 20, CancellationToken ct = default);
        Task<int> HesapPlaniSayisiAsync(CancellationToken ct = default);
        Task<HesapPlaniOzetDto> HesapPlaniOzetAsync(CancellationToken ct = default);
        Task<(HesapPlaniIceAktarimSonucDto? Veri, string? Hata)> HesapPlaniIceAktarAsync(Stream icerik, string dosyaAdi, CancellationToken ct = default);

        // Öğrenilen eşleşmeler
        Task<List<HesapEslesmesiDto>> EslesmeleriAraAsync(string? q, int enFazla = 100, CancellationToken ct = default);
        Task<(HesapEslesmesiDto? Veri, string? Hata)> EslesmeGuncelleAsync(int id, HesapEslesmesiYazDto dto, CancellationToken ct = default);
        Task<string?> EslesmeSilAsync(int id, CancellationToken ct = default);
    }
}
