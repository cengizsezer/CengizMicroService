namespace WebApp.Pages.BankaEkstre.Bolumler
{
    /// <summary>
    /// Modülün iki sekmesi; <see cref="FirmaBasligi"/> hangisinin açık olduğunu buradan
    /// bilir. "Firma içi" değil: firmaya girilmiyor, ikisi de tüm firmaları gösteriyor
    /// (KARARLAR §99).
    /// </summary>
    public enum BankaOtomasyonSekmesi
    {
        /// <summary>Günlük iş: banka sekmeleri, hesap kartları, ekstre yükleme, onay ekranı.</summary>
        Aktar,

        /// <summary>Kurulum: hesap planı, banka hesapları ve firma bazlı tanımlar.</summary>
        Tanimlar
    }
}
