using CatalogService.Api.Features.Payroll.Commands.CalculatePayroll;
using CatalogService.Api.Features.Payroll.Configuration;
using CatalogService.Api.Features.Payroll.Dtos.Requests;
using CatalogService.Api.Features.Payroll.Dtos.Responses;
using CatalogService.Api.Features.Payroll.Enums;
using CatalogService.Api.Features.Payroll.Queries.GetPayrollLawTypes;
using CatalogService.Api.Features.Payroll.Services;
using CatalogService.Api.Features.Payroll.Services.Models;
using ClosedXML.Excel;

namespace CatalogService.UnitTests.Payroll
{
    /// <summary>
    /// 2026 teşvik listesi ve kural tablosu (PayrollTesvikKurallari). 2026 rakamları:
    /// asgari ücret 33.030; işveren SGK = MYÖ %12 + GSS %7,5 + KVSK %2,25 = %21,75; İSP %2; sigortalı SGK %14.
    /// </summary>
    public class PayrollTesvikTests
    {
        private const decimal AsgariUcret2026 = 33030m;
        private const decimal IsverenSgk2026 = 0.2175m;

        private static CalculatePayrollResponse Hesapla(
            string kod, decimal brut, Action<CalculatePayrollCommand>? ayar = null, int yil = 2026, int ay = 1)
        {
            var komut = new CalculatePayrollCommand
            {
                Year = yil,
                CalculationType = PayrollCalculationType.GrossToNet,
                EmployeeType = PayrollEmployeeType.Normal,
                IncludeEmployerCost = true,
                LawCode = kod,
                Months = new() { new PayrollMonthInputDto { Month = ay, Amount = brut } }
            };
            ayar?.Invoke(komut);
            return new CalculatePayrollCommandHandler(new PayrollCalculationEngine()).Handle(komut, default).Result;
        }

        private static PayrollMonthResultDto Ay(CalculatePayrollResponse r, int ay = 1) => r.Months.Single(x => x.Month == ay);

        private static decimal R2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

        // ---------------- Liste ----------------

        private static readonly (string Anahtar, string Ad)[] Beklenen2026 =
        {
            ("00000", "İndirimsiz / Teşviksiz"),
            ("05510", "5510 - Genel prim indirimi"),
            ("04691", "Teknopark / 4691"),
            ("05746", "Ar-Ge / Tasarım - 5746"),
            ("05746-TEKMER", "TEKMER / Teknoloji Merkezi"),
            ("14857", "Engelli istihdam teşviki"),
            ("06111", "Genç / Kadın / Mesleki Belge"),
            ("15921", "İşsizlik ödeneği alan istihdamı"),
            ("YATIRIM", "Yatırım teşviki istihdam"),
            ("03294", "Sosyal yardım alan istihdamı"),
            ("02828", "Sosyal hizmet modelleri istihdamı"),
            ("05225", "Kültür yatırımı/girişimi"),
            ("TURIZM", "Turizm ücret desteği"),
            ("ISG", "İSG desteği"),
            ("MANUEL", "Özel / Manuel teşvik"),
        };

        private static readonly string[] Silinenler =
            { "06486", "46486", "56486", "66486", "06645", "15510", "25510", "15746", "25225", "55225", "16322", "26322" };

        [Fact]
        public async Task Liste_2026_tam_15_secenek_bu_sirada_bu_adlarla()
        {
            var liste = await new GetPayrollLawTypesQueryHandler().Handle(new GetPayrollLawTypesQuery { Year = 2026 }, default);

            Assert.Equal(Beklenen2026.Select(x => x.Anahtar), liste.Select(x => x.Code));
            Assert.Equal(Beklenen2026.Select(x => x.Ad), liste.Select(x => x.Name));
            Assert.DoesNotContain(liste, x => Silinenler.Contains(x.Code));
            Assert.Equal("05746", liste.Single(x => x.Code == "05746-TEKMER").SgkKodu);
            Assert.Equal("00000 – İndirimsiz / Teşviksiz", liste[0].Label);
            Assert.Equal("Turizm ücret desteği", liste.Single(x => x.Code == "TURIZM").Label);
        }

