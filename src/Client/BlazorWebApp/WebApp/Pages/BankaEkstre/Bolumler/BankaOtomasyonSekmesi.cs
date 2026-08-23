namespace WebApp.Pages.BankaEkstre.Bolumler
{
    /// <summary>Firma içi iki sekme; <see cref="FirmaBasligi"/> hangisinin açık olduğunu buradan bilir.</summary>
    public enum BankaOtomasyonSekmesi
    {
        /// <summary>Günlük iş: banka sekmeleri, hesap kartları, ekstre yükleme, onay ekranı.</summary>
        Aktar,

        /// <summary>Kurulum: hesap planı, banka hesapları ve firma geneli tanımlar.</summary>
        Tanimlar
    }
}
