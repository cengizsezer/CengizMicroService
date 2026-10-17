using CatalogService.Api.Features.Payroll.Configuration;
using CatalogService.Api.Features.Payroll.Dtos.Responses;
using CatalogService.Api.Features.Payroll.Enums;
using CatalogService.Api.Features.Payroll.Services.Interfaces;
using CatalogService.Api.Features.Payroll.Services.Models;

namespace CatalogService.Api.Features.Payroll.Services.Strategies
{
    /// <summary>Kural dışı, isteğe bağlı teşvik girdileri.</summary>
    public sealed record TesvikGirdileri(
        int Yil,
        int TurizmPrimGunu,
        decimal IsgDestegiTutari,
        PayrollManuelTesvikTuru? ManuelTur,
        decimal ManuelDeger);

    /// <summary>
    /// 2026+ bütün teşviklerin TEK hesaplayıcısı: kuralı <see cref="PayrollTesvikKurallari"/>
    /// tablosundan okur, teşvik başına sınıf yok. 2026 öncesi yıllar eski strateji
    /// sınıflarında (dondurulmuş) kalır.
    /// </summary>
    public sealed class TesvikKuraliStratejisi : IPayrollIncentiveStrategy
    {
        private readonly CozulmusTesvik _tesvik;
        private readonly TesvikGirdileri _girdi;

        public TesvikKuraliStratejisi(CozulmusTesvik tesvik, TesvikGirdileri girdi)
        {
            _tesvik = tesvik;
            _girdi = girdi;
        }

        public void EnrichEmployerCosts(CalculatePayrollResponse response, PayrollCalculationContext context)
        {
            var p = context.Parameter;
            var kural = _tesvik.Kural;

            foreach (var month in response.Months)
            {
                // Prime esas kazanç = ücret + SGK'ya tabi yol/yemek (yan haksız: yalnız ücret). SGDP'de de aynı.
                var primeEsas = Round2(month.GrossSalary + month.SgkTaxableBenefits);
                var sgkBase = PayrollTesvikTabani.SgkMatrahi(primeEsas, p);
                var (sgkEmployerGross, unemploymentEmployerGross) =
                    PayrollTesvikTabani.IsverenPrimi(sgkBase, p, context.RetiredSgdpEmployerRate);

                decimal sgkIncentive = 0m, unemploymentIncentive = 0m;
                decimal? support = kural.AyriSatirDestek ? 0m : null;

                if (month.GrossSalary > 0)
                {
                    switch (kural.Hesap)
                    {
                        case TesvikHesapTuru.PrimIndirimi:
                        {
                            var taban = PayrollTesvikTabani.Taban(kural.Taban, primeEsas, p);
                            // TODO-TEYIT: emekli (SGDP) + 4691/5746: teşvik normal işveren oranının (%21,75) yarısı
                            // olarak hesaplanıp SGDP brütünden düşüyor. Karar: resmî teyide kadar bu haliyle kalır;
                            // SGDP hissesinin (%24,75) yarısı olduğu teyit edilirse oran burada değişir.
                            var kapsanan = Round2(taban * PayrollTesvikTabani.IsverenSgkOrani(_tesvik.Kalemler, p));
                            sgkIncentive = Round2(kapsanan * _tesvik.Oran);
                            if (_tesvik.Kalemler.HasFlag(TesvikKalemleri.IsverenIsp))
                                unemploymentIncentive = Round2(Round2(taban * p.UnemploymentEmployerRate) * _tesvik.Oran);
                            break;
                        }
                        case TesvikHesapTuru.Puan5510:
                        {
                            var puan = context.IsManufacturingSector
                                ? p.Incentive05510ManufacturingRate
                                : p.Incentive05510TreasuryRate;
                            sgkIncentive = Round2(sgkBase * puan);
                            break;
                        }
                        case TesvikHesapTuru.Manuel:
                            sgkIncentive = _girdi.ManuelTur == PayrollManuelTesvikTuru.Tutar
                                ? Round2(_girdi.ManuelDeger)
                                : Round2(sgkBase * _girdi.ManuelDeger / 100m);
                            break;
                        case TesvikHesapTuru.GunlukUcretDestegi:
                            if (DonemIcinde(kural, _girdi.Yil, month.Month))
                                support = Round2(kural.GunlukDestekTutari * Math.Clamp(_girdi.TurizmPrimGunu, 0, 30));
                            break;
                        case TesvikHesapTuru.AylikTutarDestegi:
                            support = Round2(Math.Max(0, _girdi.IsgDestegiTutari));
                            break;
                    }
                }

                // Teşvik, karşıladığı primi aşamaz.
                sgkIncentive = Math.Clamp(sgkIncentive, 0m, sgkEmployerGross);
                unemploymentIncentive = Math.Clamp(unemploymentIncentive, 0m, unemploymentEmployerGross);

                var sgkEmployerNet = Round2(sgkEmployerGross - sgkIncentive);
                var unemploymentEmployerNet = Round2(unemploymentEmployerGross - unemploymentIncentive);

                month.SgkEmployerGross = sgkEmployerGross;
                month.SgkEmployerIncentive = sgkIncentive;
                month.SgkEmployerNet = sgkEmployerNet;
                month.UnemploymentEmployerAmount = unemploymentEmployerNet;
                month.EmployerSupportAmount = support;
                month.EmployerSupportSource = kural.AyriSatirDestek ? kural.Kaynak : null;
                // İşveren maliyeti: ücret + yol/yemek (nakit ve kart, istisnalı kısım dahil) + net primler − destek.
                month.TotalEmployerCost = Round2(month.GrossSalary + month.MealAmount + month.TransportAmount
                    + sgkEmployerNet + unemploymentEmployerNet - (support ?? 0m));
                month.IncentiveSource = kural.Hesap is TesvikHesapTuru.Yok || kural.AyriSatirDestek ? null : kural.Kaynak;
            }

            if (response.Totals is null) return;

            response.Totals.TotalSgkEmployerGross = Round2(response.Months.Sum(x => x.SgkEmployerGross ?? 0));
            response.Totals.TotalSgkEmployerIncentive = Round2(response.Months.Sum(x => x.SgkEmployerIncentive ?? 0));
            response.Totals.TotalSgkEmployerNet = Round2(response.Months.Sum(x => x.SgkEmployerNet ?? 0));
            response.Totals.TotalUnemploymentEmployerAmount = Round2(response.Months.Sum(x => x.UnemploymentEmployerAmount ?? 0));
            response.Totals.TotalEmployerSupportAmount = kural.AyriSatirDestek
                ? Round2(response.Months.Sum(x => x.EmployerSupportAmount ?? 0))
                : null;
            response.Totals.TotalEmployerCost = Round2(response.Months.Sum(x => x.TotalEmployerCost ?? 0));
        }

        private static bool DonemIcinde(TesvikKurali kural, int yil, int ay)
        {
            var donem = yil * 100 + ay;
            if (kural.DestekBaslangic is { } b && donem < b.Yil * 100 + b.Ay) return false;
            if (kural.DestekBitis is { } s && donem > s.Yil * 100 + s.Ay) return false;
            return true;
        }

        private static decimal Round2(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