        [Theory]
        [InlineData(2024)]
        [InlineData(2025)]
        public async Task Gecmis_yillarda_silinenler_ve_2026_yenileri_listede_yok_ek_girdi_sorulmuyor(int yil)
        {
            var liste = await new GetPayrollLawTypesQueryHandler().Handle(new GetPayrollLawTypesQuery { Year = yil }, default);

            Assert.Equal(new[] { "00000", "05510", "04691", "05746", "14857", "06111", "YATIRIM", "02828" },
                liste.Select(x => x.Code));
            Assert.DoesNotContain(liste, x => Silinenler.Contains(x.Code));
            Assert.All(liste, x =>
            {
                Assert.Empty(x.AltSecimler);
                Assert.False(x.EgitimDurumuSorulur);
                Assert.False(x.EngelZorunlu);
            });
        }

        [Theory]
        [InlineData("06486", 2026)]
        [InlineData("15746", 2026)]
        [InlineData("25225", 2026)]
        [InlineData("16322", 2026)]
        [InlineData("06645", 2025)]
        [InlineData("05746-TEKMER", 2025)]
        [InlineData("TURIZM", 2025)]
        public void Silinen_ya_da_o_yil_tanimsiz_kod_reddediliyor(string kod, int yil)
        {
            var hata = Assert.Throws<PayrollGirdiHatasi>(() => Hesapla(kod, 50000m, yil: yil));
        }

        // ---------------- 05510 (mevcut kural) ----------------

        [Theory]
        [InlineData(false, 50000, 1000)]
        [InlineData(true, 50000, 2500)]
        [InlineData(false, 20000, 400)]
        [InlineData(true, 20000, 1000)]
        public void Genel_prim_indirimi_imalat_butonu(bool imalat, decimal brut, decimal beklenen)
        {
            var r = Hesapla("05510", brut, k => k.IsManufacturingSector = imalat);
            Assert.Equal(beklenen, Ay(r).SgkEmployerIncentive);
        }

        // ---------------- Asgari ücret tabanlı tam işveren hissesi ----------------

        [Theory]
        [InlineData("06111")]
        [InlineData("14857")]
        [InlineData("03294")]
        public void Asgari_ucret_tabaninda_isveren_hissesinin_tamami(string kod)
        {
            void Engel(CalculatePayrollCommand k) => k.DisabilityType = PayrollDisabilityType.FirstDegree;

            var ust = Ay(Hesapla(kod, 50000m, Engel));
            Assert.Equal(R2(AsgariUcret2026 * IsverenSgk2026), ust.SgkEmployerIncentive); // 7.184,03
            Assert.Equal(R2(50000m * IsverenSgk2026) - 7184.03m, ust.SgkEmployerNet);
            Assert.Equal(1000m, ust.UnemploymentEmployerAmount); // İSP kapsam dışı

            var alt = Ay(Hesapla(kod, 20000m, Engel));
            Assert.Equal(R2(20000m * IsverenSgk2026), alt.SgkEmployerIncentive); // 4.350
            Assert.Equal(0m, alt.SgkEmployerNet);
            Assert.True(alt.SgkEmployerIncentive <= alt.GrossSalary);
        }

        // ---------------- İşveren + sigortalı hissesi (15921, 02828) ----------------
        // brüt, işveren SGK teşviki, işveren İSP teşviki, sigortalı teşviki (SGK + İSP)

