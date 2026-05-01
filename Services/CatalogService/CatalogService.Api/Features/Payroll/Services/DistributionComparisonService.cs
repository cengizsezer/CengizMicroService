using CatalogService.Api.Features.Payroll.Dtos.Shared;
using CatalogService.Api.Features.Payroll.Entities;
using CatalogService.Api.Features.Payroll.Services.Interfaces;

namespace CatalogService.Api.Features.Payroll.Services
{
    public class DistributionComparisonService : IDistributionComparisonService
    {
        private const decimal BeyanSiniri = 5_300_000m;
        private const decimal GeciciVergiOrani = 0.25m;

        public DistributionComparisonResultDto Compare(
            int year,
            decimal yillikBrut,
            decimal yillikVergiMaliyeti,
            decimal yillikNet,
            decimal stopajOrani,
            IReadOnlyList<PayrollTaxBracket> taxBrackets)
        {
            var huzurHakki = BuildHuzurHakki(yillikBrut, yillikVergiMaliyeti, yillikNet);
            var karDagitimi = BuildKarDagitimi(yillikBrut, stopajOrani, taxBrackets);

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
                BeyanSiniri = BeyanSiniri,
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
            IReadOnlyList<PayrollTaxBracket> taxBrackets)
        {
            var a = Round2(yillikBrut);
            var b = Round2(a * stopajOrani);
            var c = Round2(a - b);

            var d = Round2(a / 2m);
            var e = d;

            var f = CalculateProgressiveTax(e, taxBrackets);
            var ortVergiOrani = e == 0 ? 0m : Round2(f / e * 100m);
            var g = Round2(f - b);
            var netVergiMaliyeti = f;

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

        private decimal CalculateProgressiveTax(
            decimal beyanaTabiGelir,
            IReadOnlyList<PayrollTaxBracket> taxBrackets)
        {
            decimal totalTax = 0m;
            decimal remaining = beyanaTabiGelir;

            foreach (var bracket in taxBrackets.OrderBy(b => b.Order))
            {
                if (remaining <= 0) break;

                var bracketSize = bracket.MaxAmount.HasValue
                    ? bracket.MaxAmount.Value - bracket.MinAmount
                    : decimal.MaxValue;

                var taxableInBracket = Math.Min(remaining, bracketSize);
                totalTax += taxableInBracket * bracket.TaxRate;
                remaining -= taxableInBracket;
            }

            return Round2(totalTax);
        }

        private static decimal Round2(decimal value)
            => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
