using System.Globalization;
using CatalogService.Api.Features.BankaEkstre.Domain;
using ClosedXML.Excel;

namespace CatalogService.Api.Features.BankaEkstre.Services.Parsing
{
    /// <summary>
    /// Vakıfbank vadesiz TL hesap ekstresi (xlsx).
    ///
    /// Ölçülmüş dosya yapısı: başlık blokları var, veri 8. satırdan başlıyor.
    /// Kolonlar önce başlık satırından isimle aranır; bulunamazsa ölçülen sabit
    /// indekslere düşülür ve durum <see cref="EkstreParseSonuc.Uyarilar"/> ile raporlanır
    /// (banka kolon sırası değiştirdiğinde sessizce yanlış veri okunmasın diye).
    /// </summary>
    public class VakifbankVadesizParser : IEkstreParser
    {
        public const string Tip = "VAKIFBANK_VADESIZ";

        public string ParserTipi => Tip;
        public string Ad => "Vakıfbank — Vadesiz TL";

        /// <summary>Ölçülen veri başlangıcı (1 tabanlı Excel satır numarası).</summary>
        private const int VarsayilanIlkVeriSatiri = 8;

        // Ölçülen 0 tabanlı kolon indeksleri (Excel'de +1).
        private const int IdxTarih = 2;
        private const int IdxIslemTipi = 5;
        private const int IdxTutar = 6;
        private const int IdxKanal = 8;
        private const int IdxVkn = 14;
        private const int IdxBorcAlacak = 15;
        private const int IdxAciklama = 16;

        private static readonly string[] BaslikTarih = { "Tarih", "İşlem Tarihi", "Islem Tarihi", "Valör Tarihi" };
        private static readonly string[] BaslikIslemTipi = { "İşlem Tipi", "Islem Tipi", "İşlem Türü", "Islem Turu", "İşlem Adı" };
        private static readonly string[] BaslikTutar = { "Tutar", "İşlem Tutarı", "Islem Tutari" };
        private static readonly string[] BaslikKanal = { "Kanal", "İşlem Kanalı", "Islem Kanali" };
        private static readonly string[] BaslikVkn = { "VKN", "Karşı VKN", "Karsi VKN", "VKN/TCKN", "Vergi No" };
        private static readonly string[] BaslikBorcAlacak = { "B/A", "BA", "Borç/Alacak", "Borc/Alacak" };
        private static readonly string[] BaslikAciklama = { "Açıklama", "Aciklama", "İşlem Açıklaması" };

        public EkstreParseSonuc Ayristir(Stream dosya)
        {
            var sonuc = new EkstreParseSonuc();

            using var kitap = new XLWorkbook(dosya);
            var sayfa = kitap.Worksheets.FirstOrDefault()
                        ?? throw new InvalidDataException("Excel dosyasında sayfa bulunamadı.");

            var (kolonlar, ilkVeriSatiri) = KolonlariBul(sayfa, sonuc);
            sonuc.AciklamaKolonu = kolonlar.Aciklama;

            var sonSatir = sayfa.LastRowUsed()?.RowNumber() ?? 0;

            for (var satirNo = ilkVeriSatiri; satirNo <= sonSatir; satirNo++)
            {
                var satir = sayfa.Row(satirNo);
                if (satir.IsEmpty()) continue;

                var tarihHam = Hucre(satir, kolonlar.Tarih);
                var tutarHam = Hucre(satir, kolonlar.Tutar);

                // Tarih veya tutar okunamıyorsa satır veri değildir (ara başlık, toplam, dipnot).
                if (!TarihOku(satir, kolonlar.Tarih, out var tarih) || !TutarOku(satir, kolonlar.Tutar, out var tutar))
                {
                    if (!string.IsNullOrWhiteSpace(tarihHam) || !string.IsNullOrWhiteSpace(tutarHam))
                        sonuc.AtlananSatir++;
                    continue;
                }

                var aciklama = Hucre(satir, kolonlar.Aciklama);
                var borcAlacak = Hucre(satir, kolonlar.BorcAlacak);

                var ayrilan = new AyrilanSatir
                {
                    SiraNo = sonuc.Satirlar.Count + 1,
                    KaynakSatirNo = satirNo,
                    Tarih = tarih.Date,
                    Tutar = Math.Abs(tutar),
                    Yon = YonBul(tutar, borcAlacak),
                    IslemTipi = Hucre(satir, kolonlar.IslemTipi).Trim(),
                    HamAciklama = aciklama.Trim(),
                    Kanal = Bos(Hucre(satir, kolonlar.Kanal)),
                    // VKN kolonu KASITLI okunmuyor: ölçümde 286 satırın hepsinde aynı değer
                    // vardı (0070511435) — karşı tarafın değil, hesap sahibinin VKN'si.
                    // Doldurulsaydı ilk onaydan sonra tüm satırlar güven 1.0 ile aynı hesaba
                    // eşleşir, onaya bile düşmezdi.
                    KarsiVkn = null,
                    // IBAN kolonu yok; açıklama metninden çıkarılır. Eşleştirmede kullanılmaz
                    // (IBAN katmanı kapalı), yalnız bilgi olarak saklanır.
                    KarsiIban = Normalizasyon.IbanBul(aciklama)
                };

                sonuc.Satirlar.Add(ayrilan);
            }

            if (sonuc.Satirlar.Count == 0)
                sonuc.Uyarilar.Add("Dosyada ayrıştırılabilir satır bulunamadı. Doğru banka/hesap tipi seçildi mi?");

            return sonuc;
        }