        [Theory]
        // 15921: işveren MYÖ 12 + GSS 7,5 + KVSK 1 puan = %20,5; sigortalı MYÖ 9 + GSS 5 = %14; toplam %34,5.
        [InlineData("15921", 50000, 6771.15, 0, 4624.20)]   // 33.030 × %34,5 = 11.395,35
        [InlineData("15921", 20000, 4100, 0, 2800)]         // 20.000 × %34,5 = 6.900
        // 02828: işveren SGK %21,75 + İSP %2; sigortalı SGK %14 + İSP %1; toplam %38,75.
        [InlineData("02828", 50000, 7184.03, 660.60, 4954.50)] // 33.030 × %38,75 = 12.799,13
        [InlineData("02828", 20000, 4350, 400, 3000)]           // 20.000 × %38,75 = 7.750
        public void Isveren_ve_sigortali_hissesi_asgari_ucret_tabaninda_net_artar(
            string kod, decimal brut, decimal isverenSgk, decimal isverenIsp, decimal sigortali)
        {
            var duz = Ay(Hesapla("00000", brut));
            var t = Ay(Hesapla(kod, brut));

            Assert.Equal(isverenSgk, t.SgkEmployerIncentive);
            Assert.Equal(R2(duz.UnemploymentEmployerAmount!.Value - isverenIsp), t.UnemploymentEmployerAmount);
            Assert.Equal(sigortali, t.EmployeeSgkIncentive);
            Assert.Equal(duz.NetSalary + sigortali, t.NetSalary);
            Assert.Equal(duz.IncomeTaxBase, t.IncomeTaxBase); // 26322 ile aynı yol: GV matrahı değişmez
            Assert.Equal(R2(duz.TotalEmployerCost!.Value - isverenSgk - isverenIsp), t.TotalEmployerCost);

            var geri = Ay(Hesapla(kod, t.NetSalary, k => k.CalculationType = PayrollCalculationType.NetToGross));
            Assert.Equal(t.NetSalary, geri.NetSalary);
            Assert.InRange(geri.GrossSalary, brut - 0.02m, brut); // en küçük brüt kuralı (kuruş yuvarlaması)
            Assert.Equal(sigortali, geri.EmployeeSgkIncentive);
        }

        [Fact]
        public void Issizlik_odenegi_alan_tam_ay_toplami_ve_issizlik_primi_kapsam_disi()
        {
            var t = Ay(Hesapla("15921", 50000m));
            Assert.Equal(11395.35m, t.SgkEmployerIncentive + t.EmployeeSgkIncentive);
            Assert.Equal(1000m, t.UnemploymentEmployerAmount); // işveren İSP kapsam dışı
            Assert.Equal(500m, t.UnemploymentEmployeeAmount); // sigortalı İSP kesilir, karşılanmaz
        }

        [Fact]
        public void Sosyal_hizmet_tam_ay_toplami()
        {
            var t = Ay(Hesapla("02828", 50000m));
            Assert.Equal(12799.13m, t.SgkEmployerIncentive + R2(1000m - t.UnemploymentEmployerAmount!.Value) + t.EmployeeSgkIncentive);
        }

        [Fact]
        public void Engelli_tesviki_engel_durumu_olmadan_reddediliyor()
        {
            var hata = Assert.Throws<PayrollGirdiHatasi>(() => Hesapla("14857", 50000m));
            Assert.Contains("Engel Durumu", hata.Message);
        }

        // ---------------- %50 işveren + GV/DV ----------------

        [Theory]
        [InlineData("04691")]
        [InlineData("05746")]
        [InlineData("05746-TEKMER")]
        public void Yuzde_elli_isveren_hissesi(string kod)
        {
            Assert.Equal(5437.50m, Ay(Hesapla(kod, 50000m)).SgkEmployerIncentive);
            Assert.Equal(2175m, Ay(Hesapla(kod, 20000m)).SgkEmployerIncentive);
        }

