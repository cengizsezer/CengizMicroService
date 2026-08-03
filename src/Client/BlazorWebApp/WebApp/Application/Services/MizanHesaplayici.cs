using WebApp.Domain.Models.FirmaKontrol;

namespace WebApp.Application.Services
{
    // Mizan gruplama/toplama mantığının TEK kaynağı. Bilanço (Aktif/Pasif) doğrudan,
    // gelir tablosu ise GelirTablosuCalculator üzerinden buraya bağlanır. Böylece
    // "I -DÖNEN VARLIKLAR" gibi bir grup toplamı Bilanço, Bilanço Özet, Dikey Yüzdeler
    // ve Finansal Oranlar sekmelerinde AYNI çıkar — ikinci bir toplama mantığı yoktur.
    //
    // Kurallar:
    //  1) Account — ham bakiye (çözücü verilmişse ondan) MaliTabloIsareti ile bölümün
    //     sunum yönüne çevrilir. Ham mizan bakiyesi borç-pozitiftir (borç − alacak);
    //     Aktif olduğu gibi kalır, Pasif ve gelir tablosu ters çevrilir. Grup/Total
    //     toplamları bu işaretli değerler üzerinden alınır; Dikey % pay ve payda
    //     birlikte döndüğü için değişmez.
    //  2) SubGroup — kendisinden sonraki ilk SubGroup/MainGroup/Total'a kadarki hesaplar.
    //  3) MainGroup — bir sonraki Total veya GERÇEK üst düzey MainGroup'a kadar. Hesap
    //     planında bazı alt bölüm başlıkları (H-Diğer Dönen Varlıklar=190-199, C-Mali
    //     Duran Varlıklar, Pasif'te I-Diğer Kısa Vadeli Yabancı Kaynaklar ...) yanlışlıkla
    //     MainGroup etiketlidir; bunlarda kesilmez, hesapları üst gruba emilir.
    //  4) Total — KÜMÜLATİF: tablonun başından o satıra kadarki tüm hesaplar. Bilanço'da
    //     tek Total (genel toplam) bulunduğundan sonuç değişmez; gelir tablosunda ara
    //     toplamların birikmesini sağlar (net satışlar → brüt kâr → ... → 690).
    public static class MizanHesaplayici
    {
        public record ComputedRow(MizanSatir Source, decimal? Onceki, decimal? Cari);

        /// <summary>
        /// Bir Account satırının HAM (işaretsiz) dönem değerlerini çözer. Varsayılan
        /// davranış satırın kendi bakiyesidir; gelir tablosu 6'lı↔7'li yansıtma
        /// fallback'ini bu kanaldan verir.
        /// </summary>
        public delegate (decimal? Onceki, decimal? Cari) HamDegerCozucu(MizanSatir satir);

        /// <param name="hamDeger">
        /// Account ham değer çözücüsü. null ise satırın OncekiDonem/CariDonem'i kullanılır.
        /// </param>
        /// <param name="toplamaGirmeyenKodlar">
        /// Kümülatif Total toplamına DAHİL EDİLMEYECEK hesap kodları (gelir tablosunda
        /// 690/691/692 ve 7'li yansıtma hesapları). null ise hepsi toplanır.
        /// </param>
        public static List<ComputedRow> Compute(
            List<MizanSatir> rows,
            MaliTabloBolumu bolum,
            HamDegerCozucu? hamDeger = null,
            IReadOnlySet<string>? toplamaGirmeyenKodlar = null)
        {
            var oncekiVals = new decimal?[rows.Count];
            var cariVals = new decimal?[rows.Count];

            // 1) Account değerleri — ham değer çözülür, sonra bölümün sunum işaretine çevrilir.
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Tip != SatirTipi.Account) continue;

                var (hamOnceki, hamCari) = hamDeger is null
                    ? (rows[i].OncekiDonem, rows[i].CariDonem)
                    : hamDeger(rows[i]);

                oncekiVals[i] = MaliTabloIsareti.Uygula(hamOnceki, bolum);
                cariVals[i] = MaliTabloIsareti.Uygula(hamCari, bolum);
            }

            // 2) SubGroup ve MainGroup toplamları
            for (int i = 0; i < rows.Count; i++)
            {
                var t = rows[i].Tip;
                if (t != SatirTipi.SubGroup && t != SatirTipi.MainGroup) continue;

                decimal? sumOnceki = null;
                decimal? sumCari = null;

                for (int j = i + 1; j < rows.Count; j++)
                {
                    var jt = rows[j].Tip;

                    if (t == SatirTipi.SubGroup)
                    {
                        if (jt == SatirTipi.SubGroup || jt == SatirTipi.MainGroup || jt == SatirTipi.Total)
                            break;
                    }
                    else // MainGroup
                    {
                        // Sadece Total'da veya gerçek üst düzey (tamamı büyük harf) MainGroup'ta kes.
                        if (jt == SatirTipi.Total ||
                            (jt == SatirTipi.MainGroup && UstDuzeyMainGroupMu(rows[j].Ad)))
                            break;
                    }

                    if (jt == SatirTipi.Account)
                    {
                        if (oncekiVals[j].HasValue)
                            sumOnceki = (sumOnceki ?? 0m) + oncekiVals[j]!.Value;
                        if (cariVals[j].HasValue)
                            sumCari = (sumCari ?? 0m) + cariVals[j]!.Value;
                    }
                }

                oncekiVals[i] = sumOnceki;
                cariVals[i] = sumCari;
            }

            // 3) Total satırları: tablonun başından o satıra kadarki tüm Account'lar (kümülatif).
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Tip != SatirTipi.Total) continue;

                decimal? sumOnceki = null;
                decimal? sumCari = null;

                for (int j = 0; j < i; j++)
                {
                    if (rows[j].Tip != SatirTipi.Account) continue;
                    if (toplamaGirmeyenKodlar?.Contains(rows[j].Kod ?? string.Empty) == true) continue;

                    if (oncekiVals[j].HasValue)
                        sumOnceki = (sumOnceki ?? 0m) + oncekiVals[j]!.Value;
                    if (cariVals[j].HasValue)
                        sumCari = (sumCari ?? 0m) + cariVals[j]!.Value;
                }

                oncekiVals[i] = sumOnceki;
                cariVals[i] = sumCari;
            }

            var result = new List<ComputedRow>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                result.Add(new ComputedRow(rows[i], oncekiVals[i], cariVals[i]));
            }
            return result;
        }

        // Gerçek üst düzey bilanço başlıkları TAMAMEN BÜYÜK HARF (örn "I -DÖNEN VARLIKLAR",
        // "III-KISA VADELİ YABANCI KAYNAKLAR", "V-ÖZ KAYNAKLAR").
        // Yanlışlıkla MainGroup etiketli alt bölümler en az bir küçük harf içerir
        // (örn "H-Diğer Dönen Varlıklar", "I-Diğer Kısa Vadeli Yabancı Kaynaklar").
        // Roma rakamı önekine göre ayırmak hatalı olurdu; büyük/küçük harf ölçütü kullanılır.
        public static bool UstDuzeyMainGroupMu(string? ad) =>
            !string.IsNullOrEmpty(ad) && ad.Any(char.IsLetter) && !ad.Any(char.IsLower);
    }
}
