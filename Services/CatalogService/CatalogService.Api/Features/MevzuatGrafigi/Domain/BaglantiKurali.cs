namespace CatalogService.Api.Features.MevzuatGrafigi.Domain
{
    /// <summary>
    /// Bağlantı sırası: Firma → İşlem veya doğrudan İstisna / İndirim → Kanun / Şart.
    /// Firma → teşvik doğrudan bağlantısı "firma bu teşvikten yararlanıyor" demektir; İşlem
    /// üzerinden ulaşılan teşvik yalnız "uygun olabilir"dir (önizleme bu ayrımı yapar).
    /// İstemcideki ALLOWED tablosunun sunucu karşılığı (ikisi aynı kalmalı); istemci aynı
    /// kuralı ekranda uygular ama son söz burada.
    /// </summary>
    public static class BaglantiKurali
    {
        public const string SiraMetni = "Firma → İşlem veya doğrudan İstisna / İndirim → Kanun / Şart";

        private static readonly IReadOnlyDictionary<MevzuatKartTuru, MevzuatKartTuru[]> Izinli =
            new Dictionary<MevzuatKartTuru, MevzuatKartTuru[]>
            {
                [MevzuatKartTuru.Firma]   = new[] { MevzuatKartTuru.Islem, MevzuatKartTuru.Istisna, MevzuatKartTuru.Indirim },
                [MevzuatKartTuru.Islem]   = new[] { MevzuatKartTuru.Istisna, MevzuatKartTuru.Indirim },
                [MevzuatKartTuru.Istisna] = new[] { MevzuatKartTuru.Kanun, MevzuatKartTuru.Sart },
                [MevzuatKartTuru.Indirim] = new[] { MevzuatKartTuru.Kanun, MevzuatKartTuru.Sart },
                [MevzuatKartTuru.Kanun]   = Array.Empty<MevzuatKartTuru>(),
                [MevzuatKartTuru.Sart]    = Array.Empty<MevzuatKartTuru>(),
            };

        public static bool IzinliMi(MevzuatKartTuru kaynak, MevzuatKartTuru hedef)
            => Izinli.TryGetValue(kaynak, out var hedefler) && hedefler.Contains(hedef);

        public static string RetMesaji(MevzuatKartTuru kaynak, MevzuatKartTuru hedef)
            => $"{Etiket(kaynak)} kartı {Etiket(hedef)} kartına bağlanamaz. Sıra: {SiraMetni}.";

        public static string Etiket(MevzuatKartTuru tur) => tur switch
        {
            MevzuatKartTuru.Firma   => "Firma",
            MevzuatKartTuru.Islem   => "İşlem",
            MevzuatKartTuru.Istisna => "İstisna",
            MevzuatKartTuru.Indirim => "İndirim",
            MevzuatKartTuru.Kanun   => "Kanun",
            MevzuatKartTuru.Sart    => "Şart",
            _                       => tur.ToString()
        };
    }

    /// <summary>
    /// Dışlama yalnız iki teşvik arasında kurulur: İstisna-İstisna, İstisna-İndirim, İndirim-İndirim
    /// ("iki teşvik aynı matrah için birlikte kullanılamaz"). Tür değişikliğinde de geçerli:
    /// dışlaması olan kart teşvik dışı bir türe çevrilemez.
    /// </summary>
    public static class DislamaKurali
    {
        public static bool TesvikMi(MevzuatKartTuru tur)
            => tur is MevzuatKartTuru.Istisna or MevzuatKartTuru.Indirim;

        public static string RetMesaji(MevzuatKartTuru a, MevzuatKartTuru b)
            => $"Dışlama yalnız İstisna ve İndirim kartları arasında kurulabilir; " +
               $"{BaglantiKurali.Etiket(a)} - {BaglantiKurali.Etiket(b)} olmaz.";
    }
}
