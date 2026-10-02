using System.Globalization;
using System.Text;
using CatalogService.Api.Features.Yapilacaklar.Domain;

namespace CatalogService.Api.Features.Yapilacaklar.Services
{
    /// <summary>
    /// İş tarifinin kuralları (Prompt 14) — saf, veritabanı yok. Seed'deki ilk üretim ve ekrandaki
    /// "Tarif oluştur" AYNI fonksiyonu (<see cref="Planla"/>) kullanır.
    ///
    /// <b>Sessiz birleştirme yok.</b> Başlığa göre gruplamak farklı iki işi aynı tarife bağlayabilir
    /// (iki firmadaki "Rapor gönder" aynı iş olmayabilir). Grubun EN DOLU kaydından tarif üretilir;
    /// tarife yalnız metni BOŞ ya da tarifle AYNI olan kayıtlar bağlanır. Metni FARKLI olan kayıt
    /// bağlanmaz, kendi metniyle çalışmaya devam eder ve raporlanır — birleştirmeye kullanıcı karar verir.
    /// </summary>
    public static class IsTarifiKurucu
    {
        private static readonly CultureInfo Tr = new("tr-TR");

        /// <summary>
        /// Karşılaştırma biçimi: Türkçe küçültme (İ→i, I→ı; <c>ToLowerInvariant</c> "İ"yi "i̇" yapar),
        /// noktasız ı da i sayılır ("GIRIS" yazan "giriş"i kastediyor), boşluk, noktalama ve simgeler atılır.
        /// <see cref="Sistemler.Domain.SistemAdi.Normalize"/> ile aynı yaklaşım, daha geniş noktalama.
        /// </summary>
        public static string Normalize(string? metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return string.Empty;

            var kucuk = metin.ToLower(Tr).Normalize(NormalizationForm.FormC);
            var sb = new StringBuilder(kucuk.Length);
            foreach (var c in kucuk)
            {
                if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c) || c == '̇') continue;
                sb.Append(c == 'ı' ? 'i' : c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// İşin tarif grubu — adresteki <c>?is=</c>. Yasal iş kod + tekrarla (adı takvimden gelir, tarifi
        /// ortaktır): "y-0015-2". Özel iş normalize başlık + tekrarla: "o-bordro-2". Aynı başlıklı aylık ve
        /// yıllık iş AYRI tariftir ("Defter tasdiki" yılda bir ile ayda bir aynı iş olamaz). Gün kuralı
        /// gruba GİRMEZ — firmaya göre değişebilir.
        /// </summary>
        public static string Anahtar(FirmaIsi isi)
            => isi.YasalMukellefiyetKodu is { } kod ? YasalAnahtar(kod, isi.Tekrar) : OzelAnahtar(isi.Baslik, isi.Tekrar);

        public static string YasalAnahtar(string kod, IsTekrari tekrar) => $"y-{kod.Trim()}-{(byte)tekrar}";

        public static string OzelAnahtar(string baslik, IsTekrari tekrar) => $"o-{Normalize(baslik)}-{(byte)tekrar}";

        /// <summary>NasilYapilir ya da MenuYolu dolu mu (normalize sonrası).</summary>
        public static bool Dolu(FirmaIsi isi) => Normalize(isi.NasilYapilir).Length > 0 || Normalize(isi.MenuYolu).Length > 0;

        /// <summary>
        /// Kayıt bu tarife kayıpsız bağlanabilir mi: her alan ya BOŞ ya tarifle AYNI (normalize). Bir
        /// alanı farklıysa bağlanmaz — metni kaybolmasın, yanlış tarife bağlanmasın.
        /// </summary>
        public static bool Uyumlu(FirmaIsi isi, string? tarifMenu, string? tarifNasil)
            => AlanUyumlu(isi.NasilYapilir, tarifNasil) && AlanUyumlu(isi.MenuYolu, tarifMenu);

        private static bool AlanUyumlu(string? kayit, string? tarif)
        {
            var k = Normalize(kayit);
            return k.Length == 0 || k == Normalize(tarif);
        }

        /// <summary>Bağlı kaydın tarifle AYNI olmayan kendi metni var mı ("Bu firmada farklı").</summary>
        public static bool FirmayaOzelMetinVar(FirmaIsi isi, string? tarifMenu, string? tarifNasil)
            => !Uyumlu(isi, tarifMenu, tarifNasil);

        /// <summary>Bir grubun üretim planı.</summary>
        /// <param name="Kaynak">Tarifin üretileceği EN DOLU kayıt; grupta dolu kayıt yoksa boş (tarif üretilmez).</param>
        /// <param name="Baglanacaklar">Metni boş ya da tarifle aynı kayıtlar (kaynak dahil).</param>
        /// <param name="Farklilar">Metni FARKLI olduğu için bağlanmayan kayıtlar — raporlanır.</param>
        public sealed record GrupPlani(string Anahtar, string Baslik, FirmaIsi? Kaynak,
                                       List<FirmaIsi> Baglanacaklar, List<FirmaIsi> Farklilar)
        {
            /// <summary>Farklı kayıtlardaki birbirinden farklı metin sayısı (raporun "kaç farklı metin"i).</summary>
            public int FarkliMetinSayisi => Farklilar
                .Select(i => (Normalize(i.MenuYolu), Normalize(i.NasilYapilir)))
                .Distinct()
                .Count();
        }

        /// <summary>
        /// Tarife henüz bağlanmamış kayıtları gruplar ve her grup için planı çıkarır. Zaten bağlı
        /// (<see cref="FirmaIsi.IsTarifiId"/> dolu) kayıtlar plana girmez.
        /// </summary>
        public static List<GrupPlani> Planla(IEnumerable<FirmaIsi> isler)
        {
            var planlar = new List<GrupPlani>();
            foreach (var g in isler.Where(i => i.IsTarifiId is null).GroupBy(Anahtar).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var uyeler = g.OrderBy(i => i.Id).ToList();

                // EN DOLU: normalize metin uzunluğu en büyük; eşitse en eski kayıt.
                var kaynak = uyeler.Where(Dolu)
                    .OrderByDescending(i => Normalize(i.NasilYapilir).Length + Normalize(i.MenuYolu).Length)
                    .ThenBy(i => i.Id)
                    .FirstOrDefault();

                if (kaynak is null)
                {
                    planlar.Add(new GrupPlani(g.Key, uyeler[0].Baslik.Trim(), null, new(), new()));
                    continue;
                }

                var bagla = uyeler.Where(i => Uyumlu(i, kaynak.MenuYolu, kaynak.NasilYapilir)).ToList();
                var farkli = uyeler.Where(i => !Uyumlu(i, kaynak.MenuYolu, kaynak.NasilYapilir)).ToList();
                planlar.Add(new GrupPlani(g.Key, kaynak.Baslik.Trim(), kaynak, bagla, farkli));
            }
            return planlar;
        }

        /// <summary>Plandan tarif kaydı (kaydedilmez).</summary>
        public static IsTarifi Tarif(FirmaIsi kaynak, DateTime simdiUtc) => new()
        {
            Ad = kaynak.Baslik.Trim() is { Length: > 200 } uzun ? uzun[..200] : kaynak.Baslik.Trim(),
            SistemId = kaynak.SistemId,
            MenuYolu = string.IsNullOrWhiteSpace(kaynak.MenuYolu) ? null : kaynak.MenuYolu.Trim(),
            NasilYapilir = string.IsNullOrWhiteSpace(kaynak.NasilYapilir) ? null : kaynak.NasilYapilir.Trim(),
            OlusturmaZamani = simdiUtc
        };
    }
}
