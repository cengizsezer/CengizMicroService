namespace WebApp.Shared.Dto.FirmaKontrol
{
    /// <summary>
    /// Mizan notunun türü. DTO'larda int taşınır (FirmaKontrolMadde.Status ile aynı
    /// desen); bu sınıf UI'da sihirli sayı kullanılmasın diye adlandırır.
    ///
    /// Tür ≠ kapsam: kalıcı/dönem ayrımı <see cref="MizanNotuDto.DonemYili"/>'dir.
    /// </summary>
    public static class MizanNotTuru
    {
        /// <summary>Bakiyenin gerekçesini anlatan not — bir iş beklenmez.</summary>
        public const int Aciklama = 0;

        /// <summary>
        /// Yapılacak bir düzeltmeyi kaydeden not. Bakiye hiç değişmemişse
        /// "iş yapılmamış" sinyali gösterilir.
        /// </summary>
        public const int Duzeltilecek = 1;

        public static string Etiket(int notTuru) =>
            notTuru == Duzeltilecek ? "Düzeltilecek" : "Açıklama";
    }
}
