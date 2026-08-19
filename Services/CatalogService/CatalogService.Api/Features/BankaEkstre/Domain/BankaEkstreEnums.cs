namespace CatalogService.Api.Features.BankaEkstre.Domain
{
    /// <summary>Banka hesabının tipi. Şimdilik yalnız vadesiz TL ekstresi ayrıştırılıyor.</summary>
    public enum HesapTipi : byte
    {
        Vadesiz = 1,
        Vadeli = 2
    }

    /// <summary>Ekstre satırının para yönü. Tutar her zaman pozitif saklanır, işaret bu alanda durur.</summary>
    public enum Yon : byte
    {
        Giren = 1,
        Cikan = 2
    }

    /// <summary>Ekstre yüklemesinin durumu.</summary>
    public enum YuklemeDurum : byte
    {
        Isleniyor = 0,
        Tamamlandi = 1,
        Hatali = 2
    }

    /// <summary>
    /// Satırın çözüm durumu. <see cref="DigerBankada"/> bankalar arası transferin karşı
    /// bacağı başka bankanın ekstresinde işlendiğinde kullanıcı tarafından elle işaretlenir
    /// ve satır dışa aktarımdan düşer.
    /// </summary>
    public enum SatirDurum : byte
    {
        Otomatik = 1,
        OnayBekliyor = 2,
        Onaylandi = 3,
        Cozulemedi = 4,
        DigerBankada = 5
    }

    /// <summary>
    /// Karşı hesabı hangi katmanın çözdüğü. Hata ayıklama için kritik: hangi katmanın
    /// yanıldığı bu alandan görülür, onay ekranında etiket olarak gösterilir.
    /// </summary>
    public enum KaynakKatman : byte
    {
        Yok = 0,
        Iban = 1,
        Vkn = 2,
        GecmisOnay = 3,
        BankaKayitDefteri = 4,
        SabitKural = 5,
        UnvanBenzerligi = 6,
        Kullanici = 7
    }

    /// <summary>Öğrenme kaydının anahtar tipi.</summary>
    public enum AnahtarTipi : byte
    {
        AciklamaHash = 1,
        Iban = 2,
        Vkn = 3
    }

    /// <summary>Şablon/desen/kural tablolarında işlem tipinin nasıl eşleşeceği.</summary>
    public enum EslesmeTuru : byte
    {
        /// <summary>Tam eşitlik (normalize edilmiş, büyük/küçük harf duyarsız).</summary>
        Tam = 1,
        /// <summary>İşlem tipi metni deseni içeriyorsa.</summary>
        Icerir = 2,
        /// <summary>Desen bir .NET regex'i.</summary>
        Regex = 3
    }
}
