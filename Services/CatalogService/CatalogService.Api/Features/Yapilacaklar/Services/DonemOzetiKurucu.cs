using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;

namespace CatalogService.Api.Features.Yapilacaklar.Services
{
    /// <summary>
    /// Takip panosunun sekme kuralları — TEK YER (Prompt 14). Sekme sayısı ve renk eşiği burada;
    /// istemci kendi eşiğini tanımlamaz, sunucunun hesapladığı <see cref="EksikTonu"/>'nu boyar.
    /// </summary>
    public static class DonemSekmeleri
    {
        /// <summary>Gösterilen dönem sayısı: içinde bulunulan ay + önceki beş ay.</summary>
        public const int Sayi = 6;

        /// <summary>Bu kadar ve fazla eksik kırmızı; 1 ile bunun bir eksiği amber.</summary>
        public const int KirmiziEsik = 4;

        public static EksikTonu Ton(int eksik)
            => eksik <= 0 ? EksikTonu.Yok : eksik < KirmiziEsik ? EksikTonu.Az : EksikTonu.Cok;

        /// <summary>Sekmedeki aylar, eskiden yeniye: bugünün ayı ve öncesindeki <see cref="Sayi"/> − 1 ay.</summary>
        public static List<IsDonemi> Aylar(DateTime bugun)
        {
            var donem = new IsDonemi(IsTekrari.Aylik, bugun.Year, bugun.Month);
            var aylar = new List<IsDonemi> { donem };
            for (var i = 1; i < Sayi; i++) aylar.Add(donem = donem.Onceki());
            aylar.Reverse();
            return aylar;
        }
    }

    /// <summary>
    /// Özet sheet: satır firma, kolon dönem, hücre "5/6". Her ay için <see cref="DonemPanosuKurucu.Kur"/>
    /// çağrılır ve sonucu toplanır — ikinci bir dönem/geçerlilik hesabı YOK; özetin sayıları o ayın
    /// matrisiyle birebir tutar. Saf fonksiyon.
    /// </summary>
    public static class DonemOzetiKurucu
    {
        public static DonemOzetiDto Kur(DateTime bugun,
                                        IReadOnlyCollection<YapilacaklarKurucu.FirmaGirdisi> firmalar,
                                        IReadOnlyCollection<VergiTakvimi> takvim,
                                        IReadOnlyCollection<FirmaIsi> ozelIsler,
                                        IReadOnlyCollection<IsTamamlama> tamamlamalar)
        {
            var varsayilan = DonemPanosuService.VarsayilanDonem(bugun);
            var aylar = DonemSekmeleri.Aylar(bugun);
            var panolar = aylar.Select(a => DonemPanosuKurucu.Kur(a.Yil, a.No, firmalar, takvim, ozelIsler, tamamlamalar)).ToList();

            var dto = new DonemOzetiDto
            {
                VarsayilanAnahtar = new IsDonemi(IsTekrari.Aylik, varsayilan.Yil, varsayilan.Ay).Anahtar,
                KirmiziEsik = DonemSekmeleri.KirmiziEsik
            };

            for (var i = 0; i < aylar.Count; i++)
            {
                var (ay, pano) = (aylar[i], panolar[i]);
                var eksik = pano.Toplam - pano.Yapilan;
                dto.Donemler.Add(new DonemSekmesiDto
                {
                    Yil = ay.Yil,
                    Ay = ay.No,
                    Anahtar = ay.Anahtar,
                    Etiket = pano.Etiket,
                    Toplam = pano.Toplam,
                    Yapilan = pano.Yapilan,
                    Eksik = eksik,
                    EksikFirmaSayisi = pano.Satirlar.Count(s => s.Yapilan < s.Gecerli),
                    Ton = DonemSekmeleri.Ton(eksik),
                    IcindeBulunulan = i == aylar.Count - 1,
                    Eksikler = pano.Eksikler
                });
            }

            // Satırlar: her panoda firma sırası aynı (ada göre); yine de Id ile eşlenir.
            var tr = StringComparer.Create(IsTakvimHesabi.Tr, true);
            foreach (var firma in firmalar.OrderBy(f => f.Ad, tr))
            {
                var satir = new DonemOzetSatirDto
                {
                    FirmaId = firma.FirmaId,
                    FirmaAdi = firma.Ad,
                    SorumluKullaniciId = firma.SorumluKullaniciId,
                    SorumluKullaniciAdi = firma.SorumluKullaniciAdi
                };
                foreach (var pano in panolar)
                {
                    var s = pano.Satirlar.First(x => x.FirmaId == firma.FirmaId);
                    satir.Hucreler.Add(new DonemOzetHucreDto
                    {
                        Gecerli = s.Gecerli,
                        Yapilan = s.Yapilan,
                        Ton = DonemSekmeleri.Ton(s.Gecerli - s.Yapilan)
                    });
                }
                dto.Satirlar.Add(satir);
            }

            return dto;
        }
    }
}
