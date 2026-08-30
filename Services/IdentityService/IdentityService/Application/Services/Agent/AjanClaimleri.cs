namespace IdentityService.Application.Services.Agent
{
    /// <summary>
    /// Ajan token'ını kullanıcı token'ından ayıran claim'ler.
    ///
    /// <b>Neden iki claim:</b> <c>typ</c> okunabilir işaret, ama JwtBearer
    /// gelen kısa claim adlarının bir kısmını uzun URI'lere çeviriyor ve bu
    /// eşleme sürüm sürüm değişebiliyor. Kararın dayandığı claim bu yüzden
    /// <c>ajan_id</c>: eşleme tablosunda yeri olmayan, bize ait bir ad. Bir
    /// kullanıcı token'ında hiç bulunmuyor.
    ///
    /// CatalogService tarafında aynı sabitler <c>AjanKimligi</c> içinde duruyor;
    /// iki servis arasında paylaşılan bir kütüphane yok, sözleşme bu dosyalarda.
    /// </summary>
    public static class AjanClaimleri
    {
        public const string Tip = "typ";
        public const string AjanTipi = "agent";
        public const string AjanId = "ajan_id";
        public const string AjanAdi = "ajan_adi";
    }
}