        // ---- Kolon eşleme ----

        private sealed record KolonHaritasi(int Tarih, int IslemTipi, int Tutar, int Kanal, int Vkn, int BorcAlacak, int Aciklama);

        /// <summary>
        /// Başlık satırını isimle arar. Bulursa kolonlar isimden, veri o satırın altından başlar;
        /// bulamazsa ölçülen sabit indekslere düşer ve uyarı yazar.
        /// </summary>
        private static (KolonHaritasi Kolonlar, int IlkVeriSatiri) KolonlariBul(IXLWorksheet sayfa, EkstreParseSonuc sonuc)
        {
            var sonTaranan = Math.Min(sayfa.LastRowUsed()?.RowNumber() ?? 0, VarsayilanIlkVeriSatiri + 4);

            for (var satirNo = 1; satirNo <= sonTaranan; satirNo++)
            {
                var harita = BasliklariEsle(sayfa.Row(satirNo));
                if (harita is null) continue;

                return (harita, satirNo + 1);
            }

            sonuc.Uyarilar.Add(
                "Başlık satırı bulunamadı; ölçülen sabit kolon indekslerine düşüldü " +
                $"(tarih={IdxTarih}, işlem tipi={IdxIslemTipi}, tutar={IdxTutar}, açıklama={IdxAciklama}). " +
                "Ayrışan tarih/tutar değerlerini gözden geçirin.");

            var varsayilan = new KolonHaritasi(
                IdxTarih + 1, IdxIslemTipi + 1, IdxTutar + 1, IdxKanal + 1, IdxVkn + 1, IdxBorcAlacak + 1, IdxAciklama + 1);

            return (varsayilan, VarsayilanIlkVeriSatiri);
        }

        /// <summary>
        /// Bir satırı başlık satırı olarak yorumlamayı dener. Tarih + tutar + açıklama
        /// üçlüsü bulunamazsa bu satır başlık değildir.
        /// </summary>
        private static KolonHaritasi? BasliklariEsle(IXLRow satir)
        {
            var harita = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var hucre in satir.CellsUsed())
            {
                var metin = hucre.GetString().Trim();
                if (metin.Length == 0) continue;
                harita.TryAdd(metin, hucre.Address.ColumnNumber);
            }

            if (harita.Count == 0) return null;

            var tarih = Ara(harita, BaslikTarih);
            var tutar = Ara(harita, BaslikTutar);
            var aciklama = Ara(harita, BaslikAciklama);

            if (tarih is null || tutar is null || aciklama is null) return null;

            return new KolonHaritasi(
                tarih.Value,
                Ara(harita, BaslikIslemTipi) ?? IdxIslemTipi + 1,
                tutar.Value,
                Ara(harita, BaslikKanal) ?? IdxKanal + 1,
                Ara(harita, BaslikVkn) ?? IdxVkn + 1,
                Ara(harita, BaslikBorcAlacak) ?? IdxBorcAlacak + 1,
                aciklama.Value);
        }

