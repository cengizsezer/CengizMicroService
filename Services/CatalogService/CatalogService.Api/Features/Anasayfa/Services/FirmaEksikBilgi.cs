using CatalogService.Api.Features.Anasayfa.Dtos;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.FirmaBilgileri.Domain;

namespace CatalogService.Api.Features.Anasayfa.Services
{
    /// <summary>Bir firmanın eksik bilgi kontrolünün girdisi.</summary>
    /// <param name="CariMizanEksik">Geçmiş dönem mizanı var, cari dönemin mizanı yok.</param>
    /// <param name="MuhasebeProgramiVar">Firmanın "Kullanılan sistemler"inde Muhasebe türünde sistem var (Prompt 9).</param>
    /// <param name="GecerliYetkiliSayisi">Yetkisi bugün geçerli (bitişi boş ya da bugün/sonrası) yetkili sayısı;
    /// verilmezse <paramref name="YetkiliSayisi"/>.</param>
    public sealed record FirmaKunyeGirdisi(Firma Firma, FirmaSicilBilgisi? Sicil, int OrtakSayisi, int YetkiliSayisi,
                                           bool CariMizanEksik = false, bool MuhasebeProgramiVar = false,
                                           int? GecerliYetkiliSayisi = null);

    /// <summary>Raydaki rozetin türü — renk ve metin istemcide bu değerden; karar burada tek yerde.</summary>
    public enum KunyeRozeti : byte
    {
        /// <summary>Gerekli eksik de uyarı da yok: rozet ÇIKMAZ (yeşil tik da yok — gürültü).</summary>
        Yok = 0,

        /// <summary>Kırmızı: "N gerekli alan eksik".</summary>
        GerekliEksik = 1,

        /// <summary>Amber: gerekli eksik yok, uyarı var — "N uyarı".</summary>
        Uyari = 2,

        /// <summary>Gri: yeni firma (tolerans içinde) ve gerekli eksik var — "Künye tamamlanıyor · N gerekli alan".</summary>
        Tamamlaniyor = 3
    }

    /// <summary>
    /// Anasayfa künyesinin eksik bilgi kontrolleri — <b>tek tanım yeri</b>. Sol raydaki
    /// rozet ile sağdaki "Eksik bilgi" şeridi aynı listeden besleniyor; bir alan
    /// eklenir ya da çıkarılırsa ikisi birlikte değişir.
    ///
    /// <b>Gerekli / ikincil (Prompt 12).</b> "13 eksik" bir şey söylemiyordu; "3 gerekli alan eksik"
    /// ne yapılacağını söyler. Rozet YALNIZ gerekli alanları sayar. İkincil alanlar zamanla dolar,
    /// listede durur (kartta "—"), rozete girmez. Koşullu mizan maddesi bir künye eksiği değil bir
    /// DURUM'dur: rozete girmez, raydaki kırmızı "!" (Prompt 6B) ayrı göstergedir.
    ///
    /// Sıra kartların ekrandaki sırası: şerit grupları ve "Tamamla" ilk eksiği bu sırayla seçer.
    /// </summary>
    public static class FirmaEksikBilgi
    {
        /// <summary>
        /// YENİ FİRMA TOLERANSI: firma oluşturulalı bu kadar günden az ise gerekli eksikler kırmızı değil
        /// gri "Künye tamamlanıyor" görünür. Açıldığı gün künye zaten boş; ilk günden kırmızı rozet rozetin
        /// anlamını yok eder. Sayım AYNEN sürer — eksik saklanmaz, yalnız renk ve metin değişir.
        /// </summary>
        public const int YeniFirmaToleransGun = 7;

        private enum Tur : byte { Gerekli, Ikincil, Durum }

        private sealed record Kontrol(string Kart, string Alan, string Odak, Tur Tur, Func<FirmaKunyeGirdisi, bool> Bos);

        private static bool B(string? metin) => string.IsNullOrWhiteSpace(metin);

