using WebApp.Shared.Dto.MevzuatGrafigi;

namespace WebApp.Application.Services.Interfaces
{
    /// <summary>
    /// /catalog/mevzuat-grafigi. Hiçbir metot fırlatmaz: hata durumunda sonuç
    /// Basarili=false ve sunucunun Türkçe mesajıyla döner.
    /// </summary>
    public interface IMevzuatGrafigiApi
    {
        Task<MevzuatGrafigiSonuc<MevzuatGrafigiDto>> GetAsync();

        Task<MevzuatGrafigiSonuc<MevzuatKartDto>> KartEkleAsync(MevzuatKartSaveDto dto);
        Task<MevzuatGrafigiSonuc<MevzuatKartDto>> KartGuncelleAsync(int id, MevzuatKartSaveDto dto);
        Task<MevzuatGrafigiSonuc> KartSilAsync(int id);

        Task<MevzuatGrafigiSonuc<MevzuatBaglantiDto>> BaglantiEkleAsync(MevzuatBaglantiSaveDto dto);
        Task<MevzuatGrafigiSonuc> BaglantiSilAsync(int id);

        Task<MevzuatGrafigiSonuc<MevzuatDislamaDto>> DislamaEkleAsync(MevzuatDislamaSaveDto dto);
        Task<MevzuatGrafigiSonuc> DislamaSilAsync(int id);

        Task<MevzuatGrafigiSonuc<MevzuatTuvalDto>> TuvalEkleAsync(MevzuatTuvalSaveDto dto);
        Task<MevzuatGrafigiSonuc<MevzuatTuvalDto>> TuvalGuncelleAsync(int id, MevzuatTuvalSaveDto dto);
        Task<MevzuatGrafigiSonuc<MevzuatYerlesimDto>> TuvaleKartEkleAsync(int tuvalId, MevzuatYerlesimDto dto);
        Task<MevzuatGrafigiSonuc> TuvaldenKartKaldirAsync(int tuvalId, int kartId);
        Task<MevzuatGrafigiSonuc<List<MevzuatYerlesimDto>>> YerlesimGuncelleAsync(int tuvalId, List<MevzuatYerlesimDto> liste);

        Task<MevzuatGrafigiSonuc<MevzuatSartTakipDto>> SartTakipKaydetAsync(MevzuatSartTakipDto dto);
    }
}
