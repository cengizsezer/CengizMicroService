using CatalogService.Api.Features.FirmaKontrol.Dtos;

namespace CatalogService.Api.Features.FirmaKontrol.Services
{
    /// <summary>
    /// Bir mizan yüklemesinin özeti: seviye sayısı ve ana hesap borç/alacak bakiye
    /// toplamları. Yalnız <b>kayıt sırasında</b> bir kez çalışır ve ağaç kaydına yazılır;
    /// anasayfa bu değerleri okur, yeniden hesaplamaz.
    ///
    /// Mali tablolardan hiçbiri (bilanço, gelir tablosu, oranlar) bu değeri kullanmaz —
    /// yalnız "yüklenen dosya kendi içinde dengeli mi" sorusunun cevabı.
    ///
    /// Seviye koddan türetilir (boşlukla ayrılmış parça sayısı; istemcideki
    /// <c>HesapDugumu.Parcala</c> ile aynı kural). Toplam yalnız ana hesap (tek parçalı)
    /// düğümlerden alınır; alt kırılımlar dahil edilseydi aynı para iki kez sayılırdı.
    /// </summary>
    public static class MizanYuklemeOzeti
    {
        public sealed record Ozet(int SeviyeSayisi, decimal BorcToplam, decimal AlacakToplam);

        public static Ozet Hesapla(IReadOnlyCollection<MizanHamDugumDto> dugumler)
        {
            var seviye = 0;
            decimal borc = 0, alacak = 0;

            foreach (var d in dugumler)
            {
                var parca = Parcala(d.Kod).Length;
                if (parca == 0) continue;

                if (parca > seviye) seviye = parca;

                if (parca == 1)
                {
                    borc += d.BorcBakiye;
                    alacak += d.AlacakBakiye;
                }
            }

            return new Ozet(seviye, borc, alacak);
        }

        private static string[] Parcala(string? kod)
            => string.IsNullOrWhiteSpace(kod)
                ? Array.Empty<string>()
                : kod.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