        [Theory]
        [InlineData("05746", PayrollArGeEgitimDurumu.Diger80, 0.80)]
        [InlineData("05746", PayrollArGeEgitimDurumu.YuksekLisans90, 0.90)]
        [InlineData("05746", PayrollArGeEgitimDurumu.Doktora95, 0.95)]
        [InlineData("05746-TEKMER", PayrollArGeEgitimDurumu.Diger80, 0.80)]
        [InlineData("05746-TEKMER", PayrollArGeEgitimDurumu.YuksekLisans90, 0.90)]
        [InlineData("05746-TEKMER", PayrollArGeEgitimDurumu.Doktora95, 0.95)]
        public void ArGe_ve_TEKMER_GV_egitime_gore_DV_muaf(string kod, PayrollArGeEgitimDurumu egitim, decimal oran)
        {
            var duz = Ay(Hesapla("00000", 80000m));
            var t = Ay(Hesapla(kod, 80000m, k => k.ArGeEgitimDurumu = egitim));

            var kalan = duz.CalculatedIncomeTax - duz.IncomeTaxExemption; // AÜ istisnası sonrası
            Assert.True(kalan > 0);
            Assert.Equal(R2(kalan - R2(kalan * oran)), t.PayableIncomeTax);
            Assert.Equal(0m, t.PayableStampTax);
            Assert.True(duz.PayableStampTax > 0);
        }

        // ---------------- Yatırım teşviki ----------------

        [Fact]
        public void Yatirim_alt_secimsiz_reddediliyor()
        {
            var hata = Assert.Throws<PayrollGirdiHatasi>(() => Hesapla("YATIRIM", 50000m));
        }

        [Fact]
        public void Yatirim_1_5_bolge_isveren_hissesi_net_degismez()
        {
            var duz = Ay(Hesapla("00000", 50000m));
            var y = Ay(Hesapla("YATIRIM", 50000m, k => k.TesvikAltSecimi = 1));

            Assert.Equal(7184.03m, y.SgkEmployerIncentive);
            Assert.Equal(0m, y.EmployeeSgkIncentive);
            Assert.Equal(duz.NetSalary, y.NetSalary);
        }

        [Fact]
        public void Yatirim_6_bolge_sigortali_hissesi_neti_artirir_ve_netten_brute_gidis_donus_tutar()
        {
            var duz = Ay(Hesapla("00000", 50000m));
            var y = Ay(Hesapla("YATIRIM", 50000m, k => k.TesvikAltSecimi = 2));

            Assert.Equal(7184.03m, y.SgkEmployerIncentive);
            Assert.Equal(R2(AsgariUcret2026 * 0.14m), y.EmployeeSgkIncentive); // 4.624,20
            Assert.Equal(duz.NetSalary + 4624.20m, y.NetSalary);
            Assert.Equal(duz.IncomeTaxBase, y.IncomeTaxBase); // TODO-TEYIT: GV matrahı değişmiyor

            var geri = Ay(Hesapla("YATIRIM", y.NetSalary, k =>
            {
                k.TesvikAltSecimi = 2;
                k.CalculationType = PayrollCalculationType.NetToGross;
            }));
            Assert.Equal(y.NetSalary, geri.NetSalary);
            Assert.InRange(geri.GrossSalary, 49999.98m, 50000m); // en küçük brüt kuralı (kuruş yuvarlaması)
            Assert.Equal(y.EmployeeSgkIncentive, geri.EmployeeSgkIncentive);

            // Asgari ücret altı: sigortalı hissesinin tamamı.
            var alt = Ay(Hesapla("YATIRIM", 20000m, k => k.TesvikAltSecimi = 2));
            Assert.Equal(alt.SgkEmployeeAmount, alt.EmployeeSgkIncentive);
        }

        // ---------------- Sosyal hizmet / Kültür ----------------

        [Theory]
        [InlineData(1, 50000, 5437.50)]
        [InlineData(2, 50000, 2718.75)]
        [InlineData(1, 20000, 2175)]
        [InlineData(2, 20000, 1087.50)]
        public void Kultur_yatirim_yuzde_elli_isletme_yuzde_yirmi_bes(int donem, decimal brut, decimal beklenen)
        {
            Assert.Equal(beklenen, Ay(Hesapla("05225", brut, k => k.TesvikAltSecimi = donem)).SgkEmployerIncentive);
        }

