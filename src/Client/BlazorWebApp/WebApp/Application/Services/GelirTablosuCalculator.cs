using WebApp.Domain.Models.FirmaKontrol;

namespace WebApp.Application.Services
{
    /// <summary>
    /// Gelir tablosu sunum değerlerini hesaplar.
    ///
    /// Gruplama/toplama/işaret mantığı BURADA DEĞİL, tek kaynak olan
    /// <see cref="MizanHesaplayici"/> içindedir; bu sınıf yalnızca gelir tablosuna
    /// özgü katmanları ekler:
    ///  • 6'lı↔7'li yansıtma fallback'i (ham değer çözücüsü olarak),
    ///  • kümülatif ara toplama girmeyecek kodlar (690/691/692 + yansıtma),
    ///  • 690/691/692 satırlarının mizan öncelikli / formül fallback'li doldurulması,
    ///  • UI'ın kullandığı Kaynak Hesap ve Yansıtma kodu sütunları.
    ///
    /// Bilanço (Aktif/Pasif) doğrudan <see cref="MizanHesaplayici"/> ile hesaplanır —
    /// böylece grup toplamları tüm sekmelerde aynı çıkar.
    /// </summary>
    public static class GelirTablosuCalculator
    {
        // 6'lı maliyet/gider kodu -> mukabil 7'li yansıtma kodu eşleşmesi.
        // İki amaçla kullanılır:
        //  (1) Bilgi: 740/750/760/770/780 ayrı "YANSITMA" satırı olarak gösterilir.
        //  (2) Fallback (tek kaynak / çifte sayım yok): 6'lı hesap mizanda BOŞ ise
        //      tutar 7'li yansıtma hesabında duruyordur → o değer 6'lıya taşınır.
        //      6'lı DOLU ise 7'liye dokunulmaz. 7'li satırlar ana grup/Total
        //      toplamlarına hiçbir zaman dahil edilmez, dolayısıyla çifte sayım olmaz.
        public static readonly IReadOnlyDictionary<string, string> YansitmaMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["622"] = "740",
                ["630"] = "750",
                ["631"] = "760",
                ["632"] = "770",
                ["660"] = "780"
            };

        // Kümülatif ara toplamlara (Total satırları) GİRMEYEN hesap kodları:
        //  • 690/691/692 — kendileri sonuç satırıdır, alt kalemlerden türetilir.
        //  • 740/750/760/770/780 — yansıtma; tutarları zaten 6'lı hesaba taşındı,
        //    tekrar toplanırsa çifte sayım olur.
        private static readonly HashSet<string> ToplamaGirmeyenKodlar =
            new(new[] { "690", "691", "692", "740", "750", "760", "770", "780" },
                StringComparer.OrdinalIgnoreCase);

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
            var kaynakKod = new string?[n];
            var yansitmaKod = new string?[n];

            // Ham (borç-pozitif) mizan bakiyesini gelir tablosu sunum işaretine çevirir:
            // gelir alacak bakiyeli → artı, gider/maliyet borç bakiyeli → eksi.
            // Ayrıntı: MaliTabloIsareti. (Account satırlarını MizanHesaplayici çevirir;
            // bu yerel yardımcı yalnızca aşağıdaki 690/691/692 override'ları içindir.)
            static decimal? Isaretle(decimal? ham) =>
                MaliTabloIsareti.Uygula(ham, MaliTabloBolumu.GelirTablosu);

            // Kaynak kod tüm tipler için (kod doluysa) gösterilir — 690/691/692 gibi
            // başlık/total satırlarında da Hesap Kodu sütununun beslenmesi için.
            // Yansıtma kodu yalnızca 6'lı Account satırlarında bilgi amaçlı (UI ipucu).
            for (int i = 0; i < n; i++)
            {
                var r = rows[i];
                kaynakKod[i] = string.IsNullOrWhiteSpace(r.Kod) ? null : r.Kod;

                if (r.Tip == SatirTipi.Account && !string.IsNullOrEmpty(r.Kod) &&
                    YansitmaMap.TryGetValue(r.Kod, out var mapped))
                    yansitmaKod[i] = mapped;
            }

            // Gruplama / toplama / işaret: TEK KAYNAK — MizanHesaplayici.
            // Buraya yalnızca gelir tablosuna özgü iki girdi verilir: ham değer çözücüsü
            // (yansıtma fallback'i) ve kümülatif ara toplama girmeyecek kodlar.
            var hesaplanan = MizanHesaplayici.Compute(
                rows,
                MaliTabloBolumu.GelirTablosu,
                YansitmaliHamDeger,
                ToplamaGirmeyenKodlar);

            var cari = new decimal?[n];
            var onceki = new decimal?[n];
            for (int i = 0; i < n; i++)
            {
                cari[i] = hesaplanan[i].Cari;
                onceki[i] = hesaplanan[i].Onceki;
            }

            // 6'lı maliyet/gider hesabı (620/622/630/631/632/660...) mizanda boşsa, tutar
            // mukabil 7'li hesapta (740/750/760/770/780) duruyordur; o değeri 6'lıya
            // taşırız (6'lı doluysa dokunmayız → çifte sayım yok). 7'li satırlar ayrı
            // "YANSITMA" alt grubunda kalır ve ToplamaGirmeyenKodlar sayesinde kümülatif
            // ara toplamlara dahil edilmez; tutar yalnızca BİR kez sayılır.
            (decimal? Onceki, decimal? Cari) YansitmaliHamDeger(MizanSatir s)
            {
                var c = s.CariDonem;
                var o = s.OncekiDonem;

                if (!string.IsNullOrEmpty(s.Kod) && YansitmaMap.TryGetValue(s.Kod, out var yKod))
                {
                    if (!c.HasValue) c = RawValue(rawCari, yKod);
                    if (!o.HasValue) o = RawValue(rawOnceki, yKod);
                }

                return (o, c);
            }

            // 690 / 691 / 692 — Mizan öncelikli, formül fallback.
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

            // 690 — mizanda varsa override; yoksa kümülatif Total sonucu kalır.
            // (Kâr edilmişse 690 alacak bakiyelidir → işaretlenince artı çıkar.)
            if (idx690 >= 0)
            {
                var m690c = RawValue(rawCari, "690");
                if (m690c.HasValue) cari[idx690] = Isaretle(m690c);

                var m690o = RawValue(rawOnceki, "690");
                if (m690o.HasValue) onceki[idx690] = Isaretle(m690o);
            }

            // 691 — mizanda varsa override; yoksa vergi panelinden -1 ile.
            // hesaplananVergi691Cari zaten pozitif vergi tutarıdır (sunum işareti
            // taşımaz); indirim kalemi olduğu için -1 ile eksiye çevrilir.
            if (idx691 >= 0)
            {
                var m691c = RawValue(rawCari, "691");
                if (m691c.HasValue)
                    cari[idx691] = Isaretle(m691c);
                else
                    cari[idx691] = hesaplananVergi691Cari.HasValue ? -1m * hesaplananVergi691Cari.Value : 0m;

                var m691o = RawValue(rawOnceki, "691");
                if (m691o.HasValue) onceki[idx691] = Isaretle(m691o);
            }

            // 692 — mizanda varsa override; yoksa 690 + 691
            if (idx692 >= 0)
            {
                var m692c = RawValue(rawCari, "692");
                if (m692c.HasValue)
                {
                    cari[idx692] = Isaretle(m692c);
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
                    onceki[idx692] = Isaretle(m692o);
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
