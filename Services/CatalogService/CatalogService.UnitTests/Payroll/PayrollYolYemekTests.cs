using CatalogService.Api.Features.Payroll.Commands.CalculatePayroll;
using CatalogService.Api.Features.Payroll.Dtos.Requests;
using CatalogService.Api.Features.Payroll.Dtos.Responses;
using CatalogService.Api.Features.Payroll.Enums;
using CatalogService.Api.Features.Payroll.Services;
using ClosedXML.Excel;

namespace CatalogService.UnitTests.Payroll
{
    /// <summary>
    /// Yol / yemek yan hakkı (2026+). Örnekler: normal çalışan, brüt 50.000, 22 gün.
    /// İstisnalar: PayrollYanHakIstisnalari (yemek GV/DV kart 330 / nakit 300; yemek SGK 2026 tüm yıl
    /// kart ve nakit 300; yol kart 158, nakit 0).
    /// </summary>
    public class PayrollYolYemekTests
    {
        private const decimal Brut = 50000m;
        private const PayrollYanHakOdemeSekli Nakit = PayrollYanHakOdemeSekli.Nakit;
        private const PayrollYanHakOdemeSekli Kart = PayrollYanHakOdemeSekli.Kart;

        private static PayrollYolYemekDto Yemek(decimal gunluk, PayrollYanHakOdemeSekli odeme) =>
            new() { GunSayisi = 22, YemekGunlukTutar = gunluk, YemekOdemeSekli = odeme };

        private static PayrollYolYemekDto Yol(decimal gunluk, PayrollYanHakOdemeSekli odeme) =>
            new() { GunSayisi = 22, YolGunlukTutar = gunluk, YolOdemeSekli = odeme };

        private static CalculatePayrollCommand Komut(
            PayrollYolYemekDto? yolYemek, int ay = 6, decimal tutar = Brut,
            PayrollCalculationType tip = PayrollCalculationType.GrossToNet,
            PayrollEmployeeType calisan = PayrollEmployeeType.Normal, int yil = 2026, string kanun = "00000") =>
            new()
            {
                Year = yil,
                CalculationType = tip,
                EmployeeType = calisan,
                IncludeEmployerCost = true,
                LawCode = kanun,
                StartMonth = ay,
                Months = new() { new PayrollMonthInputDto { Month = ay, Amount = tutar } },
                YolYemek = yolYemek
            };

        private static CalculatePayrollResponse Hesapla(CalculatePayrollCommand komut) =>
            new CalculatePayrollCommandHandler(new PayrollCalculationEngine()).Handle(komut, default).Result;

        private static PayrollMonthResultDto Ay(CalculatePayrollResponse r, int ay = 6) => r.Months.Single(x => x.Month == ay);

        private static decimal R2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

        // ---------------- İstenen örnekler ----------------

        [Fact]
        public void Haziran_yemek_kart_300_tamami_istisna_net_degismez_kartla_odenen_6600()
        {
            var duz = Ay(Hesapla(Komut(null)));
            var a = Ay(Hesapla(Komut(Yemek(300m, Kart))));

            Assert.Equal(6600m, a.MealAmount);
            Assert.Equal(0m, a.IncomeTaxableBenefits); // 22 × 330 = 7.260 istisna
            Assert.Equal(0m, a.SgkTaxableBenefits);    // 22 × 300 = 6.600 istisna
            Assert.Equal(6600m, a.CardPaidBenefits);
            Assert.Equal(0m, a.CashBenefits);
            Assert.Equal(duz.NetSalary, a.NetSalary);
            Assert.Equal(duz.TotalDeductions, a.TotalDeductions);
            Assert.Equal(R2(duz.TotalEmployerCost!.Value + 6600m), a.TotalEmployerCost);
        }