        private static readonly Kontrol[] Kontroller =
        {
            // Mükellefiyet
            new("Mükellefiyet", "vergi dairesi", "vergiDairesi", Tur.Gerekli, g => B(g.Firma.VergiDairesi)),
            new("Mükellefiyet", "VKN", "vkn", Tur.Gerekli, g => B(g.Firma.VergiKimlikNo)),
            new("Mükellefiyet", "NACE kodu", "naceKodu", Tur.Ikincil, g => B(g.Sicil?.NaceKodu)),
            new("Mükellefiyet", "işe başlama", "iseBaslama", Tur.Ikincil, g => g.Sicil?.IseBaslamaTarihi is null),

            // Sınıflandırma
            new("Sınıflandırma", "defter usulü", "defterUsulu", Tur.Gerekli, g => g.Firma.DefterUsulu == DefterUsulu.Belirsiz),
            new("Sınıflandırma", "vergi türü", "vergiTuru", Tur.Gerekli, g => g.Firma.VergiTuru == VergiTuru.Belirsiz),
            new("Sınıflandırma", "hesap dönemi", "hesapDonemi", Tur.Gerekli, g => g.Firma.HesapDonemi is null),

            // Kullanılan sistemler (Prompt 9): mizan formatı bu karta taşındı; muhasebe programı firmanın
            // Muhasebe türündeki sistemi (Prompt 12 ile eklendi).
            new("Kullanılan sistemler", "mizan formatı", "mizanFormati", Tur.Gerekli, g => !MizanFormatlari.Secili(g.Firma.MizanFormati)),
            new("Kullanılan sistemler", "muhasebe programı", "muhasebeProgrami", Tur.Gerekli, g => !g.MuhasebeProgramiVar),

            // Mizan: DURUM, eksik değil. Yalnız geçmiş dönemi olup cari dönemi eksik olan firmada.
            // Hiç mizanı olmayan firma bu maddeyi almaz — onun işareti raydaki kırmızı "!".
            new("Mizan", "cari dönem mizanı yüklenmemiş", "cariMizan", Tur.Durum, g => g.CariMizanEksik),

            // Sicil
            new("Sicil", "ticaret sicil no", "ticaretSicilNo", Tur.Ikincil, g => B(g.Firma.TicaretSicilNo)),
            new("Sicil", "MERSİS no", "mersisNo", Tur.Ikincil, g => B(g.Sicil?.MersisNo)),
            new("Sicil", "sermaye", "sermaye", Tur.Ikincil, g => g.Sicil?.Sermaye is null),
            new("Sicil", "kuruluş tarihi", "kurulusTarihi", Tur.Ikincil, g => g.Sicil?.KurulusTarihi is null),
            new("Sicil", "adres", "adres", Tur.Ikincil, g => B(g.Sicil?.Adres)),

            // İlgili kayıtlar
            new("Ortaklık", "ortak kaydı yok", "ortak", Tur.Ikincil, g => g.OrtakSayisi == 0),
            // Prompt 12: "imza yetkilisi yok" yerine "geçerli imza yetkilisi" — yetkisi dolmuş yetkili de
            // firmayı imzalayamaz. Hiç yetkili yoksa da bu madde düşer.
            new("İmza yetkilileri", "geçerli imza yetkilisi", "imzaYetkilisi", Tur.Gerekli,
                g => (g.GecerliYetkiliSayisi ?? g.YetkiliSayisi) == 0),

            // Takip
            new("Takip", "sorumlu atanmamış", "sorumlu", Tur.Gerekli, g => g.Firma.SorumluKullaniciId is null)
        };

        /// <summary>Kontrol sayısı: 17 künye alanı + koşullu "cari dönem mizanı" durumu.</summary>
        public static int KontrolSayisi => Kontroller.Length;

        /// <summary>Gerekli kontrol sayısı (rozetin en çok gösterebileceği).</summary>
        public static int GerekliSayisi => Kontroller.Count(k => k.Tur == Tur.Gerekli);

        /// <summary>Boş olan alanlar (gerekli + ikincil + durum), ekran sırasıyla; her biri türünü taşır.</summary>
        public static List<EksikAlanDto> Eksikler(FirmaKunyeGirdisi girdi)
            => Kontroller
                .Where(k => k.Bos(girdi))
                .Select(k => new EksikAlanDto
                {
                    Kart = k.Kart, Alan = k.Alan, Odak = k.Odak,
                    Gerekli = k.Tur == Tur.Gerekli,
                    Durum = k.Tur == Tur.Durum
                })
                .ToList();

        /// <summary>Firma oluşturulalı <see cref="YeniFirmaToleransGun"/> günden az mı (yerel tarih).</summary>
        public static bool YeniFirma(DateTime olusturmaUtc, DateTime bugun)
            => (bugun.Date - DateTime.SpecifyKind(olusturmaUtc, DateTimeKind.Utc).ToLocalTime().Date).Days < YeniFirmaToleransGun;

        /// <summary>Rozet kararı — gerekli eksik sayısı, uyarı sayısı ve yeni firma toleransından.</summary>
        public static KunyeRozeti Rozet(int gerekliEksik, int uyari, bool yeniFirma)
            => gerekliEksik > 0 ? (yeniFirma ? KunyeRozeti.Tamamlaniyor : KunyeRozeti.GerekliEksik)
             : uyari > 0 ? KunyeRozeti.Uyari
             : KunyeRozeti.Yok;
    }
}
