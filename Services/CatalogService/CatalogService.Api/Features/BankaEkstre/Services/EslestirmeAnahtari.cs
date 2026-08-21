namespace CatalogService.Api.Features.BankaEkstre.Services
{
    /// <summary>
    /// Banka hesabının eşleştirme anahtarları: virgülle ayrılmış listenin ayrıştırılması,
    /// saklama biçimi ve hesap adından öneri üretimi.
    ///
    /// Anahtarlar, aynı bankada birden fazla hesap olduğunda hangi hesabın kastedildiğini
    /// ayırt eder. Ekstre açıklaması "Otomatik Süpürme Pkf Aday" yazdığında yalnız
    /// <c>BankaAdi</c> ("Vakıfbank") arandığı sürece süpürme hesabı ile vadesiz hesap
    /// birbirinden ayrılamıyordu.
    /// </summary>
    public static class EslestirmeAnahtari
    {
        /// <summary>Sütun sınırı; öneri de bu uzunluğu aşmaz.</summary>
        public const int EnFazlaUzunluk = 300;

        /// <summary>
        /// Bundan kısa anahtar yok sayılır. İki harflik bir parça neredeyse her açıklamada
        /// geçer ve yanlış hesaba eşler; mevcut banka adı eşlemesindeki sınırla aynı.
        /// </summary>
        public const int EnKisaAnahtar = 3;

        /// <summary>Öneriye en fazla kaç ifade konur; kalanı kullanıcı elle ekler.</summary>
        private const int EnFazlaOneri = 3;

        /// <summary>
        /// Hesap adında her hesapta geçen, ayırt etmeyen kelimeler. Öneriden atılırlar;
        /// kullanıcı isterse elle yazabilir.
        /// </summary>
        private static readonly HashSet<string> GenelKelimeler = new(StringComparer.Ordinal)
        {
            "VADESIZ", "VADELI", "TL", "TRY", "TRL", "USD", "EUR", "GBP", "DOVIZ", "KUR",
            "HESAP", "HESABI", "HESAPLARI", "BANKA", "BANKASI", "BANK", "BANKACILIK",
            "SUBE", "SUBESI", "MEVDUAT", "CARI", "KATILIM", "ANONIM", "SIRKETI", "AS"
        };

        /// <summary>Öneri üretirken parçalara bölen ayraçlar (hesap adı "Banka, Tip - Ad" kalıbında).</summary>
        private static readonly char[] Ayraclar = { ',', '-', '/', '(', ')', ';', '|' };

        /// <summary>
        /// Virgülle ayrılmış listeyi tek tek anahtarlara böler: boşlar atılır, baştaki ve
        /// sondaki boşluklar kırpılır, aynı anahtar (normalize hâline göre) iki kez girmez.
        /// </summary>
        public static List<string> Ayristir(string? liste)
        {
            var sonuc = new List<string>();
            if (string.IsNullOrWhiteSpace(liste)) return sonuc;

            var gorulen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var parca in liste.Split(','))
            {
                var temiz = Normalizasyon.Kirp(parca, EnFazlaUzunluk);
                if (temiz.Length == 0) continue;

                var normal = Normalizasyon.MetinNormalize(temiz);
                if (normal.Length == 0 || !gorulen.Add(normal)) continue;

                sonuc.Add(temiz);
            }

            return sonuc;
        }

        /// <summary>Saklama biçimi: temizlenmiş anahtarlar ", " ile birleştirilir; boşsa null.</summary>
        public static string? Duzenle(string? liste)
        {
            var anahtarlar = Ayristir(liste);
            if (anahtarlar.Count == 0) return null;

            var birlesik = string.Join(", ", anahtarlar);
            return birlesik.Length <= EnFazlaUzunluk ? birlesik : birlesik[..EnFazlaUzunluk].TrimEnd(' ', ',');
        }

        /// <summary>
        /// Açıklamada aranacak hâlleri: <see cref="Normalizasyon.MetinNormalize"/>'dan geçmiş,
        /// <see cref="EnKisaAnahtar"/> altındakiler elenmiş. Eşleştirme yalnız bunları kullanır.
        /// </summary>
        public static List<string> NormalizeAnahtarlar(string? liste)
            => Ayristir(liste)
                .Select(Normalizasyon.MetinNormalize)
                .Where(a => a.Length >= EnKisaAnahtar)
                .ToList();

        /// <summary>
        /// Hesap adından ayırt edici anahtar önerisi: banka adı, genel kelimeler ve hesap
        /// numaraları atıldıktan sonra kalan ifadeler. Kullanıcı formda düzenler; öneri
        /// bilerek dar tutulur, fazla anahtar yanlış hesaba eşlemekten daha kötüdür.
        ///
        /// "Vakıfbank, Vadeli Tl - Otomatik Süpürme Hesabı" → "Otomatik Süpürme"
        /// "Ziraat Bankası, Günlük Kazanan Hesap - 5022"    → "Günlük Kazanan"
        /// </summary>
        public static string? Oner(string? hesapAdi, string? bankaAdi)
        {
            if (string.IsNullOrWhiteSpace(hesapAdi)) return null;

            var bankaTokenlari = Normalizasyon.MetinNormalize(bankaAdi)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length >= EnKisaAnahtar)
                .ToHashSet(StringComparer.Ordinal);

            var ifadeler = new List<string>();
            var gorulen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var parca in hesapAdi.Split(Ayraclar, StringSplitOptions.RemoveEmptyEntries))
            {
                var kalan = new List<string>();

                foreach (var kelime in parca.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                {
                    var normal = Normalizasyon.MetinNormalize(kelime);
                    if (normal.Length < EnKisaAnahtar) continue;
                    if (normal.All(char.IsDigit)) continue;
                    if (bankaTokenlari.Contains(normal) || GenelKelimeler.Contains(normal)) continue;

                    var gosterim = Kirpik(kelime);
                    if (gosterim.Length > 0) kalan.Add(gosterim);
                }

                if (kalan.Count == 0) continue;

                var ifade = Normalizasyon.BaslikBicimi(string.Join(' ', kalan));
                if (gorulen.Add(Normalizasyon.MetinNormalize(ifade))) ifadeler.Add(ifade);

                if (ifadeler.Count == EnFazlaOneri) break;
            }

            return Duzenle(string.Join(", ", ifadeler));
        }

        /// <summary>Kelimenin baş/son noktalama işaretlerini atar; içindeki kesme işareti korunur.</summary>
        private static string Kirpik(string kelime)
        {
            var bas = 0;
            var son = kelime.Length - 1;

            while (bas <= son && !char.IsLetterOrDigit(kelime[bas])) bas++;
            while (son >= bas && !char.IsLetterOrDigit(kelime[son])) son--;

            return son < bas ? string.Empty : kelime[bas..(son + 1)];
        }
    }
}
