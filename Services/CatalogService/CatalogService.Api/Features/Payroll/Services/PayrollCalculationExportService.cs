using System.Globalization;
using System.Reflection;
using CatalogService.Api.Features.Payroll.Configuration;
using CatalogService.Api.Features.Payroll.Dtos.Requests;
using CatalogService.Api.Features.Payroll.Dtos.Responses;
using CatalogService.Api.Features.Payroll.Enums;
using CatalogService.Api.Features.Payroll.Services.Interfaces;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CatalogService.Api.Features.Payroll.Services
{
    public class PayrollCalculationExportService : IPayrollCalculationExportService
    {
        private static readonly CultureInfo TrCulture = CultureInfo.GetCultureInfo("tr-TR");
        private const string MoneyFormat = "#,##0.00";
        private const string PercentFormat = "0.00\"%\"";

        private static readonly string[] MonthNames =
        {
            "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        };

        private static readonly Lazy<byte[]?> LogoBytes = new(LoadLogoBytes);

        private static byte[]? LoadLogoBytes()
        {
            var asm = typeof(PayrollCalculationExportService).Assembly;
            using var stream = asm.GetManifestResourceStream(PayrollExportFooterTexts.LogoEmbeddedResourceName);
            if (stream is null) return null;

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        // ─────────────────────────────────────────────────────────
        // EXCEL
        // ─────────────────────────────────────────────────────────
        public byte[] ExportToExcel(CalculatePayrollResponse result, CalculatePayrollRequest request)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Bordro");

            bool isHonorarium = result.EmployeeType == PayrollEmployeeType.Honorarium;
            string calcLabel = result.CalculationType == PayrollCalculationType.GrossToNet
                ? "Brütten Nete" : "Netten Brüte";

            // Title
            ws.Cell(1, 1).Value = isHonorarium
                ? $"Huzur Hakkı Hesaplama — {result.Year}"
                : $"Ücret Bordrosu Hesaplama — {result.Year}";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 13;

            ws.Cell(2, 1).Value = $"Kanun: {PayrollTesvikKurallari.Etiket(request.LawCode, request.TesvikAltSecimi)}";
            ws.Cell(2, 3).Value = calcLabel;
            ws.Cell(2, 5).Value = $"Tarih: {DateTime.Now.ToString("dd.MM.yyyy", TrCulture)}";

            const int headerRow = 4;

            // totalRow her iki dalda da headerRow + 13'tür (12 ay + 1 total).
            int totalRow = headerRow + 13;
            int lastCol;
            if (isHonorarium)
                lastCol = BuildHonorariumExcel(ws, result, headerRow);
            else
                lastCol = BuildSalaryExcel(ws, result, request, headerRow);

            // Yol / yemek girdileri: tablo genişliğinde birleşik satır (A sütununu genişletmesin).
            if (result.YolYemekDahil && YolYemekBilgisi(request) is { } yolYemek)
            {
                var bilgi = ws.Range(3, 1, 3, Math.Max(lastCol, 1));
                bilgi.Merge();
                bilgi.Value = yolYemek;
            }

            AppendExcelFooter(ws, footerStartRow: totalRow + 2, lastCol: lastCol);

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            return stream.ToArray();
        }

        /// <summary>Yol / yemek girdileri — Excel ve PDF üst bilgisinde aynı metin.</summary>
        private static string? YolYemekBilgisi(CalculatePayrollRequest request)
        {
            if (request.YolYemek is not { } y) return null;

            static string Kalem(string ad, decimal gunluk, PayrollYanHakOdemeSekli odeme, string kart) =>
                gunluk > 0
                    ? $"{ad} {gunluk.ToString("N2", TrCulture)} TL/gün ({(odeme == PayrollYanHakOdemeSekli.Kart ? kart : "Nakit")})"
                    : $"{ad} yok";

            return $"Yol / Yemek: {y.GunSayisi} gün · "
                + Kalem("Yemek", y.YemekGunlukTutar, y.YemekOdemeSekli, "Yemek kartı/çeki") + " · "
                + Kalem("Yol", y.YolGunlukTutar, y.YolOdemeSekli, "Ulaşım kartı/bilet/akbil");
        }

        // ─────────────────────────────────────────────────────────
        // EXCEL FOOTER (yasal açıklama + Bordro Hesaplama logosu)
        // ─────────────────────────────────────────────────────────
        private static void AppendExcelFooter(IXLWorksheet ws, int footerStartRow, int lastCol)
        {
            if (lastCol < 1) lastCol = 1;

            // Satır 1: kısa açıklama
            var freeNoteRange = ws.Range(footerStartRow, 1, footerStartRow, lastCol);
            freeNoteRange.Merge();
            freeNoteRange.Value = PayrollExportFooterTexts.FreeServiceNote;
            freeNoteRange.Style.Font.Bold = true;
            freeNoteRange.Style.Font.FontSize = 10;
            freeNoteRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            freeNoteRange.Style.Alignment.WrapText = true;

            // Satır 2: uzun disclaimer
            int disclaimerRow = footerStartRow + 1;
            var disclaimerRange = ws.Range(disclaimerRow, 1, disclaimerRow, lastCol);
            disclaimerRange.Merge();
            disclaimerRange.Value = PayrollExportFooterTexts.Disclaimer;
            disclaimerRange.Style.Font.FontSize = 9;
            disclaimerRange.Style.Font.FontColor = XLColor.FromHtml("#555555");
            disclaimerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            disclaimerRange.Style.Alignment.WrapText = true;
            disclaimerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            ws.Row(disclaimerRow).Height = 48;

            // Satır 3: logo (sağa hizalı)
            int logoRow = footerStartRow + 2;
            ws.Row(logoRow).Height = 42;

            var logoBytes = LogoBytes.Value;
            if (logoBytes is { Length: > 0 })
            {
                try
                {
                    using var imgStream = new MemoryStream(logoBytes);
                    var picture = ws.AddPicture(imgStream)
                        .MoveTo(ws.Cell(logoRow, Math.Max(lastCol - 3, 1)))
                        .WithSize(243, 48);
                    // WithSize aspect ratio'yu korumaz; logo 486×96 (~5,06:1) → 243×48.
                }
                catch
                {
                    // logo eklenemezse sessiz geç — disclaimer metni yine basılır.
                }
            }
        }

        private int BuildSalaryExcel(IXLWorksheet ws, CalculatePayrollResponse result, CalculatePayrollRequest request, int headerRow)
        {
            bool hasDisability = result.DisabilityType != PayrollDisabilityType.None;
            bool hasEmployer = request.IncludeEmployerCost;
            // Ayrı satır destekte (Turizm, İSG) SGK teşvik kolonu yerine destek kolonu basılır.
            bool hasSupport = result.Months.Any(x => x.EmployerSupportAmount != null);
            bool hasIncentive = request.LawCode != PayrollTesvikKurallari.Indirimsiz && !hasSupport;
            bool hasBes = request.HasMandatoryBes;
            bool hasEmployeeSgkIncentive = result.Months.Any(x => x.EmployeeSgkIncentive != 0);
            bool hasYolYemek = result.YolYemekDahil;
            string supportLabel = result.Months.FirstOrDefault(x => x.EmployerSupportSource != null)?.EmployerSupportSource ?? "Destek";

            string? incentiveSource = result.Months
                .FirstOrDefault(x => x.IncentiveSource != null)?.IncentiveSource;
            string incentiveLabel = incentiveSource != null
                ? $"SGK Teşvik ({incentiveSource})"
                : "SGK Teşvik";

            int col = 1;
            void Header(string text)
            {
                var cell = ws.Cell(headerRow, col++);
                cell.Value = text;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2C5F8A");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            Header("Ay");
            Header("Tutar");
            Header("Brüt Ücret");
            if (hasYolYemek)
            {
                Header("Yemek Tutarı");
                Header("Yol Tutarı");
                Header("SGK'ya Tabi Yan Hak");
                Header("GV'ye Tabi Yan Hak");
            }
            Header("SGK İşçi");
            Header("İSP İşçi");
            if (hasEmployeeSgkIncentive) Header("SGK İşçi Teşvik");
            if (hasDisability) Header("Engel İndirimi");
            Header("GV Matrahı");
            Header("Kümülatif Matrah");
            Header("Hesaplanan GV");
            Header("İstisna GV");
            Header("Ödenecek GV");
            Header("Hesaplanan DV");
            Header("İstisna DV");
            Header("Ödenecek DV");
            if (hasBes) Header("BES Kesintisi");
            Header("Kesintiler");
            Header("Net Ücret");
            if (hasYolYemek) Header("Kartla Ödenen");
            if (hasEmployer)
            {
                Header("SGK İşveren Brüt");
                if (hasIncentive) Header(incentiveLabel);
                Header("SGK İşveren Net");
                Header("İSP İşveren");
                if (hasSupport) Header(supportLabel);
                Header("Toplam İşveren Maliyet");
            }

            int lastCol = col - 1;

            foreach (var m in result.Months)
            {
                int row = headerRow + m.Month;
                col = 1;

                void Num(decimal? val)
                {
                    var c = ws.Cell(row, col++);
                    c.Value = val ?? 0m;
                    c.Style.NumberFormat.Format = MoneyFormat;
                }

                ws.Cell(row, col++).Value = MonthNames[m.Month];
                Num(m.InputAmount);
                Num(m.GrossSalary);
                if (hasYolYemek)
                {
                    Num(m.MealAmount);
                    Num(m.TransportAmount);
                    Num(m.SgkTaxableBenefits);
                    Num(m.IncomeTaxableBenefits);
                }
                Num(m.SgkEmployeeAmount);
                Num(m.UnemploymentEmployeeAmount);
                if (hasEmployeeSgkIncentive) Num(m.EmployeeSgkIncentive);
                if (hasDisability) Num(m.DisabilityExemptionAmount);
                Num(m.IncomeTaxBase);
                Num(m.CumulativeIncomeTaxBase);
                Num(m.CalculatedIncomeTax);
                Num(m.IncomeTaxExemption);
                Num(m.PayableIncomeTax);
                Num(m.CalculatedStampTax);
                Num(m.StampTaxExemption);
                Num(m.PayableStampTax);
                if (hasBes) Num(m.BesAmount);
                Num(m.TotalDeductions);
                Num(m.NetSalary);
                if (hasYolYemek) Num(m.CardPaidBenefits);
                if (hasEmployer)
                {
                    Num(m.SgkEmployerGross);
                    if (hasIncentive) Num(m.SgkEmployerIncentive);
                    Num(m.SgkEmployerNet);
                    Num(m.UnemploymentEmployerAmount);
                    if (hasSupport) Num(m.EmployerSupportAmount);
                    Num(m.TotalEmployerCost);
                }
            }

            int totalRow = headerRow + 13;

            if (result.Totals != null)
            {
                col = 1;

                void Total(decimal? val)
                {
                    var c = ws.Cell(totalRow, col++);
                    c.Value = val ?? 0m;
                    c.Style.NumberFormat.Format = MoneyFormat;
                    c.Style.Font.Bold = true;
                }

                var tc = ws.Cell(totalRow, col++);
                tc.Value = "Toplam";
                tc.Style.Font.Bold = true;
                col++; // skip Tutar column

                Total(result.Totals.TotalGrossSalary);
                if (hasYolYemek)
                {
                    Total(result.Totals.TotalMealAmount);
                    Total(result.Totals.TotalTransportAmount);
                    Total(result.Totals.TotalSgkTaxableBenefits);
                    Total(result.Totals.TotalIncomeTaxableBenefits);
                }
                Total(result.Totals.TotalSgkEmployeeAmount);
                Total(result.Totals.TotalUnemploymentEmployeeAmount);
                if (hasEmployeeSgkIncentive) Total(result.Totals.TotalEmployeeSgkIncentive);
                if (hasDisability) Total(result.Totals.TotalDisabilityExemptionAmount);
                Total(result.Totals.TotalIncomeTaxBase);
                Total(result.Totals.TotalIncomeTaxBase);
                Total(result.Totals.TotalCalculatedIncomeTax);
                Total(result.Totals.TotalIncomeTaxExemption);
                Total(result.Totals.TotalPayableIncomeTax);
                Total(result.Totals.TotalCalculatedStampTax);
                Total(result.Totals.TotalStampTaxExemption);
                Total(result.Totals.TotalPayableStampTax);
                if (hasBes) Total(result.Totals.TotalBesAmount);
                Total(result.Totals.TotalDeductions);
                Total(result.Totals.TotalNetSalary);
                if (hasYolYemek) Total(result.Totals.TotalCardPaidBenefits);
                if (hasEmployer)
                {
                    Total(result.Totals.TotalSgkEmployerGross);
                    if (hasIncentive) Total(result.Totals.TotalSgkEmployerIncentive);
                    Total(result.Totals.TotalSgkEmployerNet);
                    Total(result.Totals.TotalUnemploymentEmployerAmount);
                    if (hasSupport) Total(result.Totals.TotalEmployerSupportAmount);
                    Total(result.Totals.TotalEmployerCost);
                }
            }

            ws.Range(headerRow, 1, totalRow, lastCol).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(headerRow, 1, totalRow, lastCol).Style.Border.InsideBorder = XLBorderStyleValues.Hair;

            return lastCol;
        }

        private int BuildHonorariumExcel(IXLWorksheet ws, CalculatePayrollResponse result, int headerRow)
        {
            int col = 1;
            void Header(string text)
            {
                var cell = ws.Cell(headerRow, col++);
                cell.Value = text;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#92400e");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            Header("Ay");
            Header("Tutar");
            Header("Brüt Huzur Hakkı");
            Header("GV Matrahı");
            Header("Kümülatif Matrah");
            Header("Hesaplanan GV");
            Header("İstisna GV");
            Header("Ödenecek GV");
            Header("Hesaplanan DV");
            Header("İstisna DV");
            Header("Ödenecek DV");
            Header("Kesintiler");
            Header("Vergi Yükü");
            Header("Net Huzur Hakkı");

            int lastCol = col - 1;

            foreach (var m in result.HonorariumMonths)
            {
                int row = headerRow + m.Month;
                col = 1;

                void Num(decimal val)
                {
                    var c = ws.Cell(row, col++);
                    c.Value = val;
                    c.Style.NumberFormat.Format = MoneyFormat;
                }

                ws.Cell(row, col++).Value = MonthNames[m.Month];
                Num(m.InputAmount);
                Num(m.GrossHonorarium);
                Num(m.IncomeTaxBase);
                Num(m.CumulativeIncomeTaxBase);
                Num(m.CalculatedIncomeTax);
                Num(m.IncomeTaxExemption);
                Num(m.PayableIncomeTax);
                Num(m.CalculatedStampTax);
                Num(m.StampTaxExemption);
                Num(m.PayableStampTax);
                Num(m.TotalDeductions);

                var burdenCell = ws.Cell(row, col++);
                burdenCell.Value = m.TaxBurdenRate;
                burdenCell.Style.NumberFormat.Format = PercentFormat;

                Num(m.NetHonorarium);
            }

            int totalRow = headerRow + 13;

            if (result.HonorariumTotals != null)
            {
                col = 1;

                void Total(decimal val)
                {
                    var c = ws.Cell(totalRow, col++);
                    c.Value = val;
                    c.Style.NumberFormat.Format = MoneyFormat;
                    c.Style.Font.Bold = true;
                }

                var tc = ws.Cell(totalRow, col++);
                tc.Value = "Toplam";
                tc.Style.Font.Bold = true;
                col++; // skip Tutar

                Total(result.HonorariumTotals.TotalGrossHonorarium);
                Total(result.HonorariumTotals.TotalIncomeTaxBase);
                Total(result.HonorariumTotals.TotalIncomeTaxBase);
                Total(result.HonorariumTotals.TotalCalculatedIncomeTax);
                Total(result.HonorariumTotals.TotalIncomeTaxExemption);
                Total(result.HonorariumTotals.TotalPayableIncomeTax);
                Total(result.HonorariumTotals.TotalCalculatedStampTax);
                Total(result.HonorariumTotals.TotalStampTaxExemption);
                Total(result.HonorariumTotals.TotalPayableStampTax);
                Total(result.HonorariumTotals.TotalDeductions);

                var burdenCell = ws.Cell(totalRow, col++);
                burdenCell.Value = result.HonorariumTotals.AverageTaxBurdenRate;
                burdenCell.Style.NumberFormat.Format = PercentFormat;
                burdenCell.Style.Font.Bold = true;

                Total(result.HonorariumTotals.TotalNetHonorarium);
            }

            ws.Range(headerRow, 1, totalRow, lastCol).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(headerRow, 1, totalRow, lastCol).Style.Border.InsideBorder = XLBorderStyleValues.Hair;

            return lastCol;
        }

        // ─────────────────────────────────────────────────────────
        // PDF
        // ─────────────────────────────────────────────────────────
        public byte[] ExportToPdf(CalculatePayrollResponse result, CalculatePayrollRequest request)
        {
            bool isHonorarium = result.EmployeeType == PayrollEmployeeType.Honorarium;
            string calcLabel = result.CalculationType == PayrollCalculationType.GrossToNet
                ? "Brütten Nete" : "Netten Brüte";
            string title = isHonorarium
                ? $"Huzur Hakkı Hesaplama — {result.Year}"
                : $"Ücret Bordrosu — {result.Year}";

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(t => t.FontSize(8).FontFamily(Fonts.Lato));

                    page.Header().Column(col =>
                    {
                        col.Item().Text(title).Bold().FontSize(14).FontColor(Colors.Grey.Darken4);
                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Text($"Kanun: {PayrollTesvikKurallari.Etiket(request.LawCode, request.TesvikAltSecimi)}").FontSize(8).FontColor(Colors.Grey.Darken1);
                            r.RelativeItem().Text(calcLabel).FontSize(8).FontColor(Colors.Grey.Darken1);
                            r.RelativeItem().AlignRight().Text($"Tarih: {DateTime.Now.ToString("dd.MM.yyyy", TrCulture)}")
                                .FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                        if (result.YolYemekDahil && YolYemekBilgisi(request) is { } yolYemek)
                            col.Item().Text(yolYemek).FontSize(8).FontColor(Colors.Grey.Darken1);
                        col.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                    });

                    page.Content().PaddingVertical(8).Column(col =>
                    {
                        col.Item().Element(content =>
                        {
                            if (isHonorarium)
                                BuildHonorariumPdf(content, result);
                            else
                                BuildSalaryPdf(content, result, request);
                        });

                        // Yasal açıklama + logo — tabloyla aynı içerikte yer alır, sayfa taşarsa
                        // QuestPDF Column akışıyla bir sonraki sayfaya düzgün taşınır.
                        col.Item().PaddingTop(12).Element(BuildPdfDisclaimerBlock);
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Sayfa ").FontSize(7).FontColor(Colors.Grey.Darken1);
                        t.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Darken1);
                        t.Span(" / ").FontSize(7).FontColor(Colors.Grey.Darken1);
                        t.TotalPages().FontSize(7).FontColor(Colors.Grey.Darken1);
                    });
                });
            }).GeneratePdf();
        }

        private void BuildSalaryPdf(IContainer container, CalculatePayrollResponse result, CalculatePayrollRequest request)
        {
            bool hasDisability = result.DisabilityType != PayrollDisabilityType.None;
            bool hasEmployer = request.IncludeEmployerCost;
            // Ayrı satır destekte (Turizm, İSG) SGK teşvik kolonu yerine destek kolonu basılır.
            bool hasSupport = result.Months.Any(x => x.EmployerSupportAmount != null);
            bool hasIncentive = request.LawCode != PayrollTesvikKurallari.Indirimsiz && !hasSupport;
            bool hasBes = request.HasMandatoryBes;
            bool hasEmployeeSgkIncentive = result.Months.Any(x => x.EmployeeSgkIncentive != 0);
            bool hasYolYemek = result.YolYemekDahil;
            string supportLabel = result.Months.FirstOrDefault(x => x.EmployerSupportSource != null)?.EmployerSupportSource ?? "Destek";

            var headers = new List<string> { "Ay", "Tutar", "Brüt" };
            if (hasYolYemek) headers.AddRange(new[] { "Yemek", "Yol", "SGK Tabi YH", "GV Tabi YH" });
            headers.AddRange(new[] { "SGK İ", "İSP İ" });
            if (hasEmployeeSgkIncentive) headers.Add("SGK İ Teşvik");
            if (hasDisability) headers.Add("Engel");
            headers.AddRange(new[]
            {
                "GV Matrah", "Küm. Matrah", "Hes. GV", "İst. GV", "Öd. GV",
                "Hes. DV", "İst. DV", "Öd. DV"
            });
            if (hasBes) headers.Add("BES");
            headers.AddRange(new[] { "Kesintiler", "Net" });
            if (hasYolYemek) headers.Add("Kartla Öd.");
            if (hasEmployer)
            {
                headers.Add("İşvr Brüt");
                if (hasIncentive) headers.Add("Teşvik");
                headers.AddRange(new[] { "İşvr Net", "İSP İşvr" });
                if (hasSupport) headers.Add(supportLabel);
                headers.Add("Toplam İşvr");
            }

            container.Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    foreach (var _ in headers) c.RelativeColumn();
                });

                // Header
                foreach (var h in headers)
                {
                    t.Cell().Background("#2C5F8A").PaddingVertical(3).PaddingHorizontal(2)
                        .AlignCenter()
                        .Text(h).FontColor(Colors.White).Bold().FontSize(7);
                }

                // Data
                foreach (var m in result.Months)
                {
                    Cell(t, MonthNames[m.Month], align: "left");
                    Cell(t, FormatMoney(m.InputAmount));
                    Cell(t, FormatMoney(m.GrossSalary));
                    if (hasYolYemek)
                    {
                        Cell(t, FormatMoney(m.MealAmount));
                        Cell(t, FormatMoney(m.TransportAmount));
                        Cell(t, FormatMoney(m.SgkTaxableBenefits));
                        Cell(t, FormatMoney(m.IncomeTaxableBenefits));
                    }
                    Cell(t, FormatMoney(m.SgkEmployeeAmount));
                    Cell(t, FormatMoney(m.UnemploymentEmployeeAmount));
                    if (hasEmployeeSgkIncentive) Cell(t, FormatMoney(m.EmployeeSgkIncentive));
                    if (hasDisability) Cell(t, FormatMoney(m.DisabilityExemptionAmount));
                    Cell(t, FormatMoney(m.IncomeTaxBase));
                    Cell(t, FormatMoney(m.CumulativeIncomeTaxBase));
                    Cell(t, FormatMoney(m.CalculatedIncomeTax));
                    Cell(t, FormatMoney(m.IncomeTaxExemption));
                    Cell(t, FormatMoney(m.PayableIncomeTax));
                    Cell(t, FormatMoney(m.CalculatedStampTax));
                    Cell(t, FormatMoney(m.StampTaxExemption));
                    Cell(t, FormatMoney(m.PayableStampTax));
                    if (hasBes) Cell(t, FormatMoney(m.BesAmount));
                    Cell(t, FormatMoney(m.TotalDeductions));
                    Cell(t, FormatMoney(m.NetSalary));
                    if (hasYolYemek) Cell(t, FormatMoney(m.CardPaidBenefits));
                    if (hasEmployer)
                    {
                        Cell(t, FormatMoney(m.SgkEmployerGross ?? 0));
                        if (hasIncentive) Cell(t, FormatMoney(m.SgkEmployerIncentive ?? 0));
                        Cell(t, FormatMoney(m.SgkEmployerNet ?? 0));
                        Cell(t, FormatMoney(m.UnemploymentEmployerAmount ?? 0));
                        if (hasSupport) Cell(t, FormatMoney(m.EmployerSupportAmount ?? 0));
                        Cell(t, FormatMoney(m.TotalEmployerCost ?? 0));
                    }
                }

                // Totals
                if (result.Totals != null)
                {
                    var tot = result.Totals;
                    Cell(t, "Toplam", bold: true, bg: "#f9fafb", align: "left");
                    Cell(t, "", bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalGrossSalary), bold: true, bg: "#f9fafb");
                    if (hasYolYemek)
                    {
                        Cell(t, FormatMoney(tot.TotalMealAmount), bold: true, bg: "#f9fafb");
                        Cell(t, FormatMoney(tot.TotalTransportAmount), bold: true, bg: "#f9fafb");
                        Cell(t, FormatMoney(tot.TotalSgkTaxableBenefits), bold: true, bg: "#f9fafb");
                        Cell(t, FormatMoney(tot.TotalIncomeTaxableBenefits), bold: true, bg: "#f9fafb");
                    }
                    Cell(t, FormatMoney(tot.TotalSgkEmployeeAmount), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalUnemploymentEmployeeAmount), bold: true, bg: "#f9fafb");
                    if (hasEmployeeSgkIncentive) Cell(t, FormatMoney(tot.TotalEmployeeSgkIncentive), bold: true, bg: "#f9fafb");
                    if (hasDisability) Cell(t, FormatMoney(tot.TotalDisabilityExemptionAmount), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalIncomeTaxBase), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalIncomeTaxBase), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalCalculatedIncomeTax), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalIncomeTaxExemption), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalPayableIncomeTax), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalCalculatedStampTax), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalStampTaxExemption), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalPayableStampTax), bold: true, bg: "#f9fafb");
                    if (hasBes) Cell(t, FormatMoney(tot.TotalBesAmount), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalDeductions), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalNetSalary), bold: true, bg: "#f9fafb");
                    if (hasYolYemek) Cell(t, FormatMoney(tot.TotalCardPaidBenefits), bold: true, bg: "#f9fafb");
                    if (hasEmployer)
                    {
                        Cell(t, FormatMoney(tot.TotalSgkEmployerGross ?? 0), bold: true, bg: "#f9fafb");
                        if (hasIncentive) Cell(t, FormatMoney(tot.TotalSgkEmployerIncentive ?? 0), bold: true, bg: "#f9fafb");
                        Cell(t, FormatMoney(tot.TotalSgkEmployerNet ?? 0), bold: true, bg: "#f9fafb");
                        Cell(t, FormatMoney(tot.TotalUnemploymentEmployerAmount ?? 0), bold: true, bg: "#f9fafb");
                        if (hasSupport) Cell(t, FormatMoney(tot.TotalEmployerSupportAmount ?? 0), bold: true, bg: "#f9fafb");
                        Cell(t, FormatMoney(tot.TotalEmployerCost ?? 0), bold: true, bg: "#f9fafb");
                    }
                }
            });
        }

        private void BuildHonorariumPdf(IContainer container, CalculatePayrollResponse result)
        {
            var headers = new[]
            {
                "Ay", "Tutar", "Brüt", "GV Matrah", "Küm. Matrah",
                "Hes. GV", "İst. GV", "Öd. GV",
                "Hes. DV", "İst. DV", "Öd. DV",
                "Kesintiler", "Vergi Yükü", "Net"
            };

            container.Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    foreach (var _ in headers) c.RelativeColumn();
                });

                foreach (var h in headers)
                {
                    t.Cell().Background("#92400e").PaddingVertical(3).PaddingHorizontal(2)
                        .AlignCenter()
                        .Text(h).FontColor(Colors.White).Bold().FontSize(7);
                }

                foreach (var m in result.HonorariumMonths)
                {
                    Cell(t, MonthNames[m.Month], align: "left");
                    Cell(t, FormatMoney(m.InputAmount));
                    Cell(t, FormatMoney(m.GrossHonorarium));
                    Cell(t, FormatMoney(m.IncomeTaxBase));
                    Cell(t, FormatMoney(m.CumulativeIncomeTaxBase));
                    Cell(t, FormatMoney(m.CalculatedIncomeTax));
                    Cell(t, FormatMoney(m.IncomeTaxExemption));
                    Cell(t, FormatMoney(m.PayableIncomeTax));
                    Cell(t, FormatMoney(m.CalculatedStampTax));
                    Cell(t, FormatMoney(m.StampTaxExemption));
                    Cell(t, FormatMoney(m.PayableStampTax));
                    Cell(t, FormatMoney(m.TotalDeductions));
                    Cell(t, FormatPercent(m.TaxBurdenRate));
                    Cell(t, FormatMoney(m.NetHonorarium));
                }

                if (result.HonorariumTotals != null)
                {
                    var tot = result.HonorariumTotals;
                    Cell(t, "Toplam", bold: true, bg: "#f9fafb", align: "left");
                    Cell(t, "", bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalGrossHonorarium), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalIncomeTaxBase), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalIncomeTaxBase), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalCalculatedIncomeTax), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalIncomeTaxExemption), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalPayableIncomeTax), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalCalculatedStampTax), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalStampTaxExemption), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalPayableStampTax), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalDeductions), bold: true, bg: "#f9fafb");
                    Cell(t, FormatPercent(tot.AverageTaxBurdenRate), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalNetHonorarium), bold: true, bg: "#f9fafb");
                }
            });
        }

        // ─────────────────────────────────────────────────────────
        // PDF FOOTER (yasal açıklama + Bordro Hesaplama logosu)
        // ─────────────────────────────────────────────────────────
        private static void BuildPdfDisclaimerBlock(IContainer container)
        {
            container
                .BorderTop(0.5f).BorderColor(Colors.Grey.Lighten2)
                .PaddingTop(8)
                .Column(col =>
                {
                    col.Item().Text(PayrollExportFooterTexts.FreeServiceNote)
                        .FontSize(8).Bold().FontColor(Colors.Grey.Darken3);

                    col.Item().PaddingTop(4).Text(PayrollExportFooterTexts.Disclaimer)
                        .FontSize(7).FontColor(Colors.Grey.Darken1);

                    var logoBytes = LogoBytes.Value;
                    if (logoBytes is { Length: > 0 })
                    {
                        col.Item().PaddingTop(8).AlignRight()
                            .Width(160)
                            .Image(logoBytes).FitWidth();
                    }
                });
        }

        private static void Cell(TableDescriptor t, string text, bool bold = false, string? bg = null, string align = "right")
        {
            IContainer container = t.Cell();

            if (bg != null)
                container = container.Background(bg);

            container = container
                .BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2)
                .PaddingVertical(2).PaddingHorizontal(2);

            container = align switch
            {
                "left" => container.AlignLeft(),
                "center" => container.AlignCenter(),
                _ => container.AlignRight()
            };

            container.Text(t2 =>
            {
                var span = t2.Span(text).FontSize(7);
                if (bold) span.Bold();
            });
        }

        private static string FormatMoney(decimal value)
            => value.ToString("N2", TrCulture);

        private static string FormatPercent(decimal value)
            => $"%{value.ToString("N2", TrCulture)}";
    }
}
