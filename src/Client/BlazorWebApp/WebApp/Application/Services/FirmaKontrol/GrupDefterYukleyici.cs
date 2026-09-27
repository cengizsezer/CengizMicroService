using System.Diagnostics;
using WebApp.Application.Services.Interfaces;
using WebApp.Domain.Models.FirmaKontrol;
using WebApp.Shared.Dto.FirmaKontrol;

namespace WebApp.Application.Services.FirmaKontrol
{
    /// <summary>
    /// Grup görünümü için defterleri (firma ağacı + o firmada geçerli kurallar) okur.
    ///
    /// • PARALEL: her firma aynı anda okunur. Diğer firmalar kilitli tam hidrasyondan
    ///   geçmez (<see cref="IFirmaKontrolService.GetHesapAgaciHafifAsync"/>) — eskiden tek
    ///   hidrasyon kilidi okumaları fiilen sıraya diziyordu.
    /// • ZAMAN SINIRI: firma başına <see cref="Zamanasimi"/>. Aşılırsa o satır "ulaşılamadı"
    ///   olur; sonsuz (ya da HttpClient'ın 100 sn'lik) bekleme yok.
    /// • AŞAMALI: her defter hazır olunca <c>defterGeldi</c> çağrılır; ekran tüm firmaları
    ///   beklemeden satır satır dolar.
    /// • Süre ölçülür ve konsola firma başına ms olarak yazılır.
    ///
    /// Dayanıklılık: her firma AYRI korunur, istisna yukarı sızmaz (global toast çıkmaz).
    /// Yalnızca OKUR; hiçbir resmi hesaba veri vermez.
    /// </summary>
    public static class GrupDefterYukleyici
    {
        public static readonly TimeSpan Zamanasimi = TimeSpan.FromSeconds(10);

        public static async Task<List<GrupGorunumu.Defter>> YukleAsync(
            IFirmaKontrolService firmaKontrol,
            IEtiketApiClient etiketApi,
            int bakilanFirmaId,
            EtiketBoyutuDto boyut,
            IReadOnlyList<int> firmaIdleri,
            Func<int, string> firmaAdi,
            Action<GrupGorunumu.Defter>? defterGeldi = null,
            TimeSpan? zamanasimi = null)
        {
            var sinir = zamanasimi ?? Zamanasimi;
            var gorevler = firmaIdleri
                .Select(firmaId => TekDefterAsync(firmaKontrol, etiketApi, bakilanFirmaId, boyut, firmaId, firmaAdi(firmaId), defterGeldi, sinir))
                .ToList();   // hepsi burada başlar — eş zamanlı

            return (await Task.WhenAll(gorevler)).ToList();
        }

        private static async Task<GrupGorunumu.Defter> TekDefterAsync(
            IFirmaKontrolService firmaKontrol,
            IEtiketApiClient etiketApi,
            int bakilanFirmaId,
            EtiketBoyutuDto boyut,
            int firmaId,
            string ad,
            Action<GrupGorunumu.Defter>? defterGeldi,
            TimeSpan sinir)
        {
            var sure = Stopwatch.StartNew();
            using var cts = new CancellationTokenSource(sinir);

            GrupGorunumu.Defter defter;
            try
            {
                // WaitAsync: iptale uymayan bir çağrı olsa bile zaman sınırı kesin işler.
                defter = await OkuAsync(firmaKontrol, etiketApi, bakilanFirmaId, boyut, firmaId, ad, cts.Token)
                    .WaitAsync(sinir);
            }
            catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
            {
                defter = GrupGorunumu.UlasilamayanDefter(firmaId, ad,
                    $"{sinir.TotalSeconds:0} saniye içinde yanıt gelmedi.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GrupDefter HATA] firma={firmaId} {ex.Message}");
                defter = GrupGorunumu.UlasilamayanDefter(firmaId, ad);
            }

            sure.Stop();
            Console.WriteLine($"[GrupDefter] firma={firmaId} ({ad}) {sure.ElapsedMilliseconds} ms · {defter.Durum}");

            try { defterGeldi?.Invoke(defter); }
            catch (Exception ex) { Console.WriteLine($"[GrupDefter geri çağrı HATA] {ex.Message}"); }

            return defter;
        }

        private static async Task<GrupGorunumu.Defter> OkuAsync(
            IFirmaKontrolService firmaKontrol,
            IEtiketApiClient etiketApi,
            int bakilanFirmaId,
            EtiketBoyutuDto boyut,
            int firmaId,
            string ad,
            CancellationToken ct)
        {
            if (firmaId == bakilanFirmaId)
            {
                // Bakılan firma zaten hidre: ağaç bellekten, kurallar elde.
                var kendi = await firmaKontrol.GetHesapAgaciSonucuAsync(firmaId, Donem.Cari);
                return GrupGorunumu.DefterOlustur(firmaId, ad, kendi, boyut.KapsamHesaplari, boyut.Kurallar);
            }

            // Önce ağaç: mizanı olmayan firmada kurallar hiç sorulmaz, hızlıca düşer.
            var agac = await firmaKontrol.GetHesapAgaciHafifAsync(firmaId, Donem.Cari, ct);
            if (agac.Durum != HesapAgaciDurumu.Yuklu)
                return GrupGorunumu.DefterOlustur(firmaId, ad, agac, boyut.KapsamHesaplari, Array.Empty<EtiketKuraliDto>());

            var (kurallar, not) = await KurallariAlAsync(etiketApi, firmaId, boyut.Id, ct);
            return GrupGorunumu.DefterOlustur(firmaId, ad, agac, boyut.KapsamHesaplari, kurallar, not);
        }

        /// <summary>
        /// Diğer firmada geçerli kurallar: aynı boyutun o firmadan görünen hâli (FirmaId null
        /// ya da o firma olan kurallar). Boyut o firmada görünmüyorsa (kapsamı "sadece bu
        /// firma") kural yoktur — defterin tamamı Atanmamış'a düşer ve satıra not yazılır.
        /// </summary>
        private static async Task<(List<EtiketKuraliDto> Kurallar, string? Not)> KurallariAlAsync(
            IEtiketApiClient etiketApi, int firmaId, int boyutId, CancellationToken ct)
        {
            var boyutlar = await etiketApi.GetBoyutlarAsync(firmaId, ct);
            var karsilik = boyutlar.FirstOrDefault(b => b.Id == boyutId);

            return karsilik is null
                ? (new List<EtiketKuraliDto>(), "Bu boyut bu firmada geçerli değil; tamamı atanmamış sayıldı.")
                : (karsilik.Kurallar, null);
        }
    }
}
