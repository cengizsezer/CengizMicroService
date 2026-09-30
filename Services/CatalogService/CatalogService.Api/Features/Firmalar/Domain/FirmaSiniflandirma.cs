namespace CatalogService.Api.Features.Firmalar.Domain
{
    /// <summary>Firmanın defter tutma usulü. <c>Belirsiz</c> = henüz seçilmedi, eksik sayılır.</summary>
    public enum DefterUsulu : byte
    {
        Belirsiz = 0,
        BilancoEsasi = 1,
        IsletmeHesabi = 2
    }

    /// <summary>Firmanın gelir üzerinden vergi türü. <c>Belirsiz</c> = eksik.</summary>
    public enum VergiTuru : byte
    {
        Belirsiz = 0,
        KurumlarVergisi = 1,
        GelirVergisi = 2
    }

    /// <summary>
    /// Hesap dönemi. Alan firmada <b>nullable</b> tutuluyor: seçilmemiş hesap dönemi
    /// "takvim yılı" varsayılarak doldurulursa eksik bilgi şeridi onu hiç yakalayamaz.
    /// </summary>
    public enum HesapDonemi : byte
    {
        TakvimYili = 1,
        OzelHesapDonemi = 2
    }

    /// <summary>
    /// Sınıflandırma alanının değeri nereden geldi. <c>Elle</c> olan alanın üzerine
    /// sonraki mükellefiyet okuması <b>yazmaz</b> — otomatik doldurma bir öneridir.
    /// </summary>
    public enum SiniflandirmaKaynagi : byte
    {
        Yok = 0,
        Otomatik = 1,
        Elle = 2
    }

    /// <summary>
    /// Mizan formatı seçenekleri — şimdilik sabit liste. İleride mizan format profili
    /// tablosuna bağlanacak; o gelene kadar alan bu listedeki metinlerden birini taşır.
    ///
    /// Şablon başlıkları geçici: ORKA ve Luca için format profili taslağındaki başlık
    /// adları, Mikro/Logo için genel mizan başlıkları. Profil tablosu gelince oradan
    /// üretilecek.
    /// </summary>
    public static class MizanFormatlari
    {
        public const string Belirsiz = "Belirsiz";

        public static readonly IReadOnlyList<string> Liste = new[]
        {
            "ORKA — döviz kolonlu",
            "ORKA — döviz kolonsuz",
            "Luca",
            "Mikro",
            "Logo",
            Belirsiz
        };

        /// <summary>Seçilmiş sayılır mı: boş ya da "Belirsiz" eksik demektir.</summary>
        public static bool Secili(string? format)
            => !string.IsNullOrWhiteSpace(format) && format != Belirsiz;

        public static bool Gecerli(string? format)
            => format is null || Liste.Contains(format);

        /// <summary>
        /// Boş şablonun başlık satırı. <b>Yalnız</b> gerçek çıktısı görülmüş programlar (ORKA'nın
        /// iki varyantı ve Luca); Mikro, Logo ve diğerleri için <c>null</c> — müşteriye
        /// uydurma şablon gönderilmez. Mizan format profilleri (Prompt 5) gelince şablon
        /// profilden üretilecek ve bu kısıt kalkacak.
        /// </summary>
        public static string[]? SablonBasliklari(string? format) => format switch
        {
            "ORKA — döviz kolonlu" => new[]
            {
                "Hesap Kodu", "Hesap Adı", "Borç", "Alacak", "Döviz Borç", "Döviz Alacak",
                "TL Borç Bakiye", "TL Alacak Bakiye"
            },
            "ORKA — döviz kolonsuz" => new[]
                { "Hesap Kodu", "Hesap Adı", "Borç", "Alacak", "Borç Bakiye", "Alacak Bakiye" },
            "Luca" => new[] { "HESAP KODU", "HESAP ADI", "BORÇ", "ALACAK", "BORÇ BAKİYESİ", "ALACAK BAKİYESİ" },
            _ => null
        };

        public const string SablonYokMesaji = "Bu programın gerçek mizan çıktısı henüz görülmedi; şablon yok.";
    }
}