        // ---------------- Turizm / İSG ----------------

        [Fact]
        public void Turizm_nisanda_sifir_mayista_30_gun_SGK_primine_dokunmaz()
        {
            var r = Hesapla("TURIZM", 50000m, k => k.TurizmKonaklamaOnayi = true, ay: 4);

            Assert.Equal(0m, Ay(r, 4).EmployerSupportAmount);
            var mayis = Ay(r, 5);
            Assert.Equal(R2(30 * 116.67m), mayis.EmployerSupportAmount); // 3.500,10
            Assert.Equal(0m, mayis.SgkEmployerIncentive);
            Assert.Equal(10875m, mayis.SgkEmployerNet);
            Assert.Equal(50000m + 10875m + 1000m - 3500.10m, mayis.TotalEmployerCost);
            Assert.Equal(R2(8 * 3500.10m), r.Totals!.TotalEmployerSupportAmount);

            var gun = Hesapla("TURIZM", 50000m, k => { k.TurizmKonaklamaOnayi = true; k.TurizmPrimGunu = 12; }, ay: 5);
            Assert.Equal(R2(12 * 116.67m), Ay(gun, 5).EmployerSupportAmount);
        }

        [Fact]
        public void Turizm_onaysiz_reddediliyor()
        {
            var hata = Assert.Throws<PayrollGirdiHatasi>(() => Hesapla("TURIZM", 50000m, ay: 5));
            Assert.Contains("NACE 55", hata.Message);
        }

        [Fact]
        public void ISG_aylik_tutar_ayri_satirda_duser()
        {
            var a = Ay(Hesapla("ISG", 50000m, k => k.IsgDestegiTutari = 1250m));
            Assert.Equal(1250m, a.EmployerSupportAmount);
            Assert.Equal(0m, a.SgkEmployerIncentive);
            Assert.Equal(50000m + 10875m + 1000m - 1250m, a.TotalEmployerCost);
        }

        // ---------------- Manuel ----------------

        [Theory]
        [InlineData(PayrollManuelTesvikTuru.Oran, 5, 50000, 2500)]
        [InlineData(PayrollManuelTesvikTuru.Tutar, 3000, 50000, 3000)]
        [InlineData(PayrollManuelTesvikTuru.Tutar, 20000, 50000, 10875)] // işveren SGK'sını aşamaz
        [InlineData(PayrollManuelTesvikTuru.Oran, 100, 20000, 4350)]     // oran %100 → işveren SGK'sı ile sınırlı
        public void Manuel_oran_ve_tutar_isveren_SGKsini_asmaz(PayrollManuelTesvikTuru tur, decimal deger, decimal brut, decimal beklenen)
        {
            var a = Ay(Hesapla("MANUEL", brut, k => { k.ManuelTesvikTuru = tur; k.ManuelTesvikDegeri = deger; }));
            Assert.Equal(beklenen, a.SgkEmployerIncentive);
            Assert.True(a.SgkEmployerNet >= 0);
        }

        [Fact]
        public void Manuel_tursuz_reddediliyor()
        {
            var hata = Assert.Throws<PayrollGirdiHatasi>(() => Hesapla("MANUEL", 50000m, k => k.ManuelTesvikDegeri = 5));
        }

        // ---------------- Emekli / huzur hakkı ----------------

