using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;
using WebApp.Application.Services.Interfaces;
using WebApp.Domain.Models.FirmaKontrol;

namespace WebApp.Application.Services
{
    /// <summary>
    /// Mizan Excel okuyucusu. Sütunlar SIRAYLA değil BAŞLIK ADIYLA bulunur (MizanBasliklari):
    /// başlık satırı ilk 10 satırda "Hesap Kodu" içeren satırdır; ilk sayfada yoksa sonraki
    /// sayfalara bakılır. Zorunlu başlık eksikse sabit sıraya düşülmez, hata döner.
    ///
    /// Bakiye = TL Borç Bakiye − TL Alacak Bakiye (yoksa Borç/Alacak Bakiye). Döviz sütunları
    /// hiçbir zaman bakiye sayılmaz; "Bakiye" sütunu yalnız basit biçimde okunur.
    /// </summary>
    public class ExcelMizanParser : IExcelMizanParser
    {
        private static int _encodingRegistered;

        // Sadece tam 3 haneli numerik ana hesap kodları kabul edilir.
        // Hiyerarşik alt kırılımlar ("102 1", "102 1 1 02") çift sayıma yol
        // açtığı için boşluk içeren kodlar elenir.
        private static readonly Regex AnaHesapKoduPattern = new(@"^\d{3}$", RegexOptions.Compiled);

        public ExcelMizanParser()
        {
            if (Interlocked.Exchange(ref _encodingRegistered, 1) == 0)
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }
        }

