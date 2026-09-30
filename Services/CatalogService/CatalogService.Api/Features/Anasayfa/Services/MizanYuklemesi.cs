namespace CatalogService.Api.Features.Anasayfa.Services
{
    /// <summary>
    /// Bir firmanın bir (dönem, analiz yılı) mizan yüklemesi — anasayfanın okuduğu hâli.
    ///
    /// Firma Kontrol'de mizan <c>(FirmaId, Donem, Yil)</c> anahtarıyla saklanıyor; <c>Yil</c>
    /// analiz yılı, önceki dönem yüklemesi o yılın bir eksiğinin mizanı. Anasayfa "hangi
    /// yılın mizanı yüklü" diye sorduğu için <see cref="VeriYili"/> ayrıca taşınıyor.
    ///
    /// Kaynak iki tablo: ham satırlar (her yüklemede var) ve kırılım ağacı (özet alanları
    /// burada; ağaç özelliğinden önceki yüklemelerde yok). İkisi birleştirilmezse ağaçsız
    /// eski bir yükleme "mizan yok" görünürdü.
    /// </summary>
    public sealed record MizanYuklemesi(
        int FirmaId,
        int Donem,
        int AnalizYili,
        DateTime? YuklenmeZamani,
        int? SatirSayisi,
        int? SeviyeSayisi,
        decimal? BorcToplam,
        decimal? AlacakToplam)
    {
        public const int CariDonem = 1;

        public int VeriYili => Donem == CariDonem ? AnalizYili : AnalizYili - 1;
    }
}