        [Theory]
        [InlineData("05510")]
        [InlineData("14857")]
        [InlineData("06111")]
        [InlineData("15921")]
        [InlineData("YATIRIM")]
        [InlineData("03294")]
        [InlineData("02828")]
        public void Emekliye_uygulanmayan_tesvik_teşviksiz_hesaplanir_ve_uyari_verir(string kod)
        {
            var r = Hesapla(kod, 50000m, k =>
            {
                k.EmployeeType = PayrollEmployeeType.Retired;
                k.DisabilityType = PayrollDisabilityType.FirstDegree;
                k.TesvikAltSecimi = 2;
            });
            var duz = Hesapla("00000", 50000m, k =>
            {
                k.EmployeeType = PayrollEmployeeType.Retired;
                k.DisabilityType = PayrollDisabilityType.FirstDegree;
            });

            Assert.Equal(0m, Ay(r).SgkEmployerIncentive);
            Assert.Equal(0m, Ay(r).EmployeeSgkIncentive);
            Assert.Equal(Ay(duz).NetSalary, Ay(r).NetSalary);
            Assert.Equal(Ay(duz).TotalEmployerCost, Ay(r).TotalEmployerCost);
            Assert.Contains(r.Warnings, w => w.Contains("Emekli"));
        }

        [Fact]
        public void Emekliye_Teknopark_uygulanir_uyari_yok()
        {
            var r = Hesapla("04691", 50000m, k => k.EmployeeType = PayrollEmployeeType.Retired);
            Assert.Equal(5437.50m, Ay(r).SgkEmployerIncentive); // TODO-TEYIT: normal oranın yarısı (SGDP'nin yarısı değil); teyide kadar bilinçli olarak böyle
            Assert.Empty(r.Warnings);
        }

        [Fact]
        public void Huzur_hakkinda_tesvik_uygulanmaz()
        {
            void Huzur(CalculatePayrollCommand k) => k.EmployeeType = PayrollEmployeeType.Honorarium;

            var duz = Hesapla("00000", 50000m, Huzur);
            var t = Hesapla("04691", 50000m, Huzur);

            Assert.Equal(duz.HonorariumTotals!.TotalNetHonorarium, t.HonorariumTotals!.TotalNetHonorarium);
            Assert.Equal(duz.HonorariumTotals.TotalPayableStampTax, t.HonorariumTotals.TotalPayableStampTax);
            Assert.Contains(t.Warnings, w => w.Contains("Huzur hakkında"));
            // Ek girdi istenmez: alt seçimsiz Yatırım reddedilmez.
            Hesapla("YATIRIM", 50000m, Huzur);
        }

        // ---------------- Mevcut kurallar 2026'da birebir korunuyor ----------------

        [Theory]
        [InlineData("00000")]
        [InlineData("04691")]
        [InlineData("05510")]
        [InlineData("05746")]
        public void Mevcut_kural_tasinan_tesvikler_eski_stratejiyle_ayni_sonuc(string kod)
        {
            foreach (var imalat in new[] { false, true })
            foreach (var brut in new[] { 10000m, 33030m, 50000m, 400000m })
            {
                var yeni = Hesapla(kod, brut, k => k.IsManufacturingSector = imalat);

                var eski = Hesapla("00000", brut, k => k.IsManufacturingSector = imalat);
                var ctx = new PayrollCalculationContext
                {
                    Parameter = PayrollYearConfigStore.Get(2026).Parameter,
                    IsManufacturingSector = imalat
                };
                PayrollIncentiveStrategyFactory.Create(kod).EnrichEmployerCosts(eski, ctx);

                for (var ay = 1; ay <= 12; ay++)
                {
                    Assert.Equal(Ay(eski, ay).SgkEmployerIncentive, Ay(yeni, ay).SgkEmployerIncentive);
                    Assert.Equal(Ay(eski, ay).SgkEmployerNet, Ay(yeni, ay).SgkEmployerNet);
                    Assert.Equal(Ay(eski, ay).UnemploymentEmployerAmount, Ay(yeni, ay).UnemploymentEmployerAmount);
                    Assert.Equal(Ay(eski, ay).TotalEmployerCost, Ay(yeni, ay).TotalEmployerCost);
                }
                Assert.Equal(Ay(eski).IncentiveSource, Ay(yeni).IncentiveSource);
            }
        }

        // ---------------- Emekli (SGDP) işveren primi ----------------

