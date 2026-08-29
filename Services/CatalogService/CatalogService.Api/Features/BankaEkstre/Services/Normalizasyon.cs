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

        /// <summary>
        /// Bankacılık dolgu kelimeleri. Unvan ekleri anlam taşımadığı için atılıyordu
        /// (<see cref="GurultuKelimeleri"/>); bunlar ise banka açıklamasının kendi
        /// gövdesi: "… NO'LU … HESABINDAN … TARAFINDAN … GELEN EFT". Benzersiz önek
        /// katmanı açıklamanın token dizilerini gezdiği için bu kelimeler temizlenmezse
        /// gerçek unvanın n-gram'ları hiç oluşmaz ("HESABINDAN DENIZBANK YURTICI KARGO"
        /// gibi diziler üretilir ve hiçbir cariyle eşleşmez).
        /// </summary>
        private static readonly HashSet<string> DolguKelimeleri = new(StringComparer.Ordinal)
        {
            "NOLU", "SORGU", "NUMARALI", "TARAFINDAN", "TARAFINA", "HESABINDAN", "HESABINA",
            "TARIHLI", "GELEN", "GIDEN", "EFT", "FAST", "HAVALE", "TRANSFER", "CARI", "HESAP",
            "ODEME", "ODEMESI", "FATURA", "SUBESI", "MERKEZ", "IBAN", "NEZDINDEKI", "VADESIZ",
            "MAHSUBEN"
        };

        /// <summary>
        /// Cari adında geçtiğinde kaydı banka yapan kelimeler. Açıklamalarda gönderen/alıcı
        /// bankanın adı da geçiyor; benzersiz önek indeksinde banka isimli cariler kalırsa
        /// "ZİRAAT BANKASI" metni <c>320 1 10011 ZİRAAT BANK</c> carisine eşleşir ve
        /// ölçümde 16 satırı yanlış çözüyordu. Bankalar zaten banka kayıt defteri katmanının işi.
        /// </summary>
        private static readonly HashSet<string> BankaKelimeleri = new(StringComparer.Ordinal)
        {
            "BANKASI", "BANKA", "BANK", "FINANS", "KATILIM"
        };

        /// <summary>Karşı IBAN deseni: açıklama içinde maskeli (yıldızlı) de geçebiliyor.</summary>
        private static readonly Regex IbanDeseni =
            new(@"TR\d{2}[\s\d\*]{16,30}", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexZamanAsimi);

        /// <summary>Kredi taksit satırlarındaki "(numara) KREDI HESAP NUMARALI" kalıbı (normalize metinde).</summary>
        private static readonly Regex KrediHesapDeseni =
            new(@"(\d{6,})\s+KREDI HESAP NUMARALI", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexZamanAsimi);

        /// <summary>
        /// İş Bankası'nın yazımı: "KREDİ NO: 10080844268 ANAPARA TAHSİLAT". Normalize
        /// metinde iki nokta boşluğa döndüğü için desen "KREDI NO (numara)" olur.
        /// </summary>
        private static readonly Regex KrediNoDeseni =
            new(@"KREDI NO\s+(\d{6,})", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexZamanAsimi);

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
        /// Kredi taksit satırlarının öğrenme anahtarı: <b>kredi hesap numarası</b>.
        /// Açıklama "6501439328  kredi hesap numaralı İşletme İhtiyaç Kredisi … Taksit
        /// Tahsilatı …" biçiminde; işlem tipi ("Taksitli Tahsilat") her kredide aynı olduğu
        /// için ondan üretilen anahtar bütün kredileri tek hesaba bağlıyordu. Oysa her
        /// kredinin muavini ayrı (300 1 0015 328, 300 1 20 …).
        ///
        /// Karşılaştırma normalize metin üzerinden yapılır: "numaralı" yazımındaki Türkçe
        /// karakter ve çift boşluklar burada sadeleşir.
        ///
        /// Her banka kredi numarasını başka türlü yazıyor; iki yazım da aynı anahtarı
        /// üretir (Vakıfbank "… kredi hesap numaralı", İş Bankası "KREDİ NO: …").
        /// </summary>
        public static string KrediAnahtar(string? hamAciklama)
        {
            var metin = MetinNormalize(hamAciklama);
            if (metin.Length == 0) return string.Empty;

            var eslesme = KrediHesapDeseni.Match(metin);
            if (eslesme.Success) return "KREDI:" + eslesme.Groups[1].Value;

            eslesme = KrediNoDeseni.Match(metin);
            return eslesme.Success ? "KREDI:" + eslesme.Groups[1].Value : string.Empty;
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
        ///
        /// <b>İlk</b> IBAN'ı verir; virman/döviz satırlarında metinde iki IBAN geçiyor ve
        /// ilki ekstrenin kendi hesabı olabiliyor ("… TR40 … nolu hesabından TR80 … nolu
        /// hesabına …"). Karşı tarafı ararken <see cref="IbanlariBul"/> kullanılmalı.
        /// </summary>
        public static string? IbanBul(string? metin) => IbanlariBul(metin).FirstOrDefault();

        /// <summary>
        /// Metindeki <b>tüm</b> IBAN'lar, geçtikleri sırayla ve tekrarsız. Maskeli
        /// (yıldızlı) yazımlar elenir; hepsi "TR" + 24 rakam biçimine indirgenir.
        /// </summary>
        public static List<string> IbanlariBul(string? metin)
        {
            var bulunanlar = new List<string>();
            if (string.IsNullOrWhiteSpace(metin)) return bulunanlar;

            foreach (Match eslesme in IbanDeseni.Matches(metin))
            {
                var ham = eslesme.Value.Replace(" ", string.Empty);

                // TR IBAN: 2 harf + 24 rakam. Rakamlar soldan toplanır ve 24'e ulaşınca
                // durulur; yıldıza bu sayı tamamlanmadan rastlanırsa yazım maskelidir ve
                // anahtar olamaz.
                //
                // Bütün eşleşmede yıldız aramak yetmiyordu: İş Bankası açıklamaları alanları
                // yıldızla ayırıyor ("…*TR400001500158007298490100*VAKIFBANK*…") ve desen
                // yıldızı da yuttuğu için sapasağlam bir IBAN maskeli sanılıp eleniyordu.
                var rakamlar = new StringBuilder(24);
                var maskeli = false;

                foreach (var ch in ham)
                {
                    if (char.IsDigit(ch))
                    {
                        rakamlar.Append(ch);
                        if (rakamlar.Length == 24) break;
                    }
                    else if (ch == '*')
                    {
                        maskeli = true;
                        break;
                    }
                }

                if (maskeli || rakamlar.Length < 24) continue;

                var iban = "TR" + rakamlar;
                if (!bulunanlar.Contains(iban, StringComparer.Ordinal)) bulunanlar.Add(iban);
            }

            return bulunanlar;
        }

        /// <summary>
        /// IBAN anahtarı: yalnız rakamlar. Karşılaştırmanın <b>iki tarafı</b> da buradan
        /// geçmeli — banka metninde IBAN boşluklu ("TR80 0001 5001 …"), Tanımlar'daki
        /// kayıtta bitişik ("TR800001500158048013139400") yazılıyor.
        /// </summary>
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
        /// Virgülle ayrılmış ana grup listesini ayrıştırır: <c>"195, 196"</c> → <c>["195","196"]</c>.
        /// Her parça ana gruba indirgenir (<c>"195 01"</c> → <c>"195"</c>) — kullanıcı kutuya
        /// tam kod yazarsa da kural çalışır. Tekrarlar ilk görüldükleri sırada tekilleştirilir.
        /// </summary>
        public static List<string> AnaGruplariAyir(string? metin)
        {
            var sonuc = new List<string>();
            if (string.IsNullOrWhiteSpace(metin)) return sonuc;

            foreach (var parca in metin.Split(AnaGrupAyraclari, StringSplitOptions.RemoveEmptyEntries))
            {
                var grup = AnaGrup(parca);
                if (grup.Length > 0 && !sonuc.Contains(grup, StringComparer.Ordinal)) sonuc.Add(grup);
            }

            return sonuc;
        }

        /// <summary>Ana grup listesinin saklama ve gösterim biçimi: <c>"195, 196"</c>.</summary>
        public static string AnaGruplariBirlestir(IEnumerable<string> gruplar) => string.Join(", ", gruplar);

        /// <summary>
        /// Bir sabit kuralın alt hesap araması yapacağı ana grup kümesi. Kuralda ana grup
        /// listesi tanımlıysa o kullanılır; tanımlı değilse küme, hesap kodunun tek ana
        /// grubudur — çoklu grup gelmeden önceki davranış aynen korunur.
        /// </summary>
        public static List<string> KuralAnaGruplari(string? anaGruplar, string? hesapKodu)
        {
            var tanimli = AnaGruplariAyir(anaGruplar);
            if (tanimli.Count > 0) return tanimli;

            var tek = AnaGrup(hesapKodu);
            return tek.Length == 0 ? new List<string>() : new List<string> { tek };
        }

        private static readonly char[] AnaGrupAyraclari = { ',', ';' };

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

        /// <summary>
        /// Kısaltmaları koruyan normalize: nokta <b>silinir</b>, kalan alfanümerik dışı
        /// karakterler boşluğa çevrilir. "E.F.T." → "EFT", "A.Ş." → "AS".
        ///
        /// Açıklama şablonu eşleştirmesi bunu kullanır: aynı ifade dosyada hem
        /// "HESAPLAR ARASI EFT" hem "HESAPLAR ARASI E.F.T." diye yazılıyor ve
        /// <see cref="MetinNormalize"/> ikincisini "E F T" yapıp eşleşmeyi kaçırıyordu.
        /// </summary>
        public static string KisaltmaNormalize(string? metin)
        {
            var sade = TurkceSadelestir(metin);
            if (sade.Length == 0) return string.Empty;

            var sb = new StringBuilder(sade.Length);
            foreach (var ch in sade)
            {
                if (ch == '.') continue;
                sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
            }

            return BosluklarDeseni.Replace(sb.ToString(), " ").Trim();
        }

        /// <summary>
        /// Benzersiz önek katmanının ortak token üretimi. <b>Açıklama ve hesap adı aynı
        /// boru hattından geçer</b>; geçmezse iki tarafın token dizileri hizalanmaz ve
        /// önek eşleşmesi hiç tutmaz.
        ///
        /// Sırasıyla: Türkçe sadeleştirme + alfanümerik dışını boşluk, unvan eki gürültüsü,
        /// bankacılık dolgusu, salt sayısal token'lar ve tek harfli token'lar atılır.
        /// Tamamı elenirse gürültüsüz hâline düşülür — boş dizi döndürmek "hiç bilgi yok"
        /// demek olurdu.
        /// </summary>
        public static List<string> CekirdekTokenlari(string? metin)
        {
            var normal = MetinNormalize(metin);
            if (normal.Length == 0) return new List<string>();

            var hepsi = normal.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var temiz = hepsi
                .Where(t => t.Length > 1)
                .Where(t => !t.All(char.IsDigit))
                .Where(t => !GurultuKelimeleri.Contains(t) && !DolguKelimeleri.Contains(t))
                .ToList();

            return temiz.Count > 0 ? temiz : hepsi.Where(t => t.Length > 1).ToList();
        }

        /// <summary>
        /// <see cref="CekirdekTokenlari"/>'nın tek boşlukla birleştirilmiş hâli. Hesap adı
        /// çekirdeği ve açıklama n-gram'ları bu biçimde karşılaştırılır.
        /// </summary>
        public static string Cekirdek(string? metin) => string.Join(' ', CekirdekTokenlari(metin));

        /// <summary>
        /// Ad bir bankayı mı gösteriyor? Karşılaştırma <b>tam token</b> üzerinden yapılır:
        /// düz <c>Contains</c> "BANK" kelimesini "OTEBANK" içinde de bulur ve alakasız bir
        /// cariyi indeksten düşürürdü.
        /// </summary>
        public static bool BankaAdliMi(string? ad)
            => CekirdekTokenlari(ad).Any(BankaKelimeleri.Contains);

        /// <summary>
        /// İki çekirdekten biri diğerini kapsıyor mu (tam kelime sınırlarıyla)?
        ///
        /// Çekirdek <b>eşitliği</b> yetmiyor: hesap sahibi "PKF ADAY BAGIMSIZ DENETIM" ile
        /// bankanın yazdığı "ADAY BAGIMSIZ DENETIM" eşit değil ama aynı firma. Kapsama
        /// kontrolü ikisini de aynı kimliğe bağlar.
        /// </summary>
        public static bool CekirdekKapsiyorMu(string? a, string? b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            return IfadeVarMi(a, b) || IfadeVarMi(b, a);
        }

        /// <summary>
        /// Plaka karşılaştırma anahtarı: harf/rakam dışı her şey atılır. Hesap planında
        /// plakalar boşluklu ("34 Mrp 081"), banka metninde bitişik ("34MRP081") yazılıyor;
        /// boşluk temizlenmeden iki taraf hiç eşleşmez.
        /// </summary>
        public static string PlakaAnahtar(string? plaka)
        {
            var sade = TurkceSadelestir(plaka);
            if (sade.Length == 0) return string.Empty;

            var sb = new StringBuilder(sade.Length);
            foreach (var ch in sade)
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);

            return sb.ToString();
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
