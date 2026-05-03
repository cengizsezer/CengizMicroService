using WebApp.Domain.Models.FirmaKontrol;

namespace WebApp.Application.Services
{
    public static class GelirTablosuCalculator
    {
        // Bilgi amaçlı: 6'lı gider kodu -> mukabil 7'li yansıtma kodu eşleşmesi.
        // Artık toplama yapılmıyor; muhasebeci 740/750/760/770/780'i ayrı görüp
        // kapanış kararını kendi verecek (mizan tab'ında ve gelir tablosunda
        // satır olarak listeleniyor).
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
            public decimal? OncekiCari { get; set; }
            public decimal? Cari { get; set; }
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

            // 1) Account satırları: SADECE kendi mizan bakiyesi.
            //    Yansıtma toplamı ARTIK YAPILMIYOR — 740/750/760/770/780 ayrı satır
            //    olarak gösterilir, ana grup toplamına dahil edilmez.
            //    Kaynak kod tüm tipler için (kod doluysa) gösterilir — 690/691/692 gibi
            //    başlık/total satırlarında da Hesap Kodu sütununun beslenmesi için.
            for (int i = 0; i < n; i++)
            {
                var r = rows[i];

                kaynakKod[i] = string.IsNullOrWhiteSpace(r.Kod) ? null : r.Kod;

                // Bilgi amaçlı yansıtma kodu (UI'da ipucu olarak gösteriliyor)
                if (r.Tip == SatirTipi.Account && !string.IsNullOrEmpty(r.Kod) &&
                    YansitmaMap.TryGetValue(r.Kod, out var yKod))
                {
                    yansitmaKod[i] = yKod;
                }

                if (r.Tip != SatirTipi.Account) continue;

                cari[i] = r.CariDonem;
                onceki[i] = r.OncekiDonem;
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

            // 4) 690 / 691 / 692 — Mizan öncelikli, formül fallback.
            //    Muhasebeci kapanış sonrası bu satırları mizana yazdıysa
            //    "doğru kabul" edilir ve formülün önüne geçer. Mizanda boşsa
            //    690 = Total scan sonucu (mevcut), 691 = vergi panelinden -1 ile,
            //    692 = 690 + 691 olarak hesaplanır.
            int idx690 = -1, idx691 = -1, idx692 = -1;
            for (int i = 0; i < n; i++)
            {
                if (rows[i].Kod == "690") idx690 = i;
                else if (rows[i].Kod == "691") idx691 = i;
                else if (rows[i].Kod == "692") idx692 = i;
            }

            // 690 — mizanda varsa override; yoksa Total scan sonucu kalır
            if (idx690 >= 0)
            {
                var m690c = RawValue(rawCari, "690");
                if (m690c.HasValue) cari[idx690] = m690c;

                var m690o = RawValue(rawOnceki, "690");
                if (m690o.HasValue) onceki[idx690] = m690o;
            }

            // 691 — mizanda varsa override; yoksa vergi panelinden -1 ile
            if (idx691 >= 0)
            {
                var m691c = RawValue(rawCari, "691");
                if (m691c.HasValue)
                    cari[idx691] = m691c;
                else
                    cari[idx691] = hesaplananVergi691Cari.HasValue ? -1m * hesaplananVergi691Cari.Value : 0m;

                var m691o = RawValue(rawOnceki, "691");
                if (m691o.HasValue) onceki[idx691] = m691o;
            }

            // 692 — mizanda varsa override; yoksa 690 + 691
            if (idx692 >= 0)
            {
                var m692c = RawValue(rawCari, "692");
                if (m692c.HasValue)
                {
                    cari[idx692] = m692c;
                }
                else if (idx690 >= 0)
                {
                    var v690 = cari[idx690];
                    var v691 = idx691 >= 0 ? cari[idx691] : 0m;
                    if (v690.HasValue || v691.HasValue)
                        cari[idx692] = (v690 ?? 0m) + (v691 ?? 0m);
                }

                var m692o = RawValue(rawOnceki, "692");
                if (m692o.HasValue)
                {
                    onceki[idx692] = m692o;
                }
                else if (idx690 >= 0)
                {
                    var o690 = onceki[idx690];
                    var o691 = idx691 >= 0 ? onceki[idx691] : 0m;
                    if (o690.HasValue || o691.HasValue)
                        onceki[idx692] = (o690 ?? 0m) + (o691 ?? 0m);
                }
            }

            static decimal? RawValue(IReadOnlyDictionary<string, decimal?> raw, string kod) =>
                raw.TryGetValue(kod, out var v) ? v : null;

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
