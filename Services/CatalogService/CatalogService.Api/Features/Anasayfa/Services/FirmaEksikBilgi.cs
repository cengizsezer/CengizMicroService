using CatalogService.Api.Features.Anasayfa.Dtos;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.FirmaBilgileri.Domain;

namespace CatalogService.Api.Features.Anasayfa.Services
{
    /// <summary>Bir firmanın eksik bilgi kontrolünün girdisi.</summary>
    /// <param name="CariMizanEksik">Geçmiş dönem mizanı var, cari dönemin mizanı yok.</param>
    public sealed record FirmaKunyeGirdisi(Firma Firma, FirmaSicilBilgisi? Sicil, int OrtakSayisi, int YetkiliSayisi,
                                           bool CariMizanEksik = false);

    /// <summary>
    /// Anasayfa künyesinin eksik bilgi kontrolleri — <b>tek tanım yeri</b>. Sol raydaki
    /// sayı rozeti ile sağdaki "Eksik bilgi" şeridi aynı listeden besleniyor; bir alan
    /// eklenir ya da çıkarılırsa ikisi birlikte değişir.
    ///
    /// Sıra kartların ekrandaki sırası: şerit "ilk üç"ü ve "Tamamla" ilk eksiği bu sırayla
    /// seçiyor.
    /// </summary>
    public static class FirmaEksikBilgi
    {
        private sealed record Kontrol(string Kart, string Alan, string Odak, Func<FirmaKunyeGirdisi, bool> Bos);

        private static bool B(string? metin) => string.IsNullOrWhiteSpace(metin);

        private static readonly Kontrol[] Kontroller =
        {
            // Mükellefiyet
            new("Mükellefiyet", "vergi dairesi", "vergiDairesi", g => B(g.Firma.VergiDairesi)),
            new("Mükellefiyet", "VKN", "vkn", g => B(g.Firma.VergiKimlikNo)),
            new("Mükellefiyet", "NACE kodu", "naceKodu", g => B(g.Sicil?.NaceKodu)),
            new("Mükellefiyet", "işe başlama", "iseBaslama", g => g.Sicil?.IseBaslamaTarihi is null),

            // Sınıflandırma ve mizan
            new("Sınıflandırma", "defter usulü", "defterUsulu", g => g.Firma.DefterUsulu == DefterUsulu.Belirsiz),
            new("Sınıflandırma", "vergi türü", "vergiTuru", g => g.Firma.VergiTuru == VergiTuru.Belirsiz),
            new("Sınıflandırma", "mizan formatı", "mizanFormati", g => !MizanFormatlari.Secili(g.Firma.MizanFormati)),
            new("Sınıflandırma", "hesap dönemi", "hesapDonemi", g => g.Firma.HesapDonemi is null),

            // Mizan: yalnız geçmiş dönemi olup cari dönemi eksik olan firmada. Hiç mizanı
            // olmayan firma bu maddeyi almaz — onun işareti raydaki kırmızı "!".
            new("Mizan", "cari dönem mizanı yüklenmemiş", "cariMizan", g => g.CariMizanEksik),

            // Sicil
            new("Sicil", "ticaret sicil no", "ticaretSicilNo", g => B(g.Firma.TicaretSicilNo)),
            new("Sicil", "MERSİS no", "mersisNo", g => B(g.Sicil?.MersisNo)),
            new("Sicil", "sermaye", "sermaye", g => g.Sicil?.Sermaye is null),
            new("Sicil", "kuruluş tarihi", "kurulusTarihi", g => g.Sicil?.KurulusTarihi is null),
            new("Sicil", "adres", "adres", g => B(g.Sicil?.Adres)),

            // İlgili kayıtlar
            new("Ortaklık", "ortak kaydı yok", "ortak", g => g.OrtakSayisi == 0),
            new("İmza yetkilileri", "imza yetkilisi yok", "imzaYetkilisi", g => g.YetkiliSayisi == 0),

            // Takip
            new("Takip", "sorumlu atanmamış", "sorumlu", g => g.Firma.SorumluKullaniciId is null)
        };

        /// <summary>Kontrol sayısı: 16 künye alanı + koşullu "cari dönem mizanı" maddesi.</summary>
        public static int KontrolSayisi => Kontroller.Length;

        /// <summary>Boş olan alanlar, ekran sırasıyla.</summary>
        public static List<EksikAlanDto> Eksikler(FirmaKunyeGirdisi girdi)
            => Kontroller
                .Where(k => k.Bos(girdi))
                .Select(k => new EksikAlanDto { Kart = k.Kart, Alan = k.Alan, Odak = k.Odak })
                .ToList();
    }
}
