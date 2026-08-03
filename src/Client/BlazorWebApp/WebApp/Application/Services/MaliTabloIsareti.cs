namespace WebApp.Application.Services
{
    /// <summary>
    /// Mali tablonun hangi bölümü hesaplanıyor. Ham mizan bakiyesinin sunum
    /// işaretine nasıl çevrileceğini belirler.
    /// </summary>
    public enum MaliTabloBolumu
    {
        Aktif,
        Pasif,
        GelirTablosu
    }

    /// <summary>
    /// Ham mizan bakiyesi (ExcelMizanParser) BORÇ-POZİTİF konvansiyondadır:
    /// <c>bakiye = borç − alacak</c>. Mali tabloda ise her bölümün kendi doğal
    /// yönü vardır ve tutarlar o yöne göre gösterilir:
    ///
    ///   • Aktif        : varlıklar borç bakiyeli → ham değer olduğu gibi (+1).
    ///                    Kontra hesaplar (257 Birikmiş Amortismanlar (-) gibi)
    ///                    alacak bakiyeli olduğundan kendiliğinden eksi çıkar.
    ///   • Pasif        : kaynaklar alacak bakiyeli → ters çevrilir (−1). Aksi
    ///                    halde tüm pasif ve "PASİF TOPLAMI" eksi görünürdü.
    ///                    501/591 gibi kontra hesaplar ters çevrilince eksi kalır.
    ///   • GelirTablosu : gelirler (60x, 64x, 67x) alacak bakiyeli → artı;
    ///                    gider/maliyet (61x, 62x, 63x, 65x, 66x, 68x) borç
    ///                    bakiyeli → eksi. Her ikisi de aynı (−1) çarpanıyla
    ///                    elde edilir; hesap koduna göre ayrı kural GEREKMEZ.
    ///                    (Sınıfına aykırı bakiyeli bir hesap — örn. net borç
    ///                    bakiyeli 600 — bu sayede doğru şekilde eksi çıkar.)
    ///
    /// Bu dönüşüm yalnızca SUNUM katmanındadır; MizanSatir/raw sözlükler ve
    /// kural motoru (MizanRuleContext) ham borç-pozitif değerlerle çalışmaya
    /// devam eder.
    /// </summary>
    public static class MaliTabloIsareti
    {
        public static int Yon(MaliTabloBolumu bolum) =>
            bolum == MaliTabloBolumu.Aktif ? 1 : -1;

        /// <summary>Ham bakiyeyi bölümün sunum işaretine çevirir. null → null.</summary>
        public static decimal? Uygula(decimal? hamBakiye, MaliTabloBolumu bolum) =>
            hamBakiye.HasValue ? hamBakiye.Value * Yon(bolum) : null;
    }
}
