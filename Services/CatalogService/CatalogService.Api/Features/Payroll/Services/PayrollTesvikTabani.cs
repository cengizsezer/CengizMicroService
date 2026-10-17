using CatalogService.Api.Features.Payroll.Configuration;
using CatalogService.Api.Features.Payroll.Entities;

namespace CatalogService.Api.Features.Payroll.Services
{
    /// <summary>
    /// Teşvik tabanları ve kapsanan prim oranları — 2026+ hesaplayıcı ile motorun
    /// (sigortalı hissesi) ortak tek hesap yeri.
    /// </summary>
    public static class PayrollTesvikTabani
    {
        /// <summary>Prime esas kazanç: min(brüt, asgari ücret × tavan katsayısı).</summary>
        public static decimal SgkMatrahi(decimal brut, PayrollParameter p) =>
            Round2(Math.Min(brut, p.MinimumWageGrossAmount * p.SgkCeilingMultiplier));

        /// <summary>Asgari ücret tabanı: min(brüt, yılın asgari ücreti).</summary>
        public static decimal AsgariUcret(decimal brut, PayrollParameter p) =>
            Round2(Math.Max(0, Math.Min(brut, p.MinimumWageGrossAmount)));

        public static decimal Taban(TesvikTabani taban, decimal brut, PayrollParameter p) => taban switch
        {
            TesvikTabani.AsgariUcret => AsgariUcret(brut, p),
            _ => SgkMatrahi(brut, p)
        };

        /// <summary>
        /// Brüt işveren SGK primi ve işveren İSP'si (2026+). Emeklide SGDP + KVSK, GSS ve İSP yok;
        /// normal çalışanda MYÖ + GSS + KVSK ve İSP.
        /// </summary>
        public static (decimal Sgk, decimal Isp) IsverenPrimi(decimal sgkMatrahi, PayrollParameter p, decimal? sgdpOrani) =>
            sgdpOrani is { } sgdp
                ? (Round2(sgkMatrahi * (sgdp + p.SgkEmployerKVSKRate)), 0m)
                : (Round2(sgkMatrahi * IsverenSgkOrani(TesvikKalemleri.IsverenSgk, p)), Round2(sgkMatrahi * p.UnemploymentEmployerRate));

        /// <summary>4447/50 (15921): işverenin KVSK hissesinden karşılanan kısım.</summary>
        public const decimal KvskBirPuan = 0.01m;

        /// <summary>Kalemlerdeki işveren SGK oranlarının toplamı (MYÖ/GSS/KVSK; İSP ve sigortalı hariç).</summary>
        public static decimal IsverenSgkOrani(TesvikKalemleri kalemler, PayrollParameter p)
        {
            var oran = 0m;
            if (kalemler.HasFlag(TesvikKalemleri.IsverenMyo)) oran += p.SgkEmployerMYORate;
            if (kalemler.HasFlag(TesvikKalemleri.IsverenGss)) oran += p.SgkEmployerGSSRate;
            if (kalemler.HasFlag(TesvikKalemleri.IsverenKvsk)) oran += p.SgkEmployerKVSKRate;
            else if (kalemler.HasFlag(TesvikKalemleri.IsverenKvskBirPuan)) oran += Math.Min(KvskBirPuan, p.SgkEmployerKVSKRate);
            return oran;
        }

        /// <summary>
        /// Sigortalı hissesinden karşılanan tutar (26322, 15921, 02828 — tek hesap yeri):
        /// asgari ücret tabanında, kesilen primi aşamaz. Kesintiden düşer → net artar; GV matrahı değişmez.
        /// </summary>
        public static decimal SigortaliTesviki(
            TesvikKalemleri kalemler, decimal brut, decimal sgkSigortali, decimal ispSigortali, PayrollParameter p)
        {
            var taban = AsgariUcret(brut, p);
            var tutar = 0m;
            if (kalemler.HasFlag(TesvikKalemleri.SigortaliSgk))
                tutar += Math.Min(sgkSigortali, Round2(taban * p.SgkEmployeeRate));
            if (kalemler.HasFlag(TesvikKalemleri.SigortaliIsp))
                tutar += Math.Min(ispSigortali, Round2(taban * p.UnemploymentEmployeeRate));
            return Round2(tutar);
        }

        private static decimal Round2(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
