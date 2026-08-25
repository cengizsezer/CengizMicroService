namespace CatalogService.Api.Features.BankaEkstre.Services
{
    /// <summary>
    /// Öğrenme anahtarının yazılmaya değer olup olmadığı. Tek yerde durur: anahtarı hem
    /// onay ekranı (<see cref="HesapEslesmeService.OgrenAsync"/>) hem toplu içe aktarım
    /// (<see cref="OgrenilenEslesmeIceAktarimService"/>) yazıyor ve kural ikisinde de aynı.
    ///
    /// <b>Neden gerekli?</b> Öğrenme kaydı bir kez yazıldığında geçmiş onay katmanı onu
    /// güven 1.0 ile, satırı onaya bile düşürmeden uygular. Ölçümde iki bozuk kayıt vardı:
    /// <list type="bullet">
    /// <item>"SUN TEKSSANVE TICAS" → 120 S104. Banka metnindeki noktalı/kesik yazım
    /// ("SUN TEKS.SAN.VE TİC.A.Ş.") anlamsız bir çekirdeğe iniyor ve alakasız bir firmaya
    /// bağlanınca sonraki ekstrelerde sessizce tekrarlanıyordu (doğrusu 120 S22 Suntek
    /// Teknoloji).</item>
    /// <item>Tek kelimelik ya da çok kısa çekirdekler: kapsama araması yüzünden çok geniş,
    /// ilgisiz satırları da tek cariye bağlar.</item>
    /// </list>
    /// </summary>
    public static class AnahtarKalitesi
    {
        /// <summary>
        /// Unvandan türeyen anahtarın en az token sayısı ve en az toplam uzunluğu.
        /// Sınır iki kelimelik gerçek bir unvanı ("DAGI GIYIM" — 10) dışarıda bırakmayacak
        /// kadar düşük tutuldu; amaç tek kelimelik ve kırpık çekirdekleri elemek.
        /// </summary>
        public const int EnAzTokenSayisi = 2;
        public const int EnAzCekirdekUzunlugu = 10;

        /// <summary>Örtüşme sayılacak en kısa ortak token ve en kısa ortak önek.</summary>
        public const int EnKisaOrtakToken = 4;
        public const int EnKisaOrtakOnek = 5;

        /// <summary>
        /// Unvan değil, satırın niteliğinden türeyen anahtarlar. Bunların hesap adıyla
        /// örtüşmesi beklenmez ("ISLEM:HGS BAKIYE YUKLE" → 740, "KREDI:6501439328" →
        /// 300 1 0015 328) ve kalite kapısına girmezler.
        /// </summary>
        private static readonly string[] TeknikOnekler = { "ISLEM:", "KREDI:" };

        /// <summary>
        /// Kayıt yazılabilir mi? İki koşul: (1) çekirdek en az iki token ve boşluksuz en az
        /// 10 karakter, (2) çekirdek eşleştirilen hesabın adının çekirdeğiyle en az kısmen
        /// örtüşüyor (bkz. <see cref="Ortusuyor"/>).
        ///
        /// Hesap adı bilinmiyorsa (hesap planı yüklenmemiş) örtüşme sınanamaz ve kayıt
        /// yazılır: kapı, olmayan veriye dayanıp öğrenmeyi tamamen durdurmamalı.
        ///
        /// Kayıt yazılmasa da satır onaylanmış olur — kullanıcının kararı satıra işlenir,
        /// yalnız gelecekteki satırlara genellenmez.
        /// </summary>
        public static bool Uygun(string? cekirdek, string? hesapAdi) => Neden(cekirdek, hesapAdi) is null;

        /// <summary>
        /// Yazılamıyorsa insan okunur gerekçe, yazılabiliyorsa <c>null</c>. İçe aktarım
        /// satırı reddederken bu metni raporluyor: kullanıcı hangi satırın neden atlandığını
        /// görmeli.
        /// </summary>
        public static string? Neden(string? cekirdek, string? hesapAdi)
        {
            if (string.IsNullOrWhiteSpace(cekirdek)) return "Anahtar boş.";
            if (TeknikOnekler.Any(o => cekirdek.StartsWith(o, StringComparison.Ordinal))) return null;

            var tokenlar = cekirdek.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (tokenlar.Length < EnAzTokenSayisi || cekirdek.Trim().Length < EnAzCekirdekUzunlugu)
                return $"'{cekirdek}' öğrenme anahtarı olamayacak kadar zayıf: en az " +
                       $"{EnAzTokenSayisi} kelime ve {EnAzCekirdekUzunlugu} karakter gerekiyor.";

            var hesapCekirdek = Normalizasyon.UnvanCekirdek(hesapAdi);
            if (hesapCekirdek.Length == 0) return null;

            if (!Ortusuyor(cekirdek, hesapCekirdek))
                return $"'{cekirdek}' anahtarı '{hesapAdi}' hesabının adıyla örtüşmüyor; " +
                       "kesik ya da yanlış firmaya bağlanmış bir anahtar olabilir.";

            return null;
        }

        /// <summary>
        /// İki çekirdek en az kısmen aynı adı gösteriyor mu? Üç yol:
        /// <list type="number">
        /// <item>Biri diğerini kapsıyor ("PARDUS PORTFOY" ⊂ "PARDUS PORTFOY ALTIN FONU").</item>
        /// <item>Ortak bir token (en az 4 karakter): "TEKNOLOJI".</item>
        /// <item>Boşluksuz hâllerinin ortak öneki (en az 5 karakter). Banka metni kelimeleri
        /// başka bölüyor: "SUN TEKS.SAN.VE TİC." ile "Suntek Teknoloji" hiçbir token
        /// paylaşmaz ama ikisi de "SUNTEK" ile başlar.</item>
        /// </list>
        /// Hiçbiri tutmuyorsa anahtar bu hesabın adıyla ilgisizdir.
        /// </summary>
        public static bool Ortusuyor(string a, string b)
        {
            if (Normalizasyon.CekirdekKapsiyorMu(a, b)) return true;

            var aTokenlar = a.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var bTokenlar = b.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (aTokenlar.Any(t => t.Length >= EnKisaOrtakToken &&
                                   bTokenlar.Contains(t, StringComparer.Ordinal)))
                return true;

            var aBitisik = string.Concat(aTokenlar);
            var bBitisik = string.Concat(bTokenlar);
            var enKisa = Math.Min(aBitisik.Length, bBitisik.Length);

            var ortak = 0;
            while (ortak < enKisa && aBitisik[ortak] == bBitisik[ortak]) ortak++;

            return ortak >= EnKisaOrtakOnek;
        }
    }
}