        public Task<MizanParseResult> ParseAsync(Stream excelStream)
        {
            var result = new MizanParseResult();

            try
            {
                using var reader = ExcelReaderFactory.CreateReader(excelStream);

                var sutunlar = BaslikBul(reader, out var hata);
                if (sutunlar is null)
                {
                    result.Errors.Add(hata!);
                    return Task.FromResult(result);
                }

                // Denge kontrolü yalnız üç haneli ana hesaplar üzerinden (alt kırılımlar aynı
                // paradır, çift sayılmaz).
                decimal borcToplami = 0m, alacakToplami = 0m;

                while (reader.Read())
                {
                    var rawKod = SafeGetString(reader, sutunlar.Kod);
                    if (string.IsNullOrWhiteSpace(rawKod)) continue; // boş satır - sessiz atla

                    var kod = NormalizeKod(rawKod);
                    var ad = ReadAd(reader, sutunlar);
                    var (borc, alacak, bakiye) = ReadBakiye(reader, sutunlar);

                    if (!IsAnaHesapKodu(kod))
                    {
                        // Alt kırılım ("600 1", "120 1 A08"): artık ATILMIYOR, ayrı ağaç
                        // listesine yazılıyor. Ana hesap sözlüğüne (result.Rows) GİRMEZ —
                        // aynı para ikinci kez sayılmasın diye. İlk parçası üç haneli sayı
                        // olmayan satırlar (başlık, toplam, boş) eskisi gibi atlanır.
                        if (!DugumEkle(result, kod, ad, borc, alacak, bakiye))
                        {
                            result.AtlananSatirlar.Add(new AtlananSatir
                            {
                                Kod = kod,
                                Ad = ad,
                                Bakiye = bakiye,
                                Sebep = AtlamaSebebi.GecersizFormat,
                                SebepMetni = "Geçersiz Format"
                            });
                        }
                        continue;
                    }

                    // Mizan dosyasının son satırı genelde genel toplam satırıdır:
                    // Borç Toplam = Alacak Toplam ve Borç Bakiye = Alacak Bakiye olur.
                    // Kod sütununda sayfa numarası (örn. "186", "233") görünebilir; hesap değildir.
                    if (IsMizanToplamSatiri(reader, sutunlar, ad, borc, alacak))
                    {
                        result.AtlananSatirlar.Add(new AtlananSatir
                        {
                            Kod = kod,
                            Ad = ad,
                            Bakiye = bakiye,
                            Sebep = AtlamaSebebi.MizanToplamSatiri,
                            SebepMetni = "Mizan Genel Toplam Satırı (atlandı)"
                        });
                        continue;
                    }

                    if (!bakiye.HasValue)
                    {
                        result.AtlananSatirlar.Add(new AtlananSatir
                        {
                            Kod = kod,
                            Ad = ad,
                            Bakiye = null,
                            Sebep = AtlamaSebebi.BakiyeOkunamadi,
                            SebepMetni = "Bakiye Okunamadı"
                        });
                        continue;
                    }

                    // Üç haneli ana hesap: mevcut davranış aynen sürer. Ayrıca ağacın kökü
                    // olarak düğüm listesine de eklenir — o liste hiçbir toplamda kullanılmaz.
                    DugumEkle(result, kod, ad, borc, alacak, bakiye);

                    borcToplami += borc ?? 0m;
                    alacakToplami += alacak ?? 0m;

                    // MockFirmaKontrolService.UpdateMizanFromExcelAsync, "CariDonem ?? OncekiDonem"
                    // okuyup Donem parametresine göre hedef döneme yazıyor; tek alan yeterli.
                    result.Rows.Add(new MizanExcelRow
                    {
                        Kod = kod,
                        Ad = ad,
                        CariDonem = bakiye
                    });
                }

                // Basit biçimde borç/alacak ayrımı yok; denge ölçülemez.
                if (!sutunlar.Basit && result.Rows.Count > 0)
                {
                    var fark = Math.Round(borcToplami - alacakToplami, 2);
                    if (fark != 0m)
                    {
                        var tr = new CultureInfo("tr-TR");
                        result.Uyarilar.Add(
                            $"Mizan dengesiz: ana hesaplarda borç bakiye toplamı {borcToplami.ToString("N2", tr)}, " +
                            $"alacak bakiye toplamı {alacakToplami.ToString("N2", tr)} (fark {fark.ToString("N2", tr)}).");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Excel okunurken hata: {ex.Message}");
            }

            return Task.FromResult(result);
        }

        /// <summary>
        /// Başlık satırını bulur ve okuyucuyu onun hemen altına bırakır. Sayfa sayfa, her
        /// sayfanın ilk <see cref="MizanBasliklari.BaslikArama"/> satırına bakar. "Hesap Kodu"
        /// satırı bulunur ama zorunlu başlık eksikse diğer sayfalara geçilmez, hata döner.
        /// </summary>
        private static MizanSutunlari? BaslikBul(IExcelDataReader reader, out string? hata)
        {
            do
            {
                for (var satir = 0; satir < MizanBasliklari.BaslikArama && reader.Read(); satir++)
                {
                    var hucreler = Enumerable.Range(0, reader.FieldCount)
                        .Select(i => SafeGetString(reader, i))
                        .ToList();

                    if (!MizanBasliklari.BaslikSatiriMi(hucreler)) continue;

                    return MizanBasliklari.Esle(hucreler, out hata);
                }
            }
            while (reader.NextResult());

            hata = $"Mizan başlık satırı bulunamadı: hiçbir sayfanın ilk {MizanBasliklari.BaslikArama} " +
                   "satırında \"Hesap Kodu\" başlığı yok.";
            return null;
        }

        private static bool IsAnaHesapKodu(string kod) =>
            !string.IsNullOrEmpty(kod) && AnaHesapKoduPattern.IsMatch(kod);

        /// <summary>Kırılım ağacı üst sınırı; aşılırsa fazlası alınmaz, yükleme bozulmaz.</summary>
        private const int DugumSiniri = 10_000;

        private const string SinirSebebi = "Kırılım sınırı aşıldı (ağaca alınmadı)";

        /// <summary>
        /// Satırı kırılım ağacına ekler. İlk parçası üç haneli sayı değilse hiçbir şey
        /// yapmaz ve false döner (çağıran satırı atlanan listesine yazar).
        ///
        /// Bu liste ana hesap sözlüğünden (<c>result.Rows</c>) tamamen ayrıdır; buraya
        /// eklenen hiçbir kayıt toplamlara girmez.
        /// </summary>
        private static bool DugumEkle(
            MizanParseResult result, string kod, string? ad, decimal? borc, decimal? alacak, decimal? bakiye)
        {
            var parcalar = HesapDugumu.Parcala(kod);
            if (parcalar.Length == 0 || !HesapDugumu.AnaHesapKoduMu(parcalar[0])) return false;

            if (result.HesapDugumleri.Count >= DugumSiniri)
            {
                // Sınır mesajı bir kez yazılır; kalan satırlar sessizce alınmaz.
                if (!result.AtlananSatirlar.Any(a => a.SebepMetni == SinirSebebi))
                {
                    result.AtlananSatirlar.Add(new AtlananSatir
                    {
                        Kod = kod,
                        Ad = ad,
                        Bakiye = bakiye,
                        Sebep = AtlamaSebebi.HiyerarsikAltKod,
                        SebepMetni = SinirSebebi
                    });
                }
                return true;
            }

            var dugum = HesapDugumu.Olustur(kod, ad, borc ?? 0m, alacak ?? 0m, bakiye);
            if (dugum is null) return false;

            result.HesapDugumleri.Add(dugum);
            return true;
        }

        /// <summary>
        /// Genel toplam satırı: hesap adı BOŞ ve borç/alacak toplamları (pozitif) eşit VE
        /// borç/alacak bakiyeleri eşit. Toplam sütunu olmayan biçimde (döviz sütunlu) ad boş ve
        /// bakiyeler eşit (pozitif) olmalı.
        ///
        /// Adı dolu satır hiçbir zaman toplam satırı değildir: kapanmış hesap (120 ALICILAR,
        /// borç toplamı = alacak toplamı, bakiye 0) da bu eşitlikleri sağlar ve 0 olarak yazılır.
        /// </summary>
        private static bool IsMizanToplamSatiri(
            IExcelDataReader reader, MizanSutunlari s, string? ad, decimal? borcBakiye, decimal? alacakBakiye)
        {
            if (s.Basit) return false;
            if (!string.IsNullOrWhiteSpace(ad)) return false;

            const decimal eps = 0.01m;
            var borcB = borcBakiye ?? 0m;
            var alacakB = alacakBakiye ?? 0m;

            if (s.BorcToplam is int bt && s.AlacakToplam is int at)
            {
                var borcToplam = ParseDecimal(SafeGetCell(reader, bt));
                var alacakToplam = ParseDecimal(SafeGetCell(reader, at));

                if (!borcToplam.HasValue || !alacakToplam.HasValue) return false;
                if (borcToplam.Value <= 0m || alacakToplam.Value <= 0m) return false;
                if (Math.Abs(borcToplam.Value - alacakToplam.Value) >= eps) return false;

                return Math.Abs(borcB - alacakB) < eps;
            }

            return borcB > 0m && Math.Abs(borcB - alacakB) < eps;
        }

        private static string? ReadAd(IExcelDataReader reader, MizanSutunlari s)
        {
            if (s.Ad is not int i) return null;

            var cell = SafeGetCell(reader, i);
            if (cell is null) return null;
            // Eski okuyucuyla aynı: sayıya çevrilebilen hücre ad sayılmaz.
            if (ParseDecimal(cell).HasValue) return null;

            var metin = cell.ToString()?.Trim();
            return string.IsNullOrWhiteSpace(metin) ? null : metin;
        }

        /// <summary>
        /// (borç bakiye, alacak bakiye, bakiye). Bakiye = borç − alacak. Boş hücre 0'dır (ORKA
        /// kapanmış hesapta iki bakiyeyi de boş bırakır); hücrede sayıya çevrilemeyen bir şey
        /// varsa bakiye okunamamıştır (null). Basit biçimde bakiye "Bakiye" sütunundan gelir.
        /// </summary>
        private static (decimal? Borc, decimal? Alacak, decimal? Bakiye) ReadBakiye(IExcelDataReader reader, MizanSutunlari s)
        {
            if (s.BasitBakiye is int b)
                return (null, null, ParseDecimal(SafeGetCell(reader, b)));

            var borcHucre = SafeGetCell(reader, s.BorcBakiye!.Value);
            var alacakHucre = SafeGetCell(reader, s.AlacakBakiye!.Value);
            var borc = ParseDecimal(borcHucre);
            var alacak = ParseDecimal(alacakHucre);

            if ((borc is null && !Bos(borcHucre)) || (alacak is null && !Bos(alacakHucre)))
                return (borc, alacak, null);

            return (borc, alacak, (borc ?? 0m) - (alacak ?? 0m));
        }

        private static bool Bos(object? hucre) => string.IsNullOrWhiteSpace(hucre?.ToString());

        private static string NormalizeKod(string raw)
        {
            var trimmed = raw.Trim();
            // Excel kodu "100.0" gibi geliyorsa virgül/sonraki sıfırları temizle
            if (decimal.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ||
                decimal.TryParse(trimmed, NumberStyles.Any, new CultureInfo("tr-TR"), out d))
            {
                if (d == Math.Truncate(d))
                    return ((long)d).ToString(CultureInfo.InvariantCulture);
            }
            return trimmed;
        }

        private static string? SafeGetString(IExcelDataReader reader, int idx)
        {
            try
            {
                if (idx >= reader.FieldCount || reader.IsDBNull(idx)) return null;
                var val = reader.GetValue(idx);
                return val?.ToString();
            }
            catch { return null; }
        }

        private static object? SafeGetCell(IExcelDataReader reader, int idx)
        {
            try
            {
                if (idx >= reader.FieldCount || reader.IsDBNull(idx)) return null;
                return reader.GetValue(idx);
            }
            catch { return null; }
        }

        private static decimal? ParseDecimal(object? cell)
        {
            if (cell is null) return null;
            if (cell is decimal d) return d;
            if (cell is double dbl) return (decimal)dbl;
            if (cell is float f) return (decimal)f;
            if (cell is int i) return i;
            if (cell is long l) return l;

            var s = cell.ToString();
            if (string.IsNullOrWhiteSpace(s)) return null;

            // Türkçe formatta "1.234,56" gelirse
            s = s.Trim().Replace(" ", string.Empty);

            if (decimal.TryParse(s, NumberStyles.Any, new CultureInfo("tr-TR"), out var trVal))
                return trVal;

            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var invVal))
                return invVal;

            return null;
        }
    }
}
