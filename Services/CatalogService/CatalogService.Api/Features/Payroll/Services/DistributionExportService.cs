using System.Globalization;
using CatalogService.Api.Features.Payroll.Dtos.Shared;
using CatalogService.Api.Features.Payroll.Services.Interfaces;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CatalogService.Api.Features.Payroll.Services
{
    public class DistributionExportService : IDistributionExportService
    {
        private static readonly CultureInfo TrCulture = CultureInfo.GetCultureInfo("tr-TR");
        private const string MoneyFormat = "#,##0.00 \"TL\"";
        private const string PercentFormat = "0.00\"%\"";

        public byte[] ExportToExcel(DistributionComparisonResultDto result, decimal stopajRate, int year)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Karşılaştırma");

            // ── Header
            ws.Cell(1, 1).Value = $"Kar Dağıtımı vs Huzur Hakkı Karşılaştırma — {year}";
            ws.Range(1, 1, 1, 3).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(2, 1).Value = $"Stopaj Oranı: %{(stopajRate * 100m).ToString("0.##", TrCulture)}";
            ws.Cell(2, 2).Value = $"Beyan Sınırı: {result.BeyanSiniri.ToString("N2", TrCulture)} TL";
            ws.Cell(2, 3).Value = $"Tarih: {DateTime.Now.ToString("dd.MM.yyyy", TrCulture)}";
            ws.Range(2, 1, 2, 3).Style.Font.Italic = true;
            ws.Range(2, 1, 2, 3).Style.Font.FontColor = XLColor.FromHtml("#6b7280");

            int row = 4;

            // ── HUZUR HAKKI block
            row = WriteSectionHeader(ws, row, "HUZUR HAKKI YÖNTEMİ", "#dbeafe", "#1e40af");

            var hh = result.HuzurHakki;
            row = WriteMoneyRow(ws, row, "A", "Yıllık Brüt Huzur Hakkı", hh.A_YillikBrut);
            row = WriteMoneyRow(ws, row, "B", "Vergi Maliyeti", hh.B_VergiMaliyeti);
            row = WritePercentRow(ws, row, "C", "Ortalama Vergi Oranı", hh.C_OrtalamaVergiOrani);
            row = WriteMoneyRow(ws, row, "D", "Net Ele Geçen", hh.D_NetEleGecen);
            row = WritePercentRow(ws, row, "E", "Geçici Vergi Oranı", hh.E_GeciciVergiOrani * 100m);
            row = WriteMoneyRow(ws, row, "F = A × E", "Geçici Vergi Avantajı", hh.F_GeciciVergiAvantaji);
            row = WriteMoneyRow(ws, row, "G = B − F", "Net Vergi Maliyeti", hh.G_NetVergiMaliyeti, bold: true);
            row++;

            // ── KAR DAĞITIM block
            row = WriteSectionHeader(ws, row, "KAR DAĞITIM YÖNTEMİ", "#fef3c7", "#92400e");

            var kd = result.KarDagitimi;
            row = WriteMoneyRow(ws, row, "A", "Brüt Kar Dağıtımı", kd.A_BrutKarDagitimi);
            row = WriteMoneyRow(ws, row, $"B (%{(kd.StopajOrani * 100m).ToString("0.##", TrCulture)})", "Kar Dağıtım Stopajı", kd.B_Stopaj);
            row = WriteMoneyRow(ws, row, "C = A − B", "Net Ele Geçen", kd.C_NetEleGecen);
            row = WriteMoneyRow(ws, row, "D = A / 2", "%50 İstisna", kd.D_YuzdeElliIstisna);
            row = WriteMoneyRow(ws, row, "E", "Beyana Tabi Gelir", kd.E_BeyanaTabiGelir);
            row = WriteMoneyRow(ws, row, "F", "Gelir Vergisi", kd.F_GelirVergisi);
            row = WritePercentRow(ws, row, "", "Ortalama Vergi Oranı", kd.OrtalamaVergiOrani);

            var ilaveLabel = kd.G_IlaveVergi >= 0 ? "İlave Vergi (Ödenecek)" : "İade";
            var ilaveColor = kd.G_IlaveVergi >= 0 ? "#b91c1c" : "#15803d";
            row = WriteMoneyRow(ws, row, "G = F − B", ilaveLabel, kd.G_IlaveVergi, valueColor: ilaveColor);
            row = WriteMoneyRow(ws, row, "", "Net Vergi Maliyeti", kd.NetVergiMaliyeti, bold: true);
            row++;

            // ── KARŞILAŞTIRMA block
            row = WriteSectionHeader(ws, row, "KARŞILAŞTIRMA", "#dcfce7", "#15803d");

            row = WriteMoneyRow(ws, row, "", "Huzur Hakkı Yöntemi Maliyeti", hh.G_NetVergiMaliyeti);
            row = WriteMoneyRow(ws, row, "", "Kar Dağıtım Yöntemi Maliyeti", kd.NetVergiMaliyeti);

            ws.Cell(row, 1).Value = "";
            ws.Cell(row, 2).Value = "Avantajlı Yöntem";
            ws.Cell(row, 3).Value = AvantajliYontemText(result.AvantajliYontem);
            ws.Cell(row, 3).Style.Font.Bold = true;
            ws.Cell(row, 3).Style.Font.FontColor = XLColor.FromHtml("#15803d");
            row++;

            row = WriteMoneyRow(ws, row, "", "Net Fark", result.NetFark, bold: true);

            // ── Column widths
            ws.Column(1).Width = 18;
            ws.Column(2).Width = 42;
            ws.Column(3).Width = 22;

            ws.Range(1, 1, row - 1, 3).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            return stream.ToArray();
        }

        private static int WriteSectionHeader(IXLWorksheet ws, int row, string text, string bgHex, string fgHex)
        {
            ws.Cell(row, 1).Value = text;
            ws.Range(row, 1, row, 3).Merge();
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 12;
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml(fgHex);
            ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml(bgHex);
            ws.Cell(row, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(row).Height = 22;
            return row + 1;
        }

        private static int WriteMoneyRow(
            IXLWorksheet ws,
            int row,
            string code,
            string label,
            decimal value,
            bool bold = false,
            string? valueColor = null)
        {
            ws.Cell(row, 1).Value = code;
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#6b7280");
            ws.Cell(row, 1).Style.Font.FontName = "Consolas";
            ws.Cell(row, 1).Style.Font.FontSize = 10;

            ws.Cell(row, 2).Value = label;

            ws.Cell(row, 3).Value = value;
            ws.Cell(row, 3).Style.NumberFormat.Format = MoneyFormat;
            ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            if (bold)
            {
                ws.Cell(row, 2).Style.Font.Bold = true;
                ws.Cell(row, 3).Style.Font.Bold = true;
                ws.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.FromHtml("#f9fafb");
            }

            if (valueColor != null)
            {
                ws.Cell(row, 3).Style.Font.FontColor = XLColor.FromHtml(valueColor);
                ws.Cell(row, 3).Style.Font.Bold = true;
            }

            return row + 1;
        }

        private static int WritePercentRow(IXLWorksheet ws, int row, string code, string label, decimal value)
        {
            ws.Cell(row, 1).Value = code;
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#6b7280");
            ws.Cell(row, 1).Style.Font.FontName = "Consolas";
            ws.Cell(row, 1).Style.Font.FontSize = 10;

            ws.Cell(row, 2).Value = label;

            ws.Cell(row, 3).Value = value;
            ws.Cell(row, 3).Style.NumberFormat.Format = PercentFormat;
            ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            return row + 1;
        }

        public byte[] ExportToPdf(DistributionComparisonResultDto result, decimal stopajRate, int year)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(t => t.FontSize(10).FontFamily(Fonts.Lato));

                    page.Header().Column(col =>
                    {
                        col.Item().Text($"Kar Dağıtımı vs Huzur Hakkı Karşılaştırma — {year}")
                            .Bold().FontSize(16).FontColor(Colors.Grey.Darken4);

                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Text($"Stopaj Oranı: %{(stopajRate * 100m).ToString("0.##", TrCulture)}")
                                .Italic().FontSize(9).FontColor(Colors.Grey.Darken1);
                            r.RelativeItem().Text($"Beyan Sınırı: {FormatMoney(result.BeyanSiniri)}")
                                .Italic().FontSize(9).FontColor(Colors.Grey.Darken1);
                            r.RelativeItem().AlignRight().Text($"Tarih: {DateTime.Now.ToString("dd.MM.yyyy", TrCulture)}")
                                .Italic().FontSize(9).FontColor(Colors.Grey.Darken1);
                        });

                        col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Spacing(10);

                        // ── HUZUR HAKKI
                        col.Item().Element(e => BuildSection(e, "HUZUR HAKKI YÖNTEMİ", "#1e40af", "#dbeafe"));
                        var hh = result.HuzurHakki;
                        col.Item().Element(e => BuildTable(e, new[]
                        {
                            ("A", "Yıllık Brüt Huzur Hakkı", FormatMoney(hh.A_YillikBrut), false, (string?)null),
                            ("B", "Vergi Maliyeti", FormatMoney(hh.B_VergiMaliyeti), false, null),
                            ("C", "Ortalama Vergi Oranı", FormatPercent(hh.C_OrtalamaVergiOrani), false, null),
                            ("D", "Net Ele Geçen", FormatMoney(hh.D_NetEleGecen), false, null),
                            ("E", "Geçici Vergi Oranı", FormatPercent(hh.E_GeciciVergiOrani * 100m), false, null),
                            ("F = A × E", "Geçici Vergi Avantajı", FormatMoney(hh.F_GeciciVergiAvantaji), false, null),
                            ("G = B − F", "Net Vergi Maliyeti", FormatMoney(hh.G_NetVergiMaliyeti), true, null),
                        }));

                        // ── KAR DAĞITIM
                        col.Item().Element(e => BuildSection(e, "KAR DAĞITIM YÖNTEMİ", "#92400e", "#fef3c7"));
                        var kd = result.KarDagitimi;
                        var ilaveLabel = kd.G_IlaveVergi >= 0 ? "İlave Vergi (Ödenecek)" : "İade";
                        var ilaveColor = kd.G_IlaveVergi >= 0 ? "#b91c1c" : "#15803d";

                        col.Item().Element(e => BuildTable(e, new[]
                        {
                            ("A", "Brüt Kar Dağıtımı", FormatMoney(kd.A_BrutKarDagitimi), false, (string?)null),
                            ($"B (%{(kd.StopajOrani * 100m).ToString("0.##", TrCulture)})", "Kar Dağıtım Stopajı", FormatMoney(kd.B_Stopaj), false, null),
                            ("C = A − B", "Net Ele Geçen", FormatMoney(kd.C_NetEleGecen), false, null),
                            ("D = A / 2", "%50 İstisna", FormatMoney(kd.D_YuzdeElliIstisna), false, null),
                            ("E", "Beyana Tabi Gelir", FormatMoney(kd.E_BeyanaTabiGelir), false, null),
                            ("F", "Gelir Vergisi", FormatMoney(kd.F_GelirVergisi), false, null),
                            ("", "Ortalama Vergi Oranı", FormatPercent(kd.OrtalamaVergiOrani), false, null),
                            ("G = F − B", ilaveLabel, FormatMoney(kd.G_IlaveVergi), false, ilaveColor),
                            ("", "Net Vergi Maliyeti", FormatMoney(kd.NetVergiMaliyeti), true, null),
                        }));

                        // ── KARŞILAŞTIRMA
                        col.Item().Element(e => BuildSection(e, "KARŞILAŞTIRMA", "#15803d", "#dcfce7"));
                        col.Item().Element(e => BuildTable(e, new[]
                        {
                            ("", "Huzur Hakkı Yöntemi Maliyeti", FormatMoney(hh.G_NetVergiMaliyeti), false, (string?)null),
                            ("", "Kar Dağıtım Yöntemi Maliyeti", FormatMoney(kd.NetVergiMaliyeti), false, null),
                            ("", "Avantajlı Yöntem", AvantajliYontemText(result.AvantajliYontem), true, "#15803d"),
                            ("", "Net Fark", FormatMoney(result.NetFark), true, null),
                        }));
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Sayfa ").FontSize(8).FontColor(Colors.Grey.Darken1);
                        t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                        t.Span(" / ").FontSize(8).FontColor(Colors.Grey.Darken1);
                        t.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });
            }).GeneratePdf();
        }

        private static void BuildSection(IContainer container, string title, string fgHex, string bgHex)
        {
            container.Background(bgHex)
                .PaddingVertical(4)
                .PaddingHorizontal(8)
                .Text(title)
                .Bold()
                .FontSize(11)
                .FontColor(fgHex);
        }

        private static void BuildTable(
            IContainer container,
            IEnumerable<(string code, string label, string value, bool bold, string? valueColor)> rows)
        {
            container.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2);
                    c.RelativeColumn(5);
                    c.RelativeColumn(3);
                });

                foreach (var (code, label, value, bold, valueColor) in rows)
                {
                    t.Cell().Background(bold ? "#f9fafb" : Colors.White)
                        .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                        .PaddingVertical(3).PaddingHorizontal(6)
                        .Text(code)
                        .FontFamily("Consolas")
                        .FontSize(9)
                        .FontColor(Colors.Grey.Darken1);

                    t.Cell().Background(bold ? "#f9fafb" : Colors.White)
                        .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                        .PaddingVertical(3).PaddingHorizontal(6)
                        .Text(text =>
                        {
                            var span = text.Span(label).FontSize(10);
                            if (bold) span.Bold();
                        });

                    t.Cell().Background(bold ? "#f9fafb" : Colors.White)
                        .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
                        .PaddingVertical(3).PaddingHorizontal(6)
                        .AlignRight()
                        .Text(text =>
                        {
                            var span = text.Span(value).FontSize(10);
                            if (bold) span.Bold();
                            if (valueColor != null) span.FontColor(valueColor);
                        });
                }
            });
        }

        private static string FormatMoney(decimal value)
            => $"{value.ToString("N2", TrCulture)} TL";

        private static string FormatPercent(decimal value)
            => $"%{value.ToString("N2", TrCulture)}";

        private static string AvantajliYontemText(AvantajliYontem yontem) => yontem switch
        {
            AvantajliYontem.HuzurHakki => "Huzur Hakkı",
            AvantajliYontem.KarDagitimi => "Kar Dağıtımı",
            AvantajliYontem.Esit => "Eşit",
            _ => "—"
        };
    }
}
