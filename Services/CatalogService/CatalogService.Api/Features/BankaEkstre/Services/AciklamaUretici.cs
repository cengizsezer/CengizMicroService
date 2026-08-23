using System.Text.RegularExpressions;
using CatalogService.Api.Features.BankaEkstre.Domain;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    /// <summary>Açıklama üretimi ve eşleştirme için satırın bir arada gezen bağlamı.</summary>
    public class SatirBaglami
    {
        public string IslemTipi { get; set; } = string.Empty;
        public string HamAciklama { get; set; } = string.Empty;
        public Yon Yon { get; set; }
        public string? KarsiIban { get; set; }
        public string? KarsiVkn { get; set; }

        /// <summary>Desenlerden çıkarılan unvan; hiçbir desen tutmadıysa null.</summary>
        public string? Unvan { get; set; }

        /// <summary>
        /// En az bir desen hesap sahibinin <b>kendi</b> unvanını yakaladı ve atıldı.
        ///
        /// Bu, satırın kendi hesapları arası bir transfer olduğunun en güvenilir işareti:
        /// karşı taraf da firmanın kendisi. Banka kayıt defteri katmanı (Katman 2) bu
        /// bayrakla da açılır — işlem tipi "Hesaba giden EFT" olduğu için şablon
        /// <c>BankalarArasi</c> demiyor, ama açıklama "… NO'LU PKF ADAY … HESABINA YAPILAN
        /// … EFT" diyor. Karşı taraf başkasıysa (ZAFER GENÇ, YURTİÇİ KARGO) bayrak açılmaz.
        /// </summary>
        public bool HesapSahibiElendi { get; set; }

        /// <summary>Bankalar arası hareketlerde metinde geçen banka adı.</summary>
        public string? BankaAdi { get; set; }

        /// <summary>Eşleşen şablon; bankalar arası olup olmadığı buradan okunur.</summary>
        public AciklamaSablonu? Sablon { get; set; }

        /// <summary>
        /// Bu satır için öğrenme/eşleştirme anahtarı üretilmemeli. İki durumda açılır:
        /// <list type="bullet">
        /// <item>Unvan olarak yalnız hesap sahibinin kendi adı yakalandı (karşı taraf bilinmiyor).</item>
        /// <item>Açıklama kapsamlı bir sabit kural tuttu ve karşı taraf bir cari değil, kişi
        /// (personel avansı) — anahtar işlem tipine düşerse tüm havaleler aynı kişiye öğrenilir.</item>
        /// </list>
        /// Her ikisinde de işlem tipi anahtarına <b>düşülmez</b>: düşülseydi "ISLEM:GÖNDERİLEN
        /// HAVALE" anahtarı ilk onaydan sonra ilgisiz satırları da çözerdi.
        /// </summary>
        public bool AnahtarUretilmesin { get; set; }
    }

    public interface IAciklamaUretici
    {
        /// <summary>
        /// Satıra uyan şablonu bulur (yoksa null).
        ///
        /// Önce <b>ham açıklama</b> taranır, sonra işlem tipi. Sıra böyle: "HESAPLAR ARASI
        /// E.F.T. VAKIFBANK/DENİZBANK …" satırının işlem tipi "Gelen EFT Otomatik Yatan"
        /// olduğu için genel şablona düşüyor ve açıklama karşı bankayı hiç yazmıyordu;
        /// açıklamada geçen ifade işlem tipinden daha belirleyici.
        /// </summary>
        AciklamaSablonu? SablonBul(string islemTipi, IReadOnlyList<AciklamaSablonu> sablonlar,
                                   string? hamAciklama = null);

        /// <summary>Şablonu doldurup 50 karakterle sınırlı, Title Case açıklama üretir.</summary>
        string Uret(SatirBaglami baglam);
    }

    /// <summary>
    /// Muhasebe açıklaması üretimi. Şablon tablosu koda gömülmez; buradaki tek iş
    /// yer tutucuları doldurmak, Title Case'e çevirmek ve ORKA'nın 50 karakter
    /// sınırına kırpmaktır.
    /// </summary>
    public class AciklamaUretici : IAciklamaUretici
    {
        /// <summary>Şablonda kullanılabilecek bir yer tutucu ve ne doldurduğu.</summary>
        public sealed record YerTutucu(string Ad, string Aciklama);

        public const string UnvanYt = "{UNVAN}";
        public const string BankaYt = "{BANKA}";
        public const string HesapYt = "{HESAP}";
        public const string PlakaYt = "{PLAKA}";
        public const string VergiYt = "{VERGI}";

        /// <summary>
        /// Şablon ekranının listelediği yer tutucular ve <see cref="Uret"/>'in doldurduğu
        /// yer tutucular <b>aynı</b> listeden gelir: yenisi eklendiğinde ekranda da görünür,
        /// eskisi kaldırıldığında şablon denetimi de kabul etmez.
        ///
        /// Değeri boş kalan yer tutucu, kendisine bağlı ayraçla ("- {UNVAN}") birlikte düşer.
        /// </summary>
        public static readonly IReadOnlyList<YerTutucu> YerTutucular = new[]
        {
            new YerTutucu(UnvanYt, "Açıklamadan çıkarılan karşı taraf unvanı"),
            new YerTutucu(BankaYt, "Bankalar arası hareketlerde metinde geçen banka adı"),
            new YerTutucu(HesapYt, "Karşı hesabın adı: unvan varsa o, yoksa banka adı"),
            new YerTutucu(PlakaYt, "HGS/otoyol yüklemelerinde açıklamadaki plaka"),
            new YerTutucu(VergiYt, "Vergi tahsilatlarında beyanname türü (KDV, Muhtasar, Damga …)")
        };

        /// <summary>ORKA muhasebe açıklamasını 50 karakterde kesiyor.</summary>
        public const int EnFazlaUzunluk = 50;

        private static readonly TimeSpan ZamanAsimi = TimeSpan.FromMilliseconds(250);

        /// <summary>HGS/otoyol yüklemelerinde plaka: "34 ABC 123" ve bitişik yazımları.</summary>
        private static readonly Regex PlakaDeseni = new(
            @"\b\d{2}\s?[A-ZÇĞİÖŞÜ]{1,3}\s?\d{2,4}\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant, ZamanAsimi);

        /// <summary>Vergi tahsilatlarında beyanname türü; listede yoksa yer tutucu düşer.</summary>
        private static readonly string[] VergiTurleri =
        {
            "KDV", "MUHTASAR", "GECICI", "GEÇİCİ", "KURUMLAR", "GELIR", "GELİR",
            "DAMGA", "OTV", "ÖTV", "MTV", "SGK", "BAGKUR", "BAĞKUR", "STOPAJ"
        };

        public AciklamaSablonu? SablonBul(string islemTipi, IReadOnlyList<AciklamaSablonu> sablonlar,
                                          string? hamAciklama = null)
        {
            if (sablonlar.Count == 0) return null;

            var aciklamaSablonu = AciklamadaAra(hamAciklama, sablonlar);
            if (aciklamaSablonu is not null) return aciklamaSablonu;

            var hedef = Normalizasyon.TurkceSadelestir(islemTipi).Trim();

            foreach (var sablon in sablonlar.Where(s => s.Aktif).OrderBy(s => s.Sira))
            {
                var desen = Normalizasyon.TurkceSadelestir(sablon.IslemTipiDeseni).Trim();

                var uyuyor = sablon.EslesmeTuru switch
                {
                    EslesmeTuru.Tam => string.Equals(hedef, desen, StringComparison.Ordinal),
                    EslesmeTuru.Icerir => desen.Length > 0 && hedef.Contains(desen, StringComparison.Ordinal),
                    EslesmeTuru.Regex => RegexUyuyorMu(sablon.IslemTipiDeseni, islemTipi),
                    _ => false
                };

                if (uyuyor) return sablon;
            }

            return null;
        }

        /// <summary>
        /// Ham açıklamada geçen ifadeye karşılık gelen şablon. Yalnız <see cref="EslesmeTuru.Icerir"/>
        /// ve <see cref="EslesmeTuru.Regex"/> şablonları aranır: <see cref="EslesmeTuru.Tam"/>
        /// şablonu işlem tipinin tamamına eşitlik demek, açıklamada karşılığı yok.
        ///
        /// Karşılaştırma <see cref="Normalizasyon.KisaltmaNormalize"/> üzerinden ve tam kelime
        /// sınırıyla: aynı ifade dosyada hem "HESAPLAR ARASI EFT" hem "HESAPLAR ARASI E.F.T."
        /// diye yazılıyor.
        /// </summary>
        private static AciklamaSablonu? AciklamadaAra(string? hamAciklama, IReadOnlyList<AciklamaSablonu> sablonlar)
        {
            if (string.IsNullOrWhiteSpace(hamAciklama)) return null;

            var metin = Normalizasyon.KisaltmaNormalize(hamAciklama);
            if (metin.Length == 0) return null;

            foreach (var sablon in sablonlar.Where(s => s.Aktif).OrderBy(s => s.Sira))
            {
                var uyuyor = sablon.EslesmeTuru switch
                {
                    EslesmeTuru.Icerir => Normalizasyon.IfadeVarMi(
                        metin, Normalizasyon.KisaltmaNormalize(sablon.IslemTipiDeseni)),
                    EslesmeTuru.Regex => RegexUyuyorMu(sablon.IslemTipiDeseni, hamAciklama),
                    _ => false
                };

                if (uyuyor) return sablon;
            }

            return null;
        }

        public string Uret(SatirBaglami baglam)
        {
            var sablon = baglam.Sablon?.Sablon;

            // Şablon yoksa satır yine de bir açıklama alır: işlem tipi (+ varsa unvan).
            // Uydurma yapılmaz, yalnız bankanın kendi metni düzenlenir.
            if (string.IsNullOrWhiteSpace(sablon))
            {
                var taban = Normalizasyon.BaslikBicimi(baglam.IslemTipi);
                if (!string.IsNullOrWhiteSpace(baglam.Unvan))
                    taban = $"{taban} - {Normalizasyon.BaslikBicimi(baglam.Unvan)}";

                return Normalizasyon.Kirp(taban, EnFazlaUzunluk);
            }

            var metin = sablon;
            metin = YerTutucuDoldur(metin, UnvanYt, baglam.Unvan);
            metin = YerTutucuDoldur(metin, BankaYt, baglam.BankaAdi);
            metin = YerTutucuDoldur(metin, HesapYt, HesapAdi(baglam));
            metin = YerTutucuDoldur(metin, PlakaYt, PlakaBul(baglam.HamAciklama));
            metin = YerTutucuDoldur(metin, VergiYt, VergiTuruBul(baglam.HamAciklama));

            return Normalizasyon.Kirp(Normalizasyon.BaslikBicimi(metin), EnFazlaUzunluk);
        }

        /// <summary>
        /// Yer tutucu doluysa yerine konur; boşsa yer tutucu ve ona bağlı ayraç
        /// ("- {UNVAN}") birlikte düşer, açıklama sonu " -" ile bitmez.
        /// </summary>
        private static string YerTutucuDoldur(string metin, string yerTutucu, string? deger)
        {
            if (!metin.Contains(yerTutucu, StringComparison.Ordinal)) return metin;

            if (!string.IsNullOrWhiteSpace(deger))
                return metin.Replace(yerTutucu, deger.Trim(), StringComparison.Ordinal);

            var temiz = metin.Replace(yerTutucu, string.Empty, StringComparison.Ordinal).TrimEnd();
            return temiz.TrimEnd('-', '–', ':', ',').TrimEnd();
        }

        /// <summary>Hesaplararası virmanda karşı hesabın adı: unvan varsa o, yoksa banka adı.</summary>
        private static string? HesapAdi(SatirBaglami baglam)
            => !string.IsNullOrWhiteSpace(baglam.Unvan) ? baglam.Unvan : baglam.BankaAdi;

        private static string? PlakaBul(string? metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return null;

            try
            {
                var eslesme = PlakaDeseni.Match(metin.ToUpperInvariant());
                return eslesme.Success ? Regex.Replace(eslesme.Value, @"\s+", " ", RegexOptions.None, ZamanAsimi) : null;
            }
            catch (RegexMatchTimeoutException)
            {
                return null;
            }
        }

        private static string? VergiTuruBul(string? metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return null;

            var buyuk = Normalizasyon.TurkceSadelestir(metin);
            foreach (var tur in VergiTurleri)
            {
                var sade = Normalizasyon.TurkceSadelestir(tur);
                if (buyuk.Contains(sade, StringComparison.Ordinal))
                    return tur;
            }

            return null;
        }

        private static bool RegexUyuyorMu(string desen, string metin)
        {
            try
            {
                return Regex.IsMatch(metin, desen, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, ZamanAsimi);
            }
            catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
            {
                return false;
            }
        }
    }
}
