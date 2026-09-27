using System.Globalization;

namespace WebApp.Domain.Models.FirmaKontrol
{
    /// <summary>
    /// Mizandaki tek bir hesap satırı — ana hesap ("600") ya da alt kırılım ("600 1 21").
    ///
    /// DİKKAT: Bu düğümler HİÇBİR toplama girmez. Bilanço, Gelir Tablosu, Vergi Hesaplaması,
    /// Dikey Yüzdeler ve Finansal Oranlar yalnızca üç haneli ana hesap kodlarından hesaplanır.
    /// Sebebi: 600 = 600 1 = 600 1 20 + 600 1 21 — aynı paranın farklı kırılımları; toplama
    /// karıştırılırsa ciro katlanır. Bu yapı yalnızca hesap seçici / etiketleme içindir.
    /// </summary>
    public class HesapDugumu
    {
        /// <summary>Mizandaki hâliyle kod: "600", "600 1", "120 1 A08".</summary>
        public string Kod { get; set; } = string.Empty;

        public string Ad { get; set; } = string.Empty;

        /// <summary>Koddaki parça sayısı: "600" → 1, "600 1" → 2, "600 1 21" → 3.</summary>
        public int Seviye { get; set; }

        /// <summary>Son parçası atılmış kod; seviye 1 ise null.</summary>
        public string? UstKod { get; set; }

        /// <summary>İlk parça — üç haneli ana hesap kodu ("600").</summary>
        public string AnaHesapKodu { get; set; } = string.Empty;

        public decimal BorcBakiye { get; set; }
        public decimal AlacakBakiye { get; set; }

        /// <summary>Ham (borç−alacak) bakiye. Sunum yönü MaliTabloIsareti ile uygulanır.</summary>
        public decimal Bakiye { get; set; }

        /// <summary>
        /// Koddan düğüm üretir. Seviye ve üst kod KODDAN türetilir; sabit karakter genişliği
        /// varsayılmaz. Ayırıcı boşluktur, ardışık boşluklar tek ayırıcı sayılır.
        /// Kod parçaları harf içerebilir ("120 1 A08", "770 6 255").
        /// Kodun ilk parçası üç haneli sayı değilse null döner.
        /// </summary>
        public static HesapDugumu? Olustur(string? kod, string? ad, decimal borc, decimal alacak, decimal? bakiye)
        {
            var parcalar = Parcala(kod);
            if (parcalar.Length == 0) return null;
            if (!AnaHesapKoduMu(parcalar[0])) return null;

            var normalKod = string.Join(' ', parcalar);

            return new HesapDugumu
            {
                Kod = normalKod,
                Ad = (ad ?? string.Empty).Trim(),
                Seviye = parcalar.Length,
                UstKod = parcalar.Length > 1 ? string.Join(' ', parcalar[..^1]) : null,
                AnaHesapKodu = parcalar[0],
                BorcBakiye = borc,
                AlacakBakiye = alacak,
                Bakiye = bakiye ?? (borc - alacak),
            };
        }

        /// <summary>Kodu boşluklara göre parçalar; ardışık boşluklar tek ayırıcıdır.</summary>
        public static string[] Parcala(string? kod) =>
            string.IsNullOrWhiteSpace(kod)
                ? Array.Empty<string>()
                : kod.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        /// <summary>Tam üç haneli numerik kod mu ("600" evet, "60" / "6000" / "ABC" hayır).</summary>
        public static bool AnaHesapKoduMu(string? parca) =>
            parca is { Length: 3 } && parca.All(char.IsAsciiDigit);

        /// <summary>
        /// TDHP ana grubu — kodun ilk hanesi. 1-2 varlık, 3-5 kaynak, 6-7 gelir tablosu,
        /// 8-9 nazım/maliyet. Gruplama başlıkları ve sunum yönü bundan türetilir.
        /// </summary>
        public int AnaGrup =>
            AnaHesapKodu.Length > 0 && char.IsAsciiDigit(AnaHesapKodu[0])
                ? AnaHesapKodu[0] - '0'
                : 0;

        public override string ToString() =>
            $"{Kod} · {Ad} · {Bakiye.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"))}";
    }
}
