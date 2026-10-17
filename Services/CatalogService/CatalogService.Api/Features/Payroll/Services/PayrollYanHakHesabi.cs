using CatalogService.Api.Features.Payroll.Configuration;
using CatalogService.Api.Features.Payroll.Dtos.Requests;
using CatalogService.Api.Features.Payroll.Enums;

namespace CatalogService.Api.Features.Payroll.Services
{
    /// <summary>Bir ayın yol / yemek yan hakları — istisnalar düşülmüş, ücrete eklenecek tabi kısımlar.</summary>
    public sealed record YanHakSonucu(
        decimal Yemek,
        decimal Yol,
        decimal SgkyaTabi,
        decimal GvyeTabi,
        decimal Nakit,
        decimal Kart)
    {
        public static readonly YanHakSonucu Yok = new(0m, 0m, 0m, 0m, 0m, 0m);

        public decimal Toplam => Yemek + Yol;
    }

    /// <summary>
    /// Yol / yemek yan hakkının TEK hesap yeri (2026+). Kalem başına:
    /// tutar = gün × günlük; istisna = gün × günlük istisna; tabi kısım = max(0, tutar − istisna).
    /// GV ve DV aynı istisnayı kullanır; SGK'nın kendi istisnası var.
    /// </summary>
    public static class PayrollYanHakHesabi
    {
        public static YanHakSonucu Hesapla(PayrollYolYemekDto? girdi, int yil, int ay)
        {
            if (girdi is null) return YanHakSonucu.Yok;

            var yemek = Kalem(YanHakKalemi.Yemek, girdi.YemekOdemeSekli, girdi.YemekGunlukTutar, girdi.GunSayisi, yil, ay);
            var yol = Kalem(YanHakKalemi.Yol, girdi.YolOdemeSekli, girdi.YolGunlukTutar, girdi.GunSayisi, yil, ay);

            return new YanHakSonucu(
                Yemek: yemek.Tutar,
                Yol: yol.Tutar,
                SgkyaTabi: Round2(yemek.SgkyaTabi + yol.SgkyaTabi),
                GvyeTabi: Round2(yemek.GvyeTabi + yol.GvyeTabi),
                Nakit: Round2(NakitIse(girdi.YemekOdemeSekli, yemek.Tutar) + NakitIse(girdi.YolOdemeSekli, yol.Tutar)),
                Kart: Round2(KartIse(girdi.YemekOdemeSekli, yemek.Tutar) + KartIse(girdi.YolOdemeSekli, yol.Tutar)));
        }

        private static (decimal Tutar, decimal SgkyaTabi, decimal GvyeTabi) Kalem(
            YanHakKalemi kalem, PayrollYanHakOdemeSekli odeme, decimal gunluk, int gun, int yil, int ay)
        {
            // Tutar 0 ise kalem hesaba girmez.
            if (gunluk <= 0 || gun <= 0) return (0m, 0m, 0m);

            var istisna = PayrollYanHakIstisnalari.Bul(kalem, odeme, yil, ay)
                ?? throw new PayrollGirdiHatasi($"{yil}/{ay:00} için {kalem} istisna parametresi tanımlı değil.");

            var tutar = Round2(gun * gunluk);
            return (
                tutar,
                Round2(Math.Max(0, tutar - Round2(gun * istisna.SgkGunluk))),
                Round2(Math.Max(0, tutar - Round2(gun * istisna.GvDvGunluk))));
        }

        private static decimal NakitIse(PayrollYanHakOdemeSekli odeme, decimal tutar) =>
            odeme == PayrollYanHakOdemeSekli.Nakit ? tutar : 0m;

        private static decimal KartIse(PayrollYanHakOdemeSekli odeme, decimal tutar) =>
            odeme == PayrollYanHakOdemeSekli.Kart ? tutar : 0m;

        private static decimal Round2(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