        [Fact]
        public void Haziran_yemek_nakit_400_istisna_asan_2200_SGK_ve_GV_matrahina_girer()
        {
            var duz = Ay(Hesapla(Komut(null)));
            var a = Ay(Hesapla(Komut(Yemek(400m, Nakit))));

            Assert.Equal(8800m, a.MealAmount);
            Assert.Equal(2200m, a.SgkTaxableBenefits); // 8.800 − 22 × 300
            Assert.Equal(2200m, a.IncomeTaxableBenefits); // 8.800 − 22 × 300
            Assert.Equal(8800m, a.CashBenefits);
            Assert.Equal(0m, a.CardPaidBenefits);

            // SGK/İSP işçi artan matrahtan; GV matrahı = ücret + GV'ye tabi − işçi kesintileri (çift sayma yok).
            Assert.Equal(R2(52200m * 0.14m), a.SgkEmployeeAmount);
            Assert.Equal(R2(52200m * 0.01m), a.UnemploymentEmployeeAmount);
            Assert.Equal(R2(52200m - a.SgkEmployeeAmount - a.UnemploymentEmployeeAmount), a.IncomeTaxBase);
            Assert.Equal(R2(52200m * 0.00759m), a.CalculatedStampTax);

            // net = eski net + 8.800 − ek kesintiler
            Assert.Equal(R2(duz.NetSalary + 8800m - (a.TotalDeductions - duz.TotalDeductions)), a.NetSalary);
            Assert.True(a.TotalDeductions > duz.TotalDeductions);

            // İşveren: prim artan matrahtan, maliyete yan hak eklenir.
            Assert.Equal(R2(52200m * 0.2175m), a.SgkEmployerGross);
            Assert.Equal(R2(52200m * 0.02m), a.UnemploymentEmployerAmount);
            Assert.Equal(R2(Brut + 8800m + a.SgkEmployerNet!.Value + a.UnemploymentEmployerAmount!.Value), a.TotalEmployerCost);
        }

        [Fact]
        public void Subat_yemek_kart_300_Haziranla_ayni_tabi_kisim_yok()
        {
            var a = Ay(Hesapla(Komut(Yemek(300m, Kart), ay: 2)), 2);
            Assert.Equal(0m, a.SgkTaxableBenefits); // 22 × 300 = 6.600
            Assert.Equal(0m, a.IncomeTaxableBenefits); // 22 × 330 = 7.260
        }

        [Fact]
        public void Subat_yemek_nakit_300_SGKya_ve_GVye_tabi_yok()
        {
            var duz = Ay(Hesapla(Komut(null, ay: 2)), 2);
            var a = Ay(Hesapla(Komut(Yemek(300m, Nakit), ay: 2)), 2);
            Assert.Equal(0m, a.SgkTaxableBenefits); // 22 × 300 SGK istisnası
            Assert.Equal(0m, a.IncomeTaxableBenefits); // 22 × 300 GV istisnası
            Assert.Equal(duz.SgkEmployeeAmount, a.SgkEmployeeAmount);
            Assert.Equal(R2(duz.NetSalary + 6600m), a.NetSalary);
        }

        [Fact]
        public void Yol_kart_200_tutar_4400_tabi_kisim_924()
        {
            var a = Ay(Hesapla(Komut(Yol(200m, Kart))));
            Assert.Equal(4400m, a.TransportAmount);
            Assert.Equal(924m, a.SgkTaxableBenefits); // 4.400 − 22 × 158
            Assert.Equal(924m, a.IncomeTaxableBenefits);
            Assert.Equal(4400m, a.CardPaidBenefits);
            Assert.Equal(0m, a.CashBenefits);
        }

        [Fact]
        public void Yol_nakit_200_istisna_yok_tamami_tabi()
        {
            var a = Ay(Hesapla(Komut(Yol(200m, Nakit))));
            Assert.Equal(4400m, a.SgkTaxableBenefits);
            Assert.Equal(4400m, a.IncomeTaxableBenefits);
            Assert.Equal(4400m, a.CashBenefits);
        }