        private static void Emekli(CalculatePayrollCommand k) => k.EmployeeType = PayrollEmployeeType.Retired;

        [Theory]
        // SGDP %22,5 + KVSK %2,25 = %24,75; GSS ve işsizlik yok. 400.000 → taban 33.030 × 9 = 297.270.
        [InlineData(50000, 12375, 3750)]
        [InlineData(400000, 73574.33, 22295.25)]
        public void Emekli_isveren_SGDP_ve_KVSK_issizlik_yok(decimal brut, decimal isverenSgk, decimal sigortaliSgk)
        {
            var a = Ay(Hesapla("00000", brut, Emekli));

            Assert.Equal(isverenSgk, a.SgkEmployerGross);
            Assert.Equal(isverenSgk, a.SgkEmployerNet);
            Assert.Equal(0m, a.UnemploymentEmployerAmount);
            Assert.Equal(0m, a.UnemploymentEmployeeAmount);
            Assert.Equal(sigortaliSgk, a.SgkEmployeeAmount); // sigortalı %7,5 (değişmedi)
            Assert.Equal(brut + isverenSgk, a.TotalEmployerCost);

            var geri = Ay(Hesapla("00000", a.NetSalary, k =>
            {
                Emekli(k);
                k.CalculationType = PayrollCalculationType.NetToGross;
            }));
            Assert.Equal(a.NetSalary, geri.NetSalary);
            Assert.InRange(geri.GrossSalary, brut - 0.02m, brut);
            Assert.Equal(R2(PayrollTesvikTabani.SgkMatrahi(geri.GrossSalary, PayrollYearConfigStore.Get(2026).Parameter) * 0.2475m),
                geri.SgkEmployerGross);
        }

        [Fact]
        public void Emekli_2025_isveren_tarafi_dondurulmus_normal_oranlar()
        {
            var a = Ay(Hesapla("00000", 50000m, Emekli, yil: 2025));
            Assert.Equal(R2(50000m * 0.205m), a.SgkEmployerGross); // 2025: MYÖ 11 + GSS 7,5 + KVSK 2
            Assert.Equal(1000m, a.UnemploymentEmployerAmount);
        }

        [Fact]
        public void Emekliye_Teknopark_tesvik_tutari_degismez_SGDP_brutunden_duser()
        {
            var a = Ay(Hesapla("04691", 50000m, Emekli));
            Assert.Equal(5437.50m, a.SgkEmployerIncentive); // TODO-TEYIT: normal oranın yarısı (SGDP'nin yarısı değil); teyide kadar bilinçli olarak böyle
            Assert.Equal(12375m - 5437.50m, a.SgkEmployerNet);
            Assert.Equal(0m, a.UnemploymentEmployerAmount);
        }

        // ---------------- 4691/5746 GV-DV teşvik üst sınırı (asgari ücret × 40) ----------------

        private const decimal UcretTavani2026 = 33030m * 40; // 1.321.200

        [Theory]
        [InlineData("04691", 1.00)]
        [InlineData("05746", 0.80)]
        [InlineData("05746-TEKMER", 0.80)]
        public void GV_DV_tesviki_yalniz_tavana_kadar_asan_kisim_normal(string kod, decimal oran)
        {
            const decimal brut = 1500000m;
            var duz = Ay(Hesapla("00000", brut));
            var tavan = Ay(Hesapla("00000", UcretTavani2026)); // aynı SGK kesintisi (SGK tavanı aşılıyor)
            var t = Ay(Hesapla(kod, brut));

            Assert.Equal(duz.SgkEmployeeAmount, tavan.SgkEmployeeAmount);
            var auIstisnasi = duz.IncomeTaxExemption;
            var tesvik = R2(R2(tavan.CalculatedIncomeTax - auIstisnasi) * oran);

            Assert.Equal(duz.CalculatedIncomeTax, t.CalculatedIncomeTax);
            Assert.Equal(R2(auIstisnasi + tesvik), t.IncomeTaxExemption);
            Assert.Equal(R2(duz.CalculatedIncomeTax - auIstisnasi - tesvik), t.PayableIncomeTax);
            if (oran == 1m) // aşan 178.800'ün GV'si normal
                Assert.Equal(R2(duz.CalculatedIncomeTax - tavan.CalculatedIncomeTax), t.PayableIncomeTax);

            // DV: yalnız aşan 178.800 × %0,759 = 1.357,09 ödenir.
            Assert.Equal(1357.09m, t.PayableStampTax);
            Assert.Equal(R2(R2(brut * 0.00759m) - R2(UcretTavani2026 * 0.00759m)), t.PayableStampTax);

            var geri = Ay(Hesapla(kod, t.NetSalary, k => k.CalculationType = PayrollCalculationType.NetToGross));
            Assert.Equal(t.NetSalary, geri.NetSalary);
            Assert.InRange(geri.GrossSalary, brut - 0.02m, brut);
        }

