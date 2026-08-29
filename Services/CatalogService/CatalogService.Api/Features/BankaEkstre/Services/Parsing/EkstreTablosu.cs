using System.Globalization;

namespace CatalogService.Api.Features.BankaEkstre.Services.Parsing
{
    /// <summary>
    /// Okuyucudan bağımsız tek hücre. Üç yeni banka üç farklı yoldan okunuyor (ClosedXML,
    /// NPOI/HSSF, ham XML) ve her yolun kendi hücre tipi var; ayrıştırıcılar bu ortak
    /// biçimi görsün diye araya bu model kondu.
    ///
    /// Sayı ve tarih <b>ayrı</b> alanlarda tutulur: metne çevrilip yeniden ayrıştırılsalardı
    /// "12500.75" değeri tr-TR kültüründe 1250075 olurdu (Vakıfbank ayrıştırıcısında da
    /// aynı sebeple sayısal hücre doğrudan okunuyor).
    /// </summary>
    public sealed class TabloHucresi
    {
        public static readonly TabloHucresi Bos = new(string.Empty, null, null);

        public TabloHucresi(string? metin, double? sayi, DateTime? tarih)
        {
            Metin = metin?.Trim() ?? string.Empty;
            Sayi = sayi;
            Tarih = tarih;
        }

        /// <summary>Hücrenin metni; sayısal hücrelerde biçimlenmiş hâli.</summary>
        public string Metin { get; }

        /// <summary>Sayısal hücrenin değeri; metin hücrede null.</summary>
        public double? Sayi { get; }

        /// <summary>Tarih biçimli hücrenin değeri; ham XML yolunda her zaman null (bkz. <see cref="TabloDeger"/>).</summary>
        public DateTime? Tarih { get; }

        public bool BosMu => Metin.Length == 0 && Sayi is null && Tarih is null;
    }

    /// <summary>Tablonun tek satırı. <see cref="SatirNo"/> kaynak dosyadaki 1 tabanlı Excel satır numarasıdır.</summary>
    public sealed class TabloSatiri
    {
        private readonly IReadOnlyList<TabloHucresi> _hucreler;

        public TabloSatiri(int satirNo, IReadOnlyList<TabloHucresi> hucreler)
        {
            SatirNo = satirNo;
            _hucreler = hucreler;
        }

        public int SatirNo { get; }

        public int KolonSayisi => _hucreler.Count;

        /// <summary>1 tabanlı kolon; aralık dışı istek boş hücre verir (kolon haritası eksik olabilir).</summary>
        public TabloHucresi Hucre(int kolon)
            => kolon >= 1 && kolon <= _hucreler.Count ? _hucreler[kolon - 1] : TabloHucresi.Bos;

        public bool BosMu => _hucreler.All(h => h.BosMu);

        public int DoluHucreSayisi => _hucreler.Count(h => !h.BosMu);

        /// <summary>Dolu hücreler, 1 tabanlı kolon numaralarıyla.</summary>
        public IEnumerable<(int Kolon, TabloHucresi Hucre)> DoluHucreler()
        {
            for (var i = 0; i < _hucreler.Count; i++)
                if (!_hucreler[i].BosMu) yield return (i + 1, _hucreler[i]);
        }

        /// <summary>Uyarıya yazılacak satır özeti: dolu hücrelerin metinleri.</summary>
        public string Ozet()
        {
            var parcalar = DoluHucreler().Select(h => h.Hucre.Metin).Where(m => m.Length > 0).Take(20).ToList();
            return parcalar.Count == 0 ? "(boş)" : string.Join(" | ", parcalar);
        }
    }

    /// <summary>Tek sayfalık okunmuş tablo.</summary>
    public sealed class EkstreTablosu
    {
        public EkstreTablosu(IReadOnlyList<TabloSatiri> satirlar, string okuyucu)
        {
            Satirlar = satirlar;
            Okuyucu = okuyucu;
        }

        public IReadOnlyList<TabloSatiri> Satirlar { get; }

        /// <summary>Hangi okuyucunun döndürdüğü ("ClosedXML", "NPOI HSSF", "ham XML"). Uyarılarda geçer.</summary>
        public string Okuyucu { get; }

        public int SonSatirNo => Satirlar.Count == 0 ? 0 : Satirlar[^1].SatirNo;

        /// <summary>
        /// Tablo kullanılabilir mi? Bir okuyucu hata vermeden de işe yaramaz sonuç
        /// dönebiliyor: Akbank dosyasında openpyxl tüm satırı <b>tek hücre</b> görüyor.
        /// En az bir satırda iki dolu hücre yoksa sıradaki okuyucuya geçilir.
        /// </summary>
        public bool Kullanilabilir => Satirlar.Any(s => s.DoluHucreSayisi >= 2);
    }