        [Fact]
        public void Yemek_istisnalari_2026_tum_aylarda_ayni()
        {
            for (var ay = 1; ay <= 12; ay++)
            {
                var nakit = Ay(Hesapla(Komut(Yemek(400m, Nakit), ay: ay)), ay);
                Assert.Equal(2200m, nakit.SgkTaxableBenefits);     // 8.800 − 22 × 300
                Assert.Equal(2200m, nakit.IncomeTaxableBenefits);  // 8.800 − 22 × 300

                var kart = Ay(Hesapla(Komut(Yemek(350m, Kart), ay: ay)), ay);
                Assert.Equal(1100m, kart.SgkTaxableBenefits);      // 7.700 − 22 × 300
                Assert.Equal(440m, kart.IncomeTaxableBenefits);    // 7.700 − 22 × 330
            }
        }

        [Theory]
        [InlineData(400, PayrollYanHakOdemeSekli.Nakit, 200, PayrollYanHakOdemeSekli.Kart)]
        [InlineData(300, PayrollYanHakOdemeSekli.Kart, 200, PayrollYanHakOdemeSekli.Nakit)]
        [InlineData(500, PayrollYanHakOdemeSekli.Nakit, 0, PayrollYanHakOdemeSekli.Nakit)]
        public void Netten_brute_gidis_donus_yan_haklar_acik(
            decimal yemek, PayrollYanHakOdemeSekli yemekOdeme, decimal yol, PayrollYanHakOdemeSekli yolOdeme)
        {
            var yy = new PayrollYolYemekDto
            {
                GunSayisi = 22,
                YemekGunlukTutar = yemek, YemekOdemeSekli = yemekOdeme,
                YolGunlukTutar = yol, YolOdemeSekli = yolOdeme
            };
            var g2n = Ay(Hesapla(Komut(yy)));
            var ucretNeti = g2n.NetSalary - g2n.CashBenefits; // hedef: yan haklar HARİÇ ücret neti

            var n2g = Ay(Hesapla(Komut(yy, tutar: ucretNeti, tip: PayrollCalculationType.NetToGross)));

            Assert.Equal(Brut, n2g.GrossSalary);
            Assert.Equal(g2n.NetSalary, n2g.NetSalary);
            Assert.Equal(g2n.CashBenefits, n2g.CashBenefits);
            Assert.Equal(g2n.CardPaidBenefits, n2g.CardPaidBenefits);
        }

        // ---------------- Kapalı / sıfır = bugünkü hesap ----------------

        [Theory]
        [InlineData(PayrollCalculationType.GrossToNet, PayrollEmployeeType.Normal, "00000")]
        [InlineData(PayrollCalculationType.NetToGross, PayrollEmployeeType.Normal, "05510")]
        [InlineData(PayrollCalculationType.GrossToNet, PayrollEmployeeType.Retired, "04691")]
        public void Tutarlar_sifirken_sonuc_kapaliyla_ayni(
            PayrollCalculationType tip, PayrollEmployeeType calisan, string kanun)
        {
            var kapali = Ay(Hesapla(Komut(null, tip: tip, calisan: calisan, kanun: kanun)));
            var sifir = Ay(Hesapla(Komut(new PayrollYolYemekDto(), tip: tip, calisan: calisan, kanun: kanun)));

            foreach (var p in typeof(PayrollMonthResultDto).GetProperties())
                Assert.True(Equals(p.GetValue(kapali), p.GetValue(sifir)), $"{p.Name} farklı");
        }

        [Fact]
        public void Kapaliyken_yan_hak_alanlari_sifir_ve_bayrak_kapali()
        {
            var r = Hesapla(Komut(null));
            Assert.False(r.YolYemekDahil);
            var a = Ay(r);
            Assert.Equal(0m, a.MealAmount + a.TransportAmount + a.SgkTaxableBenefits + a.IncomeTaxableBenefits + a.CashBenefits + a.CardPaidBenefits);
        }

        // ---------------- Emekli (SGDP) ----------------

        [Fact]
        public void Emekli_SGDP_matrahina_da_yan_hak_eklenir()
        {
            var a = Ay(Hesapla(Komut(Yemek(400m, Nakit), calisan: PayrollEmployeeType.Retired)));
            Assert.Equal(R2(52200m * 0.075m), a.SgkEmployeeAmount);
            Assert.Equal(R2(52200m * (0.225m + 0.0225m)), a.SgkEmployerGross);
            Assert.Equal(0m, a.UnemploymentEmployerAmount);
        }

