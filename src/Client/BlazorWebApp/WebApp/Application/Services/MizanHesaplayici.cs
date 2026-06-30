using WebApp.Domain.Models.FirmaKontrol;

namespace WebApp.Application.Services
{
    // Detaylı Bilanço (MizanGrid) gruplama/toplama mantığının TEK kaynağı.
    // Hem detaylı Bilanço hem Bilanço Özet bu yardımcıyı kullanır → MainGroup
    // sınırı (örn. I-Dönen Varlıklar) her iki yerde de AYNI çıkar.
    //
    // Sınır kuralı: bir MainGroup toplamı, kendisinden sonraki TÜM SubGroup/Account
    // satırlarını bir sonraki GERÇEK üst düzey MainGroup (veya Total) başlayana kadar
    // kapsar. Hesap planında bazı alt bölüm başlıkları (H-Diğer Dönen Varlıklar=190-199,
    // C-Mali Duran Varlıklar, Pasif'te I-Diğer Kısa Vadeli Yabancı Kaynaklar ...) yanlışlıkla
    // MainGroup etiketlidir; bunlarda kesilmez, account'ları üst gruba emilir.
    //
    // İşaret/parse mantığına dokunulmaz: Account değerleri olduğu gibi (CariDonem/OncekiDonem)
    // alınır; Pasif kalemleri eskisi gibi negatif kalır.
    public static class MizanHesaplayici
    {
        public record ComputedRow(MizanSatir Source, decimal? Onceki, decimal? Cari);

        public static List<ComputedRow> Compute(List<MizanSatir> rows)
        {
            var oncekiVals = new decimal?[rows.Count];
            var cariVals = new decimal?[rows.Count];

            // Account değerleri
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Tip == SatirTipi.Account)
                {
                    oncekiVals[i] = rows[i].OncekiDonem;
                    cariVals[i] = rows[i].CariDonem;
                }
            }

            // SubGroup ve MainGroup toplamları
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
                        if (rows[j].OncekiDonem.HasValue)
                            sumOnceki = (sumOnceki ?? 0m) + rows[j].OncekiDonem!.Value;
                        if (rows[j].CariDonem.HasValue)
                            sumCari = (sumCari ?? 0m) + rows[j].CariDonem!.Value;
                    }
                }

                oncekiVals[i] = sumOnceki;
                cariVals[i] = sumCari;
            }

            // Total satırları: önceki Total'dan sonraki tüm Account'ların toplamı
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Tip != SatirTipi.Total) continue;

                int start = 0;
                for (int k = i - 1; k >= 0; k--)
                {
                    if (rows[k].Tip == SatirTipi.Total)
                    {
                        start = k + 1;
                        break;
                    }
                }

                decimal? sumOnceki = null;
                decimal? sumCari = null;

                for (int j = start; j < i; j++)
                {
                    if (rows[j].Tip != SatirTipi.Account) continue;

                    if (rows[j].OncekiDonem.HasValue)
                        sumOnceki = (sumOnceki ?? 0m) + rows[j].OncekiDonem!.Value;
                    if (rows[j].CariDonem.HasValue)
                        sumCari = (sumCari ?? 0m) + rows[j].CariDonem!.Value;
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