    /// <summary>
    /// Hücreden tarih/tutar okuma. Üç ayrıştırıcı da buradan geçer; kural tek yerde
    /// dursun ki bankalar arasında ayrışmasın.
    /// </summary>
    public static class TabloDeger
    {
        private static readonly CultureInfo Turkce = CultureInfo.GetCultureInfo("tr-TR");

        /// <summary>Desteklenen metin tarih biçimleri. Saat kısmı ayrıldıktan sonra denenir.</summary>
        private static readonly string[] TarihBicimleri =
        {
            "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy"
        };

        /// <summary>
        /// Excel seri numarasının makul aralığı: 1950-01-01 ile 2079-12-31 arası. Ham XML
        /// yolunda hücrenin biçimi <b>bilinmiyor</b> (styles.xml bozuk olduğu için zaten o
        /// yola düşülüyor), tarih ile sıradan bir sayı ancak bu aralıkla ayrılabiliyor.
        /// Seri numarası yorumu yalnız tarih kolonunda denenir, tutarda değil.
        /// </summary>
        private const double EnKucukSeri = 18264;   // 1950-01-01
        private const double EnBuyukSeri = 65746;   // 2079-12-31

        /// <summary>
        /// Tarih. Sıra: tarih biçimli hücre → metin → Excel seri numarası.
        ///
        /// Metinde saat ayracı olarak hem boşluk hem <b>tire</b> kabul edilir: İş Bankası
        /// "Tarih/Saat" kolonunu <c>26/08/2026-14:58:47</c> diye yazıyor.
        /// </summary>
        public static bool Tarih(TabloHucresi hucre, out DateTime tarih)
        {
            tarih = default;

            if (hucre.Tarih is { } dt)
            {
                tarih = dt.Date;
                return true;
            }

            var metin = hucre.Metin;
            if (metin.Length > 0)
            {
                var sadeceTarih = TarihParcasi(metin);

                if (DateTime.TryParseExact(sadeceTarih, TarihBicimleri, Turkce, DateTimeStyles.None, out tarih))
                {
                    tarih = tarih.Date;
                    return true;
                }

                if (DateTime.TryParse(metin, Turkce, DateTimeStyles.None, out tarih))
                {
                    tarih = tarih.Date;
                    return true;
                }
            }

            if (hucre.Sayi is { } seri && seri >= EnKucukSeri && seri <= EnBuyukSeri)
            {
                try
                {
                    tarih = DateTime.FromOADate(seri).Date;
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// "26/08/2026-14:58:47" → "26/08/2026". Saat ayracı olarak boşluk veya tire kabul
        /// edilir; "2026-08-26" gibi tire ayraçlı bir tarih bölünmesin diye kesme noktasından
        /// önce tarih ayracı (nokta veya eğik çizgi) aranır.
        /// </summary>
        private static string TarihParcasi(string metin)
        {
            var kesme = metin.IndexOfAny(SaatAyraclari);
            if (kesme <= 0) return metin;

            var bas = metin[..kesme];
            return bas.Contains('.') || bas.Contains('/') ? bas.Trim() : metin;
        }

        private static readonly char[] SaatAyraclari = { ' ', '-' };

        /// <summary>
        /// Tutar. Sayısal hücre doğrudan; metin hücrede önce tr-TR (1.234,56), sonra
        /// invariant (1,234.56) biçimi denenir. İşaret korunur — yön kararını çağıran verir.
        /// </summary>
        public static bool Tutar(TabloHucresi hucre, out decimal tutar)
        {
            tutar = 0m;

            if (hucre.Sayi is { } sayi)
            {
                try
                {
                    tutar = (decimal)sayi;
                    return true;
                }
                catch (OverflowException)
                {
                    return false;
                }
            }

            var ham = hucre.Metin;
            if (ham.Length == 0) return false;

            var temiz = ham.Replace("TL", string.Empty, StringComparison.OrdinalIgnoreCase)
                           .Replace("TRY", string.Empty, StringComparison.OrdinalIgnoreCase)
                           .Replace("₺", string.Empty)
                           .Replace(" ", string.Empty)
                           .Replace(" ", string.Empty)
                           .Trim();

            if (temiz.Length == 0) return false;

            const NumberStyles stil = NumberStyles.Number | NumberStyles.AllowLeadingSign;

            if (decimal.TryParse(temiz, stil, Turkce, out tutar)) return true;
            return decimal.TryParse(temiz, stil, CultureInfo.InvariantCulture, out tutar);
        }
    }
}
