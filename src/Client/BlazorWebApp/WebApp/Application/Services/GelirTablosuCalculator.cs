using WebApp.Domain.Models.FirmaKontrol;

namespace WebApp.Application.Services
{
    public static class GelirTablosuCalculator
    {
        // 5 yansıtma eşleşmesi: hedef 6'lı kod -> kaynak yansıtma kodu
        public static readonly IReadOnlyDictionary<string, string> YansitmaMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["622"] = "740",
                ["630"] = "750",
                ["631"] = "760",
                ["632"] = "770",
                ["660"] = "780"
            };

        public class ComputedRow
        {
            public MizanSatir Source { get; set; } = default!;
            public decimal? OncekiCari { get; set; }   // Önceki Dönem hesaplanmış
            public decimal? Cari { get; set; }          // Cari Dönem hesaplanmış
            public string? KaynakKod { get; set; }
            public string? YansitmaKod { get; set; }
        }

        public static List<ComputedRow> Compute(
            List<MizanSatir> rows,
            IReadOnlyDictionary<string, decimal?> rawCari,
            IReadOnlyDictionary<string, decimal?> rawOnceki,
            decimal? hesaplananVergi691Cari = null)
        {
            var n = rows.Count;
            var cari = new decimal?[n];
            var onceki = new decimal?[n];
            var kaynakKod = new string?[n];
            var yansitmaKod = new string?[n];

            // 1) Account satırları: own + (varsa) yansıtma raw değer
            //    Kaynak kod tüm tipler için (kod doluysa) gösterilir — 690/691/692 gibi
            //    başlık/total satırlarında da Hesap Kodu sütununun beslenmesi için.
            for (int i = 0; i < n; i++)
            {
                var r = rows[i];

                kaynakKod[i] = string.IsNullOrWhiteSpace(r.Kod) ? null : r.Kod;

                if (r.Tip != SatirTipi.Account) continue;

                decimal? ownCari = r.CariDonem;
                decimal? ownOnceki = r.OncekiDonem;
                decimal? yansitmaCari = null;
                decimal? yansitmaOnceki = null;

                if (!string.IsNullOrEmpty(r.Kod) && YansitmaMap.TryGetValue(r.Kod, out var yKod))
                {
                    yansitmaKod[i] = yKod;
                    if (rawCari.TryGetValue(yKod, out var vc) && vc.HasValue) yansitmaCari = vc;
                    if (rawOnceki.TryGetValue(yKod, out var vo) && vo.HasValue) yansitmaOnceki = vo;
                }

                if (ownCari.HasValue || yansitmaCari.HasValue)
                    cari[i] = (ownCari ?? 0m) + (yansitmaCari ?? 0m);
                if (ownOnceki.HasValue || yansitmaOnceki.HasValue)
                    onceki[i] = (ownOnceki ?? 0m) + (yansitmaOnceki ?? 0m);
            }

            // 2) SubGroup ve MainGroup toplamları
            for (int i = 0; i < n; i++)
            {
                var t = rows[i].Tip;
                if (t != SatirTipi.SubGroup && t != SatirTipi.MainGroup) continue;

                decimal? sumCari = null, sumOnceki = null;

                for (int j = i + 1; j < n; j++)
                {
                    var jt = rows[j].Tip;

                    if (t == SatirTipi.SubGroup)
                    {
                        if (jt == SatirTipi.SubGroup || jt == SatirTipi.MainGroup || jt == SatirTipi.Total)
                            break;
                    }
                    else
                    {
                        if (jt == SatirTipi.MainGroup || jt == SatirTipi.Total) break;
                    }

                    if (jt == SatirTipi.Account)
                    {
                        if (cari[j].HasValue) sumCari = (sumCari ?? 0m) + cari[j]!.Value;
                        if (onceki[j].HasValue) sumOnceki = (sumOnceki ?? 0m) + onceki[j]!.Value;
                    }
                }

                cari[i] = sumCari;
                onceki[i] = sumOnceki;
            }

            // 3) Total satırları: önceki Total'dan sonraki Account'ların toplamı
            for (int i = 0; i < n; i++)
            {
                if (rows[i].Tip != SatirTipi.Total) continue;

                int start = 0;
                for (int k = i - 1; k >= 0; k--)
                {
                    if (rows[k].Tip == SatirTipi.Total) { start = k + 1; break; }
                }

                decimal? sumCari = null, sumOnceki = null;
                for (int j = start; j < i; j++)
                {
                    if (rows[j].Tip != SatirTipi.Account) continue;
                    if (cari[j].HasValue) sumCari = (sumCari ?? 0m) + cari[j]!.Value;
                    if (onceki[j].HasValue) sumOnceki = (sumOnceki ?? 0m) + onceki[j]!.Value;
                }

                cari[i] = sumCari;
                onceki[i] = sumOnceki;
            }

            // 4) Vergi paneli ile bağlantı:
            //    690 = Ticari Kar (Total — mevcut hesaplama, bilanço öncesi kar)
            //    691 = Hesaplanan Vergi Karşılığı (-) → vergi panelinden gelir, NEGATİF olarak yazılır
            //    692 = Dönem Net Karı = 690 + 691
            int idx690 = -1, idx691 = -1, idx692 = -1;
            for (int i = 0; i < n; i++)
            {
                if (rows[i].Kod == "690") idx690 = i;
                else if (rows[i].Kod == "691") idx691 = i;
                else if (rows[i].Kod == "692") idx692 = i;
            }

            if (idx691 >= 0)
                cari[idx691] = hesaplananVergi691Cari.HasValue ? -1m * hesaplananVergi691Cari.Value : 0m;

            if (idx692 >= 0 && idx690 >= 0)
            {
                var v690 = cari[idx690];
                var v691 = idx691 >= 0 ? cari[idx691] : 0m;
                if (v690.HasValue || v691.HasValue)
                    cari[idx692] = (v690 ?? 0m) + (v691 ?? 0m);
            }

            var result = new List<ComputedRow>(n);
            for (int i = 0; i < n; i++)
            {
                result.Add(new ComputedRow
                {
                    Source = rows[i],
                    Cari = cari[i],
                    OncekiCari = onceki[i],
                    KaynakKod = kaynakKod[i],
                    YansitmaKod = yansitmaKod[i]
                });
            }
            return result;
        }

        /// <summary>
        /// 690 (Ticari Kar / Dönem Karı) satırının değerlerini döner.
        /// </summary>
        public static (decimal? Onceki, decimal? Cari) GetDonemKari(IEnumerable<ComputedRow> computed)
        {
            var match = computed.FirstOrDefault(r => r.Source.Kod == "690");
            return (match?.OncekiCari, match?.Cari);
        }
    }
}
