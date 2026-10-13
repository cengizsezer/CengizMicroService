using WebApp.Application.Services.Interfaces;
using WebApp.Shared.Dto.FirmaPortfoyu;

namespace WebApp.Application.Services
{
    /// <summary>
    /// Firma Portföyü: ExcelMizanParser sonucunu sunucuya gidecek ana hesap satırlarına çevirir. Okuyucuya
    /// dokunmaz, yalnız sonucunu kullanır.
    ///
    /// Ana hesaplar <see cref="MizanParseResult.Rows"/>'tan (okuyucunun kabul ettiği 3 haneli kodlar — toplam
    /// satırı, okunamayan bakiye zaten elenmiş); borç/alacak bakiyesi aynı kodun seviye-1 düğümünden. Basit
    /// biçimde (yalnız "Bakiye" sütunu) borç/alacak yoktur: bakiye işaretine göre borca ya da alacağa yazılır.
    /// Alt kırılımlar gönderilmez (aynı para, çift sayılır).
    /// </summary>
    public static class PortfoyMizanSatirlari
    {
        public static List<AylikMizanSatiriDto> Hazirla(MizanParseResult sonuc)
        {
            var dugumler = sonuc.HesapDugumleri
                .Where(d => d.Seviye == 1)
                .GroupBy(d => d.Kod)
                .ToDictionary(g => g.Key, g => g.First());

            var satirlar = new List<AylikMizanSatiriDto>(sonuc.Rows.Count);
            foreach (var r in sonuc.Rows)
            {
                var bakiye = r.CariDonem ?? r.OncekiDonem ?? 0m;
                decimal borc, alacak;
                if (dugumler.TryGetValue(r.Kod, out var d) && (d.BorcBakiye != 0m || d.AlacakBakiye != 0m))
                {
                    borc = d.BorcBakiye;
                    alacak = d.AlacakBakiye;
                }
                else
                {
                    borc = bakiye > 0m ? bakiye : 0m;
                    alacak = bakiye < 0m ? -bakiye : 0m;
                }

                satirlar.Add(new AylikMizanSatiriDto
                {
                    HesapKodu = r.Kod,
                    HesapAdi = r.Ad ?? d?.Ad,
                    BorcBakiye = borc,
                    AlacakBakiye = alacak
                });
            }
            return satirlar;
        }
    }
}
