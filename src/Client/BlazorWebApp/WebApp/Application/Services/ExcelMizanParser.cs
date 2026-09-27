using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;
using WebApp.Application.Services.Interfaces;
using WebApp.Domain.Models.FirmaKontrol;

namespace WebApp.Application.Services
{
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

                int rowIndex = 0;
                while (reader.Read())
                {
                    rowIndex++;
                    if (rowIndex == 1) continue; // başlık satırı

                    if (reader.FieldCount < 1) continue;

                    var rawKod = SafeGetString(reader, 0);
                    if (string.IsNullOrWhiteSpace(rawKod)) continue; // boş satır - sessiz atla

                    var kod = NormalizeKod(rawKod);
                    var ad = ReadAd(reader);
                    var bakiye = ReadBakiye(reader);

                    if (!IsAnaHesapKodu(kod))
                    {
                        // Alt kırılım ("600 1", "120 1 A08"): artık ATILMIYOR, ayrı ağaç
                        // listesine yazılıyor. Ana hesap sözlüğüne (result.Rows) GİRMEZ —
                        // aynı para ikinci kez sayılmasın diye. İlk parçası üç haneli sayı
                        // olmayan satırlar (başlık, toplam, boş) eskisi gibi atlanır.
                        if (!DugumEkle(result, kod, ad, reader, bakiye))
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
                    // A sütununda sayfa numarası (örn. "186") görünebilir; hesap değildir.
                    if (IsMizanToplamSatiri(reader))
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
                    DugumEkle(result, kod, ad, reader, bakiye);

                    // MockFirmaKontrolService.UpdateMizanFromExcelAsync, "CariDonem ?? OncekiDonem"
                    // okuyup Donem parametresine göre hedef döneme yazıyor; tek alan yeterli.
                    result.Rows.Add(new MizanExcelRow
                    {
                        Kod = kod,
                        Ad = ad,
                        CariDonem = bakiye
                    });
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Excel okunurken hata: {ex.Message}");
            }

            return Task.FromResult(result);
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
            MizanParseResult result, string kod, string? ad, IExcelDataReader reader, decimal? bakiye)
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

            var borc = ParseDecimal(SafeGetCell(reader, 5)) ?? 0m;
            var alacak = ParseDecimal(SafeGetCell(reader, 6)) ?? 0m;

            var dugum = HesapDugumu.Olustur(kod, ad, borc, alacak, bakiye);
            if (dugum is null) return false;

            result.HesapDugumleri.Add(dugum);
            return true;
        }

        // PROGROUP formatında D (3) ve E (4) borç/alacak toplamları, F (5) ve G (6)
        // borç/alacak bakiyeleridir. Mizan denklik kontrolü olarak son satırda
        // bu dört değer ikili-ikili eşit gelir (mizan tutar). Bu, hesap değil,
        // genel toplam satırıdır; ham olarak ayıklanmalıdır.
        private static bool IsMizanToplamSatiri(IExcelDataReader reader)
        {
            if (reader.FieldCount <= 6) return false;

            var borcToplam = ParseDecimal(SafeGetCell(reader, 3));
            var alacakToplam = ParseDecimal(SafeGetCell(reader, 4));

            if (!borcToplam.HasValue || !alacakToplam.HasValue) return false;
            if (borcToplam.Value <= 0m || alacakToplam.Value <= 0m) return false;

            const decimal eps = 0.01m;
            if (Math.Abs(borcToplam.Value - alacakToplam.Value) >= eps) return false;

            var borcBakiye = ParseDecimal(SafeGetCell(reader, 5)) ?? 0m;
            var alacakBakiye = ParseDecimal(SafeGetCell(reader, 6)) ?? 0m;

            return Math.Abs(borcBakiye - alacakBakiye) < eps;
        }

        // PROGROUP formatında B (index 1) hesap adı; basit formatta sayısal bakiye.
        // ParseDecimal başarılı oluyorsa basit formattır → ad yok.
        private static string? ReadAd(IExcelDataReader reader)
        {
            if (reader.FieldCount < 2) return null;

            var cell = SafeGetCell(reader, 1);
            if (cell is null) return null;
            if (ParseDecimal(cell).HasValue) return null;

            var s = cell.ToString()?.Trim();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        // PROGROUP formatı (8+ kolon): F (5)=Borç Bakiye, G (6)=Alacak Bakiye, H (7)=Bakiye
        // Basit format (2 kolon): B (1)=Bakiye
        private static decimal? ReadBakiye(IExcelDataReader reader)
        {
            if (reader.FieldCount > 7)
            {
                var hesaplanmis = ParseDecimal(SafeGetCell(reader, 7));
                if (hesaplanmis.HasValue) return hesaplanmis;
            }

            if (reader.FieldCount > 6)
            {
                var borc = ParseDecimal(SafeGetCell(reader, 5)) ?? 0m;
                var alacak = ParseDecimal(SafeGetCell(reader, 6)) ?? 0m;
                if (borc != 0m || alacak != 0m) return borc - alacak;
            }

            if (reader.FieldCount > 1)
            {
                return ParseDecimal(SafeGetCell(reader, 1));
            }

            return null;
        }

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
                if (reader.IsDBNull(idx)) return null;
                var val = reader.GetValue(idx);
                return val?.ToString();
            }
            catch { return null; }
        }

        private static object? SafeGetCell(IExcelDataReader reader, int idx)
        {
            try
            {
                if (reader.IsDBNull(idx)) return null;
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
