using WebApp.Pages.Hesaplamalar.FinansmanGiderKisitlamasi.Model;

namespace WebApp.Pages.Hesaplamalar.FinansmanGiderKisitlamasi.Services
{
    /// <summary>
    /// Hesap ve oran tablosu uçları. Hesabın kendisi sunucuda; istemci yalnız girdileri
    /// gönderip dokuz satırı alır (Bordro sekmesiyle aynı yaklaşım).
    /// </summary>
    public interface IFinansmanKisitlamaApiService
    {
        /// <summary>
        /// Hesabı yaptırır. Sunucu 400 dönerse (örn. yılın oranı tanımlı değil) sonuç
        /// null gelir ve <paramref name="hataMesaji"/> gövdedeki açıklamayı taşır.
        /// </summary>
        Task<(FinansmanKisitlamaSonucDto? Sonuc, string? HataMesaji)> HesaplaAsync(
            FinansmanKisitlamaHesapRequest request, CancellationToken ct = default);

        Task<List<FinansmanKisitlamaOraniDto>> GetOranlarAsync(CancellationToken ct = default);

        Task<(FinansmanKisitlamaOraniDto? Oran, string? HataMesaji)> UpsertOranAsync(
            int yil, FinansmanKisitlamaOraniSaveDto dto, CancellationToken ct = default);

        Task<bool> DeleteOranAsync(int yil, CancellationToken ct = default);
    }
}