        private static int? Ara(Dictionary<string, int> harita, string[] adaylar)
        {
            foreach (var ad in adaylar)
                if (harita.TryGetValue(ad, out var kolon)) return kolon;
            return null;
        }

        // ---- Hücre okuma ----

        private static string Hucre(IXLRow satir, int kolon)
            => kolon <= 0 ? string.Empty : satir.Cell(kolon).GetString();

        private static string? Bos(string? deger)
            => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();

        private static bool TarihOku(IXLRow satir, int kolon, out DateTime tarih)
        {
            tarih = default;
            if (kolon <= 0) return false;

            var hucre = satir.Cell(kolon);

            // Excel tarih hücresi ise doğrudan al; metinse Türkçe biçimleri dene.
            if (hucre.DataType == XLDataType.DateTime && hucre.TryGetValue<DateTime>(out var dt))
            {
                tarih = dt;
                return true;
            }

            var metin = hucre.GetString().Trim();
            if (metin.Length == 0) return false;

            // "01.02.2026 14:33" gibi saatli değerlerde tarih kısmı yeterli.
            var bicimler = new[]
            {
                "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd",
                "dd.MM.yyyy HH:mm", "dd.MM.yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "yyyy-MM-dd HH:mm:ss"
            };

            var kultur = CultureInfo.GetCultureInfo("tr-TR");
            if (DateTime.TryParseExact(metin, bicimler, kultur, DateTimeStyles.None, out tarih)) return true;
            return DateTime.TryParse(metin, kultur, DateTimeStyles.None, out tarih);
        }

        /// <summary>
        /// Tutar. Sayısal hücre doğrudan okunur; metin hücrede önce tr-TR (1.234,56),
        /// sonra invariant (1,234.56) biçimi denenir. Sayısal hücreyi metne çevirip
        /// ayrıştırmak "12500.75" değerini 1250075 yapardı.
        /// </summary>
        private static bool TutarOku(IXLRow satir, int kolon, out decimal tutar)
        {
            tutar = 0m;
            if (kolon <= 0) return false;

            var hucre = satir.Cell(kolon);
            if (hucre.DataType == XLDataType.Number && hucre.TryGetValue<decimal>(out var sayisal))
            {
                tutar = sayisal;
                return true;
            }

            var ham = hucre.GetString();
            if (string.IsNullOrWhiteSpace(ham)) return false;

            var temiz = ham.Replace("TL", string.Empty, StringComparison.OrdinalIgnoreCase)
                           .Replace("TRY", string.Empty, StringComparison.OrdinalIgnoreCase)
                           .Replace(" ", string.Empty)
                           .Replace(" ", string.Empty)
                           .Trim();

            if (temiz.Length == 0) return false;

            var kultur = CultureInfo.GetCultureInfo("tr-TR");
            const NumberStyles stil = NumberStyles.Number | NumberStyles.AllowLeadingSign;

            if (decimal.TryParse(temiz, stil, kultur, out tutar)) return true;
            // Dosya İngilizce biçimle üretilmişse (1,234.56) ikinci deneme.
            return decimal.TryParse(temiz, stil, CultureInfo.InvariantCulture, out tutar);
        }

        /// <summary>
        /// Yön öncelikle tutarın işaretinden gelir (ölçümde tutar işaretli geliyordu).
        /// İşaret yoksa B/A kolonuna bakılır: "B" borç = çıkan, "A" alacak = giren.
        /// </summary>
        private static Yon YonBul(decimal tutar, string? borcAlacak)
        {
            if (tutar < 0m) return Yon.Cikan;
            if (tutar > 0m && !string.IsNullOrWhiteSpace(borcAlacak))
            {
                var ba = borcAlacak.Trim();
                // İşaretsiz tutarda B/A kolonu tek belirleyicidir.
                if (ba.StartsWith("B", StringComparison.OrdinalIgnoreCase)) return Yon.Cikan;
                if (ba.StartsWith("A", StringComparison.OrdinalIgnoreCase)) return Yon.Giren;
            }
            return tutar < 0m ? Yon.Cikan : Yon.Giren;
        }
    }
}
