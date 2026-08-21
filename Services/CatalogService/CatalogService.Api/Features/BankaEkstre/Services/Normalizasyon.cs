using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    /// <summary>
    /// Unvan/açıklama normalizasyonu. Eşleştirmenin her katmanı buradan geçer;
    /// kurallar tek yerde durur ki cari eşleştirme ile öğrenme anahtarı ayrışmasın.
    /// </summary>
    public static class Normalizasyon
    {
        private static readonly TimeSpan RegexZamanAsimi = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Unvanın anlam taşımayan ekleri. Şirket türü ve faaliyet kelimeleri iki farklı
        /// cariyi birbirine benzetip yanlış eşleşme ürettiği için atılır.
        /// </summary>
        private static readonly HashSet<string> GurultuKelimeleri = new(StringComparer.Ordinal)
        {
            "ANONIM", "SIRKETI", "SIRKET", "STI", "AS", "LIMITED", "LTD",
            "SANAYI", "TICARET", "TIC", "SAN", "VE", "ITH", "IHR", "HIZMETLERI"
        };

        /// <summary>Karşı IBAN deseni: açıklama içinde maskeli (yıldızlı) de geçebiliyor.</summary>
        private static readonly Regex IbanDeseni =
            new(@"TR\d{2}[\s\d\*]{16,30}", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexZamanAsimi);

        private static readonly Regex BosluklarDeseni =
            new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexZamanAsimi);

        /// <summary>Türkçe karakterleri ASCII karşılıklarına indirger (İ→I, Ş→S, Ğ→G, Ü→U, Ö→O, Ç→C).</summary>
        public static string TurkceSadelestir(string? metin)
        {
            if (string.IsNullOrEmpty(metin)) return string.Empty;

            var sb = new StringBuilder(metin.Length);
            foreach (var ch in metin)
            {
                sb.Append(ch switch
                {
                    'İ' or 'ı' or 'i' or 'I' => 'I',
                    'Ş' or 'ş' => 'S',
                    'Ğ' or 'ğ' => 'G',
                    'Ü' or 'ü' => 'U',
                    'Ö' or 'ö' => 'O',
                    'Ç' or 'ç' => 'C',
                    'Â' or 'â' => 'A',
                    'Î' or 'î' => 'I',
                    'Û' or 'û' => 'U',
                    _ => char.ToUpperInvariant(ch)
                });
            }
            return sb.ToString();
        }

        /// <summary>
        /// Adım 1–2: büyük harf + Türkçe sadeleştirme, alfanümerik dışı boşluk,
        /// ardından gürültü kelimelerinin atılması.
        /// </summary>
        public static string UnvanNormalize(string? unvan)
        {
            var sade = TurkceSadelestir(unvan);
            if (sade.Length == 0) return string.Empty;

            var sb = new StringBuilder(sade.Length);
            foreach (var ch in sade)
            {
                // Kısaltmalardaki nokta silinir, boşluğa çevrilmez: "A.Ş." → "AS",
                // "Y.M.M." → "YMM". Aksi hâlde tek harflik parçalar gürültü listesinden
                // kaçar ve iki farklı cariyi birbirine yaklaştırır.
                if (ch == '.') continue;
                sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
            }

            var kelimeler = sb.ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(k => !GurultuKelimeleri.Contains(k))
                .ToArray();

            // Tamamı gürültüyse elimizde hiç bilgi kalmasın diye gürültüsüz hâline düşülür.
            if (kelimeler.Length == 0)
                kelimeler = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return string.Join(' ', kelimeler);
        }

        /// <summary>
        /// Öğrenme anahtarı: normalize unvan çekirdeği. Ham açıklamanın hash'i **kullanılmaz** —
        /// banka her satıra farklı sorgu numarası, tarih ve tutar yazdığı için o anahtar asla
        /// ikinci kez eşleşmiyordu.
        ///
        /// "...sorgu numaralı DAGİ GİYİM SANAYİ VE TİCARET ANONİM ŞİRKETİ tarafından..."
        /// → çıkarılan unvan "DAGİ GİYİM SANAYİ VE TİCARET ANONİM ŞİRKETİ" → çekirdek "DAGI GIYIM".
        ///
        /// <see cref="UnvanNormalize"/>'ın üzerine tek harfli token'ları da atar; tek harf
        /// iki farklı cariyi birbirine yaklaştırmaktan başka bir şey yapmıyor.
        /// </summary>
        public static string UnvanCekirdek(string? unvan)
        {
            var normal = UnvanNormalize(unvan);
            if (normal.Length == 0) return string.Empty;

            var kelimeler = normal.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                  .Where(k => k.Length > 1)
                                  .ToArray();

            return kelimeler.Length == 0 ? normal : string.Join(' ', kelimeler);
        }

        /// <summary>
        /// Unvan çıkarılamayan satırların (banka masrafı, HGS, vergi…) öğrenme anahtarı:
        /// işlem tipi + sabit önek. Ham açıklama kullanılsaydı her satır yeni anahtar üretirdi.
        /// </summary>
        public static string IslemAnahtari(string? islemTipi)
        {
            var sade = TurkceSadelestir(islemTipi);
            if (sade.Length == 0) return string.Empty;

            var sb = new StringBuilder(sade.Length);
            foreach (var ch in sade)
                sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');

            var temiz = BosluklarDeseni.Replace(sb.ToString(), " ").Trim();
            return temiz.Length == 0 ? string.Empty : "ISLEM:" + temiz;
        }

        /// <summary>
        /// Ham banka metninin token aramasına uygun hâli: Türkçe sadeleştirilmiş, alfanümerik
        /// dışı boşluk. Ayırt edici kelime (Aidat, Elektrik…) bu metinde aranır.
        /// </summary>
        public static string MetinNormalize(string? metin)
        {
            var sade = TurkceSadelestir(metin);
            if (sade.Length == 0) return string.Empty;

            var sb = new StringBuilder(sade.Length);
            foreach (var ch in sade)
                sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');

            return BosluklarDeseni.Replace(sb.ToString(), " ").Trim();
        }

        /// <summary>
        /// Normalize ifade, normalize metinde <b>tam kelime sınırlarıyla</b> geçiyor mu?
        /// Düz <c>Contains</c> yetmiyor: "TEB" anahtarı "OTEBANK" içinde de geçer ve
        /// yanlış hesaba eşlerdi. İki taraf da <see cref="MetinNormalize"/>'dan geçmiş
        /// olmalı (tek boşluklu, alfanümerik).
        /// </summary>
        public static bool IfadeVarMi(string? normalizeMetin, string? normalizeIfade)
        {
            if (string.IsNullOrEmpty(normalizeMetin) || string.IsNullOrEmpty(normalizeIfade))
                return false;

            var bas = 0;
            while (true)
            {
                var i = normalizeMetin.IndexOf(normalizeIfade, bas, StringComparison.Ordinal);
                if (i < 0) return false;

                var solTemiz = i == 0 || normalizeMetin[i - 1] == ' ';
                var sag = i + normalizeIfade.Length;
                var sagTemiz = sag == normalizeMetin.Length || normalizeMetin[sag] == ' ';

                if (solTemiz && sagTemiz) return true;
                bas = i + 1;
            }
        }

        /// <summary>
        /// Açıklama içindeki karşı IBAN. Ölçümde 286 satırın 97'sinde vardı; en değerli anahtar.
        /// Maskeli (yıldızlı) IBAN öğrenme anahtarı olamayacağı için elenir.
        /// </summary>
        public static string? IbanBul(string? metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return null;

            var eslesme = IbanDeseni.Match(metin);
            if (!eslesme.Success) return null;

            var ham = eslesme.Value.Replace(" ", string.Empty);
            if (ham.Contains('*')) return null;

            var rakamlar = new string(ham.Where(char.IsDigit).ToArray());
            // TR IBAN: 2 harf + 24 rakam.
            if (rakamlar.Length < 24) return null;

            return "TR" + rakamlar[..24];
        }

        /// <summary>IBAN anahtarı: yalnız rakamlar; kullanıcı boşluklu da girse aynı anahtara düşer.</summary>
        public static string IbanAnahtar(string? iban)
        {
            if (string.IsNullOrWhiteSpace(iban)) return string.Empty;
            return new string(iban.Where(char.IsDigit).ToArray());
        }

        /// <summary>VKN/TCKN anahtarı: yalnız rakamlar, 10 veya 11 hane değilse boş.</summary>
        public static string VknAnahtar(string? vkn)
        {
            if (string.IsNullOrWhiteSpace(vkn)) return string.Empty;
            var rakamlar = new string(vkn.Where(char.IsDigit).ToArray());
            return rakamlar.Length is 10 or 11 ? rakamlar : string.Empty;
        }

        /// <summary>
        /// Her kelimenin ilk harfi büyük. Mevcut muavin böyle yazıldığı için ORKA çıktısı
        /// bu biçimde üretilir. Türkçe kültürüyle çalışır (İ → i, I → ı).
        ///
        /// Sınır "harf olmayan her karakter" kabul edilir, yalnız boşluk değil: böylece
        /// "A.Ş." → "A.Ş." kalır, boşlukla bölen bir uygulamada olduğu gibi "A.ş." olmaz.
        /// </summary>
        public static string BaslikBicimi(string? metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return string.Empty;

            var kultur = CultureInfo.GetCultureInfo("tr-TR");
            var sb = new StringBuilder(metin.Length);
            var oncekiHarfti = false;

            foreach (var ch in metin)
            {
                if (!char.IsLetter(ch))
                {
                    sb.Append(ch);
                    oncekiHarfti = false;
                    continue;
                }

                sb.Append(oncekiHarfti ? char.ToLower(ch, kultur) : char.ToUpper(ch, kultur));
                oncekiHarfti = true;
            }

            return sb.ToString();
        }

        /// <summary>
        /// ORKA hesap kodunun saklama biçimi: boşluklu, tek boşluklu, büyük harf.
        /// Boşluklar kaldırılmaz — ORKA tanımaz.
        /// </summary>
        public static string HesapKoduNormalize(string? kod)
        {
            if (string.IsNullOrWhiteSpace(kod)) return string.Empty;
            var tekBosluk = BosluklarDeseni.Replace(kod.Trim(), " ");
            return tekBosluk.ToUpper(CultureInfo.GetCultureInfo("tr-TR"));
        }

        /// <summary>Kodun ilk segmenti, ör. "120 D22" → "120".</summary>
        public static string AnaGrup(string? kod)
        {
            var normal = HesapKoduNormalize(kod);
            if (normal.Length == 0) return string.Empty;
            var ilk = normal.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            return ilk;
        }

        /// <summary>
        /// Ana gruptan sonraki ilk harf, ör. "120 D22" → "D", "329 K08" → "K".
        /// Cari kodları unvanın ilk harfiyle başladığı için arama uzayı bununla daralır.
        /// </summary>
        public static string? BaslangicHarfi(string? kod)
        {
            var normal = HesapKoduNormalize(kod);
            if (normal.Length == 0) return null;

            var parcalar = normal.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var parca in parcalar.Skip(1))
                foreach (var ch in TurkceSadelestir(parca))
                    if (char.IsLetter(ch)) return ch.ToString();

            return null;
        }

        /// <summary>Normalize unvanın ilk harfi (arama uzayı daraltması için).</summary>
        public static string? IlkHarf(string? normalizeUnvan)
        {
            if (string.IsNullOrWhiteSpace(normalizeUnvan)) return null;
            foreach (var ch in normalizeUnvan)
                if (char.IsLetter(ch)) return ch.ToString();
            return null;
        }

        /// <summary>50 karakteri aşan açıklamayı keser (ORKA sınırı).</summary>
        public static string Kirp(string? metin, int enFazla)
        {
            if (string.IsNullOrEmpty(metin)) return string.Empty;
            var temiz = BosluklarDeseni.Replace(metin.Trim(), " ");
            return temiz.Length <= enFazla ? temiz : temiz[..enFazla].TrimEnd();
        }
    }
}
