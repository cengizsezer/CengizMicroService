using System.Globalization;
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

            ws.Cell(2, 1).Value = $"Kanun: {request.LawCode}";
            ws.Cell(2, 3).Value = calcLabel;
            ws.Cell(2, 5).Value = $"Tarih: {DateTime.Now.ToString("dd.MM.yyyy", TrCulture)}";

            const int headerRow = 4;

            if (isHonorarium)
                BuildHonorariumExcel(ws, result, headerRow);
            else
                BuildSalaryExcel(ws, result, request, headerRow);

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            return stream.ToArray();
        }

        private void BuildSalaryExcel(IXLWorksheet ws, CalculatePayrollResponse result, CalculatePayrollRequest request, int headerRow)
        {
            bool hasDisability = result.DisabilityType != PayrollDisabilityType.None;
            bool hasEmployer = request.IncludeEmployerCost;
            bool hasIncentive = request.LawCode != "00000";

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
            Header("SGK İşçi");
            Header("İSP İşçi");
            if (hasDisability) Header("Engel İndirimi");
            Header("GV Matrahı");
            Header("Kümülatif Matrah");
            Header("Hesaplanan GV");
            Header("İstisna GV");
            Header("Ödenecek GV");
            Header("Hesaplanan DV");
            Header("İstisna DV");
            Header("Ödenecek DV");
            Header("Kesintiler");
            Header("Net Ücret");
            if (hasEmployer)
            {
                Header("SGK İşveren Brüt");
                if (hasIncentive) Header(incentiveLabel);
                Header("SGK İşveren Net");
                Header("İSP İşveren");
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
                Num(m.SgkEmployeeAmount);
                Num(m.UnemploymentEmployeeAmount);
                if (hasDisability) Num(m.DisabilityExemptionAmount);
                Num(m.IncomeTaxBase);
                Num(m.CumulativeIncomeTaxBase);
                Num(m.CalculatedIncomeTax);
                Num(m.IncomeTaxExemption);
                Num(m.PayableIncomeTax);
                Num(m.CalculatedStampTax);
                Num(m.StampTaxExemption);
                Num(m.PayableStampTax);
                Num(m.TotalDeductions);
                Num(m.NetSalary);
                if (hasEmployer)
                {
                    Num(m.SgkEmployerGross);
                    if (hasIncentive) Num(m.SgkEmployerIncentive);
                    Num(m.SgkEmployerNet);
                    Num(m.UnemploymentEmployerAmount);
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
                Total(result.Totals.TotalSgkEmployeeAmount);
                Total(result.Totals.TotalUnemploymentEmployeeAmount);
                if (hasDisability) Total(result.Totals.TotalDisabilityExemptionAmount);
                Total(result.Totals.TotalIncomeTaxBase);
                Total(result.Totals.TotalIncomeTaxBase);
                Total(result.Totals.TotalCalculatedIncomeTax);
                Total(result.Totals.TotalIncomeTaxExemption);
                Total(result.Totals.TotalPayableIncomeTax);
                Total(result.Totals.TotalCalculatedStampTax);
                Total(result.Totals.TotalStampTaxExemption);
                Total(result.Totals.TotalPayableStampTax);
                Total(result.Totals.TotalDeductions);
                Total(result.Totals.TotalNetSalary);
                if (hasEmployer)
                {
                    Total(result.Totals.TotalSgkEmployerGross);
                    if (hasIncentive) Total(result.Totals.TotalSgkEmployerIncentive);
                    Total(result.Totals.TotalSgkEmployerNet);
                    Total(result.Totals.TotalUnemploymentEmployerAmount);
                    Total(result.Totals.TotalEmployerCost);
                }
            }

            ws.Range(headerRow, 1, totalRow, lastCol).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(headerRow, 1, totalRow, lastCol).Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        }

        private void BuildHonorariumExcel(IXLWorksheet ws, CalculatePayrollResponse result, int headerRow)
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
                            r.RelativeItem().Text($"Kanun: {request.LawCode}").FontSize(8).FontColor(Colors.Grey.Darken1);
                            r.RelativeItem().Text(calcLabel).FontSize(8).FontColor(Colors.Grey.Darken1);
                            r.RelativeItem().AlignRight().Text($"Tarih: {DateTime.Now.ToString("dd.MM.yyyy", TrCulture)}")
                                .FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                        col.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                    });

                    page.Content().PaddingVertical(8).Element(content =>
                    {
                        if (isHonorarium)
                            BuildHonorariumPdf(content, result);
                        else
                            BuildSalaryPdf(content, result, request);
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
            bool hasIncentive = request.LawCode != "00000";

            var headers = new List<string> { "Ay", "Tutar", "Brüt", "SGK İ", "İSP İ" };
            if (hasDisability) headers.Add("Engel");
            headers.AddRange(new[]
            {
                "GV Matrah", "Küm. Matrah", "Hes. GV", "İst. GV", "Öd. GV",
                "Hes. DV", "İst. DV", "Öd. DV", "Kesintiler", "Net"
            });
            if (hasEmployer)
            {
                headers.Add("İşvr Brüt");
                if (hasIncentive) headers.Add("Teşvik");
                headers.AddRange(new[] { "İşvr Net", "İSP İşvr", "Toplam İşvr" });
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
                    Cell(t, FormatMoney(m.SgkEmployeeAmount));
                    Cell(t, FormatMoney(m.UnemploymentEmployeeAmount));
                    if (hasDisability) Cell(t, FormatMoney(m.DisabilityExemptionAmount));
                    Cell(t, FormatMoney(m.IncomeTaxBase));
                    Cell(t, FormatMoney(m.CumulativeIncomeTaxBase));
                    Cell(t, FormatMoney(m.CalculatedIncomeTax));
                    Cell(t, FormatMoney(m.IncomeTaxExemption));
                    Cell(t, FormatMoney(m.PayableIncomeTax));
                    Cell(t, FormatMoney(m.CalculatedStampTax));
                    Cell(t, FormatMoney(m.StampTaxExemption));
                    Cell(t, FormatMoney(m.PayableStampTax));
                    Cell(t, FormatMoney(m.TotalDeductions));
                    Cell(t, FormatMoney(m.NetSalary));
                    if (hasEmployer)
                    {
                        Cell(t, FormatMoney(m.SgkEmployerGross ?? 0));
                        if (hasIncentive) Cell(t, FormatMoney(m.SgkEmployerIncentive ?? 0));
                        Cell(t, FormatMoney(m.SgkEmployerNet ?? 0));
                        Cell(t, FormatMoney(m.UnemploymentEmployerAmount ?? 0));
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
                    Cell(t, FormatMoney(tot.TotalSgkEmployeeAmount), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalUnemploymentEmployeeAmount), bold: true, bg: "#f9fafb");
                    if (hasDisability) Cell(t, FormatMoney(tot.TotalDisabilityExemptionAmount), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalIncomeTaxBase), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalIncomeTaxBase), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalCalculatedIncomeTax), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalIncomeTaxExemption), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalPayableIncomeTax), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalCalculatedStampTax), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalStampTaxExemption), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalPayableStampTax), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalDeductions), bold: true, bg: "#f9fafb");
                    Cell(t, FormatMoney(tot.TotalNetSalary), bold: true, bg: "#f9fafb");
                    if (hasEmployer)
                    {
                        Cell(t, FormatMoney(tot.TotalSgkEmployerGross ?? 0), bold: true, bg: "#f9fafb");
                        if (hasIncentive) Cell(t, FormatMoney(tot.TotalSgkEmployerIncentive ?? 0), bold: true, bg: "#f9fafb");
                        Cell(t, FormatMoney(tot.TotalSgkEmployerNet ?? 0), bold: true, bg: "#f9fafb");
                        Cell(t, FormatMoney(tot.TotalUnemploymentEmployerAmount ?? 0), bold: true, bg: "#f9fafb");
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
