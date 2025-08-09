using System.Globalization;
using OCRService.Api.Contracts.Dtos;
using OCRService.Api.Core.Entitiy;

namespace OCRService.Api.Application.Mapping
{
    public static class AnalysisMapper
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // " %20 " -> 0.20m  |  "1" -> 0.01m
        private static decimal ParseRateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return 0m;
            var s = key.Trim().Replace("%", "");
            return decimal.TryParse(s, NumberStyles.Any, Inv, out var pct) && pct > 0
                ? pct / 100m
                : 0m;
        }

        public static ReceiptAnalysis? ToEntity(string extractedText, OcrInterpretationDto? dto)
        {
            if (dto is null) return null;

            var entity = new ReceiptAnalysis
            {
                ExtractedText = extractedText ?? string.Empty,
                CompanyName = dto.CompanyName ?? string.Empty,
                ReceiptNumber = dto.InvoiceNumber ?? string.Empty,
                NetTotal = dto.BaseAmount
            };

            if (dto.LsVatDetails != null)
            {
                foreach (var kv in dto.LsVatDetails)
                {
                    var rate = ParseRateKey(kv.Key);
                    if (rate <= 0m) continue;

                    var v = kv.Value ?? new VatDetailDto();

                    var baseAmount = v.BaseAmount;
                    var vat = v.BaseVat;

                    // Matrah boşsa KDV'den türet: matrah = kdv / rate
                    if (baseAmount <= 0m && vat > 0m)
                        baseAmount = Math.Round(vat / rate, 2, MidpointRounding.AwayFromZero);

                    // Toplamı 0 olan kalemleri atla
                    if (baseAmount <= 0m && vat <= 0m) continue;

                    entity.VatBreakdowns.Add(new VatBreakdown
                    {
                        Rate = rate,        // 0.20m gibi
                        BaseAmount = baseAmount,  // Matrah
                        Vat = vat          // KDV
                    });
                }
            }

            // NetTotal yok/0 ise detaylardan topla
            if (entity.NetTotal <= 0m && entity.VatBreakdowns.Count > 0)
            {
                entity.NetTotal = entity.VatBreakdowns.Sum(x => x.BaseAmount);
            }

            // (Opsiyonel) Tutarlılık kontrolü: küçük sapmaları yuvarla
            var sumNet = entity.VatBreakdowns.Sum(x => x.BaseAmount);
            if (Math.Abs(entity.NetTotal - sumNet) <= 0.05m) // 5 kuruş tolerans
            {
                entity.NetTotal = sumNet;
            }

            return entity;
        }
    }
}
