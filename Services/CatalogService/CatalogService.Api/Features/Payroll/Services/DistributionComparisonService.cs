using CatalogService.Api.Features.Payroll.Dtos.Shared;
using CatalogService.Api.Features.Payroll.Services.Interfaces;

namespace CatalogService.Api.Features.Payroll.Services
{
    public class DistributionComparisonService : IDistributionComparisonService
    {
        // Huzur Hakkı tarafı geçici vergi oranı. (Sabit — Değişiklik 2 kapsamı dışı.)
        private const decimal GeciciVergiOrani = 0.25m;

        // ──────────────────────────────────────────────────────────────────────
        // BEYAN SINIRI — yıl bazlı  (Madde 6 kararı → Seçenek C: kod-içi statik tablo)
        //
        // KAPSAM: yalnızca beyan sınırı eşiği yıla göre değişir. Vergi dilimleri,
        // oranlar ve kümülatif sabitler DEĞİŞMEZ — hesap motoru
        // CalculateNonSalaryProgressiveTax2026 olduğu gibi korunur.
        //
        // Tanımsız bir yıl seçilirse ResolveBeyanSiniri SESSİZCE yanlış sonuç
        // üretmez; açık bir hata fırlatır.
        // ──────────────────────────────────────────────────────────────────────

        private static readonly IReadOnlyDictionary<int, decimal> BeyanSiniriByYear =
            new Dictionary<int, decimal>
            {
                // GVK mükerrer 86 yıllık beyan sınırları (muhasebe/kullanıcı teyitli)
                [2023] = 1_900_000m,
                [2024] = 3_000_000m,
                [2025] = 4_300_000m,
                [2026] = 5_300_000m,
            };

        public DistributionComparisonResultDto Compare(
            int year,
            decimal yillikBrut,
            decimal yillikVergiMaliyeti,
            decimal yillikNet,
            decimal stopajOrani)
        {
            var beyanSiniri = ResolveBeyanSiniri(year);

            var huzurHakki = BuildHuzurHakki(yillikBrut, yillikVergiMaliyeti, yillikNet);
            var karDagitimi = BuildKarDagitimi(yillikBrut, stopajOrani, beyanSiniri);

            var hhMaliyeti = huzurHakki.G_NetVergiMaliyeti;
            var kdMaliyeti = karDagitimi.NetVergiMaliyeti;

            AvantajliYontem avantajli;
            if (Math.Abs(hhMaliyeti - kdMaliyeti) < 0.01m)
                avantajli = AvantajliYontem.Esit;
            else
                avantajli = hhMaliyeti < kdMaliyeti
                    ? AvantajliYontem.HuzurHakki
                    : AvantajliYontem.KarDagitimi;

            return new DistributionComparisonResultDto
            {
                Year = year,
                StopajOrani = stopajOrani,
                BeyanSiniri = beyanSiniri,
                HuzurHakki = huzurHakki,
                KarDagitimi = karDagitimi,
                AvantajliYontem = avantajli,
                NetFark = Round2(Math.Abs(hhMaliyeti - kdMaliyeti))
            };
        }

        private HuzurHakkiBreakdownDto BuildHuzurHakki(
            decimal yillikBrut,
            decimal yillikVergiMaliyeti,
            decimal yillikNet)
        {
            var a = Round2(yillikBrut);
            var b = Round2(yillikVergiMaliyeti);
            var c = a == 0 ? 0m : Round2(b / a * 100m);
            var d = Round2(yillikNet);
            var e = GeciciVergiOrani;
            var f = Round2(a * e);
            var g = Round2(b - f);

            return new HuzurHakkiBreakdownDto
            {
                A_YillikBrut = a,
                B_VergiMaliyeti = b,
                C_OrtalamaVergiOrani = c,
                D_NetEleGecen = d,
                E_GeciciVergiOrani = e,
                F_GeciciVergiAvantaji = f,
                G_NetVergiMaliyeti = g
            };
        }

        private KarDagitimiBreakdownDto BuildKarDagitimi(
            decimal yillikBrut,
            decimal stopajOrani,
            decimal beyanSiniri)
        {
            var a = Round2(yillikBrut);
            var b = Round2(a * stopajOrani);
            var c = Round2(a - b);

            var d = Round2(a / 2m);
            var e = d;

            decimal f;
            decimal ortVergiOrani;
            decimal g;
            decimal netVergiMaliyeti;

            // GVK mükerrer 86: brüt kar dağıtımı beyan sınırını AŞARSA yıllık beyanname
            // verilir. Sınıra eşit olan beyan vermez → karşılaştırma ">" (">=" değil).
            if (a > beyanSiniri)
            {
                // Beyan var → ücret-dışı tarifeyle (1M kırılma noktası) artan oranlı vergi.
                // Hesap motoru yıl bazlı DEĞİL; tarife sabit korunuyor.
                f = CalculateNonSalaryProgressiveTax2026(e);
                ortVergiOrani = e == 0 ? 0m : Round2(f / e * 100m);
                g = Round2(f - b);
                netVergiMaliyeti = f;
            }
            else
            {
                // Beyan yok → stopaj (B) nihai vergidir.
                f = 0m;
                ortVergiOrani = 0m;
                g = 0m;
                netVergiMaliyeti = b;
            }

            return new KarDagitimiBreakdownDto
            {
                A_BrutKarDagitimi = a,
                StopajOrani = stopajOrani,
                B_Stopaj = b,
                C_NetEleGecen = c,
                D_YuzdeElliIstisna = d,
                E_BeyanaTabiGelir = e,
                F_GelirVergisi = f,
                OrtalamaVergiOrani = ortVergiOrani,
                G_IlaveVergi = g,
                NetVergiMaliyeti = netVergiMaliyeti
            };
        }

        private static decimal ResolveBeyanSiniri(int year)
        {
            if (!BeyanSiniriByYear.TryGetValue(year, out var value))
                throw new InvalidOperationException(
                    $"'{year}' yılı için beyan sınırı tanımlı değil. " +
                    $"DistributionComparisonService.BeyanSiniriByYear sözlüğüne ekleyin.");

            return value;
        }

        private static decimal Round2(decimal value)
            => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        // ── HESAP MOTORU — DEĞİŞMEDİ ────────────────────────────────────────────
        // Kar dağıtımı (ücret dışı gelir) artan oranlı gelir vergisi.
        // Dilimler / oranlar / kümülatif sabitler mevzuata gömülü, yıldan bağımsız.
        private static decimal CalculateNonSalaryProgressiveTax2026(decimal matrah)
        {
            if (matrah <= 0m) return 0m;

            decimal tax;
            if (matrah <= 190_000m)
                tax = matrah * 0.15m;
            else if (matrah <= 400_000m)
                tax = 28_500m + (matrah - 190_000m) * 0.20m;
            else if (matrah <= 1_000_000m)
                tax = 70_500m + (matrah - 400_000m) * 0.27m;
            else if (matrah <= 5_300_000m)
                tax = 232_500m + (matrah - 1_000_000m) * 0.35m;
            else
                tax = 1_737_500m + (matrah - 5_300_000m) * 0.40m;

            return Round2(tax);
        }
    }
}