        [Theory]
        [InlineData("04691", 1.00)]
        [InlineData("05746", 0.80)]
        [InlineData("05746-TEKMER", 0.80)]
        public void GV_DV_tesviki_tavan_altinda_degismez(string kod, decimal oran)
        {
            var duz = Ay(Hesapla("00000", 100000m));
            var t = Ay(Hesapla(kod, 100000m));

            var kalan = R2(duz.CalculatedIncomeTax - duz.IncomeTaxExemption);
            Assert.Equal(R2(kalan - R2(kalan * oran)), t.PayableIncomeTax);
            Assert.Equal(0m, t.PayableStampTax);
        }

        [Fact]
        public void GV_DV_tavani_2025te_yok()
        {
            var t = Ay(Hesapla("04691", 1500000m, yil: 2025));
            Assert.Equal(0m, t.PayableIncomeTax);
            Assert.Equal(0m, t.PayableStampTax);
        }

        // ---------------- Çıktı ----------------

        [Theory]
        [InlineData("YATIRIM", 2, "26322 – Yatırım teşviki istihdam (6. bölge)")]
        [InlineData("YATIRIM", 1, "16322 – Yatırım teşviki istihdam (1–5. bölge)")]
        [InlineData("05225", 2, "05225 – Kültür yatırımı/girişimi (İşletme dönemi %25)")]
        [InlineData("05746-TEKMER", null, "05746 – TEKMER / Teknoloji Merkezi")]
        [InlineData("TURIZM", null, "Turizm ücret desteği")]
        public void Etiket_kod_ve_ad(string kod, int? alt, string beklenen)
            => Assert.Equal(beklenen, PayrollTesvikKurallari.Etiket(kod, alt));

        [Fact]
        public void Excel_basliginda_ham_kod_yerine_kod_ve_ad_ve_destek_kolonu()
        {
            var istek = new CalculatePayrollRequest
            {
                Year = 2026,
                CalculationType = PayrollCalculationType.GrossToNet,
                EmployeeType = PayrollEmployeeType.Normal,
                IncludeEmployerCost = true,
                LawCode = "TURIZM",
                TurizmKonaklamaOnayi = true,
                Months = new() { new PayrollMonthInputDto { Month = 5, Amount = 50000m } }
            };
            var sonuc = Hesapla("TURIZM", 50000m, k => k.TurizmKonaklamaOnayi = true, ay: 5);

            var bytes = new PayrollCalculationExportService().ExportToExcel(sonuc, istek);
            using var wb = new XLWorkbook(new MemoryStream(bytes));
            var ws = wb.Worksheet(1);

            Assert.Equal("Kanun: Turizm ücret desteği", ws.Cell(2, 1).GetString());
            var basliklar = ws.Row(4).CellsUsed().Select(c => c.GetString()).ToList();
            Assert.Contains("Turizm Desteği", basliklar);
            Assert.DoesNotContain(basliklar, b => b.StartsWith("SGK Teşvik"));
        }
    }
}
