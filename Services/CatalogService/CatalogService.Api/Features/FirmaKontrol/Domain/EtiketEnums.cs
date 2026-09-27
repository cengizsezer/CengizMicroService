namespace CatalogService.Api.Features.FirmaKontrol.Domain
{
    /// <summary>Etiket boyutunun kapsamı: tüm firmalarda mı geçerli, tek firmaya mı ait.</summary>
    public enum EtiketKapsami : byte
    {
        TumFirmalar = 0,
        BuFirma = 1
    }

    /// <summary>
    /// Bir kuralın hesap düğümüyle nasıl eşleştiği.
    /// KodDeseni: hesap koduna desen uygular ("600 1 21*").
    /// AdIcerir : hesap adında metin arar.
    /// TekSecim : tek bir hesap koduna elle atama.
    /// </summary>
    public enum EtiketEslesmeTipi : byte
    {
        KodDeseni = 0,
        AdIcerir = 1,
        TekSecim = 2
    }
}