        // ---------------- Kapsam dışı ----------------

        [Theory]
        [InlineData(2024)]
        [InlineData(2025)]
        public void Yol_yemek_2026_oncesi_reddedilir(int yil)
        {
            Assert.Throws<PayrollGirdiHatasi>(() => Hesapla(Komut(Yemek(300m, Kart), yil: yil)));
        }

        [Fact]
        public void Huzur_hakkinda_yol_yemek_reddedilir()
        {
            var hata = Assert.Throws<PayrollGirdiHatasi>(
                () => Hesapla(Komut(Yemek(300m, Kart), calisan: PayrollEmployeeType.Honorarium)));
            Assert.Contains("Huzur", hata.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(32)]
        public void Gun_sayisi_1_31_disinda_reddedilir(int gun)
        {
            var yy = Yemek(300m, Kart);
            yy.GunSayisi = gun;
            Assert.Throws<PayrollGirdiHatasi>(() => Hesapla(Komut(yy)));
        }

        // ---------------- Çıktı ----------------

        [Fact]
        public void Excel_yan_hak_sutunlari_ve_girdi_ust_bilgisi()
        {
            var yy = new PayrollYolYemekDto
            {
                GunSayisi = 22,
                YemekGunlukTutar = 400m, YemekOdemeSekli = Nakit,
                YolGunlukTutar = 200m, YolOdemeSekli = Kart
            };
            var istek = new CalculatePayrollRequest
            {
                Year = 2026,
                CalculationType = PayrollCalculationType.GrossToNet,
                EmployeeType = PayrollEmployeeType.Normal,
                IncludeEmployerCost = true,
                Months = new() { new PayrollMonthInputDto { Month = 1, Amount = Brut } },
                YolYemek = yy
            };
            var sonuc = Hesapla(Komut(yy, ay: 1));

            using var wb = new XLWorkbook(new MemoryStream(new PayrollCalculationExportService().ExportToExcel(sonuc, istek)));
            var ws = wb.Worksheet(1);
            var basliklar = ws.Row(4).CellsUsed().Select(c => c.GetString()).ToList();

            Assert.Equal(new[] { "Brüt Ücret", "Yemek Tutarı", "Yol Tutarı", "SGK'ya Tabi Yan Hak", "GV'ye Tabi Yan Hak", "SGK İşçi" },
                basliklar.Skip(2).Take(6));
            Assert.Equal("Kartla Ödenen", basliklar[basliklar.IndexOf("Net Ücret") + 1]);
            Assert.Contains("22 gün", ws.Cell(3, 1).GetString());
            Assert.Contains("Nakit", ws.Cell(3, 1).GetString());
            Assert.Contains("Ulaşım kartı/bilet/akbil", ws.Cell(3, 1).GetString());

            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community; // Program.cs'teki gibi
            var pdf = new PayrollCalculationExportService().ExportToPdf(sonuc, istek);
            Assert.True(pdf.Length > 0);
        }

        [Fact]
        public void Excel_kapaliyken_yan_hak_sutunu_yok()
        {
            var istek = new CalculatePayrollRequest
            {
                Year = 2026,
                IncludeEmployerCost = true,
                Months = new() { new PayrollMonthInputDto { Month = 1, Amount = Brut } }
            };
            var sonuc = Hesapla(Komut(null, ay: 1));
            using var wb = new XLWorkbook(new MemoryStream(new PayrollCalculationExportService().ExportToExcel(sonuc, istek)));
            var ws = wb.Worksheet(1);
            var basliklar = ws.Row(4).CellsUsed().Select(c => c.GetString()).ToList();

            Assert.DoesNotContain("Yemek Tutarı", basliklar);
            Assert.DoesNotContain("Kartla Ödenen", basliklar);
            Assert.True(ws.Cell(3, 1).IsEmpty());
        }
    }
}
