using System.Globalization;
using CatalogService.Api.Features.BankaEkstre.Domain;
using ClosedXML.Excel;

namespace CatalogService.Api.Features.BankaEkstre.Services.Parsing
{
    /// <summary>
    /// Vakıfbank vadesiz TL hesap ekstresi (xlsx).
    ///
    /// Ölçülmüş dosya yapısı (gerçek dosya): 1–5. satırlar hesap künyesi (birleştirilmiş
    /// hücreler), 6. satır boş, <b>7. satır kolon başlıkları</b>, veri 8'den başlar.
    /// Başlıklar 1 tabanlı sırayla:
    /// HESAP NO, FİŞ NO, HAREKET TARIH, İŞLEM TARİHİ, KART NO, İŞLEM, TUTAR, BAKİYE,
    /// KANAL, İŞLEM NO, REFERANS, HAVALE, REF NO, TCKN, VKN, B/A, AÇIKLAMA.
    ///
    /// Kolonlar başlık satırından <b>Türkçe sadeleştirilmiş</b> adla aranır. Ordinal
    /// karşılaştırma yetmiyordu: "AÇIKLAMA" ile "Açıklama" <c>OrdinalIgnoreCase</c>
    /// altında bile eşleşmiyor, çünkü invariant kültür 'ı' (U+0131) → 'I' ve 'i' → 'İ'
    /// (U+0130) dönüşümünü yapmaz. Aynı sebeple "İŞLEM TARİHİ" ile "İşlem Tarihi" de
    /// eşleşmiyordu; başlık bulunamıyor ve sessizce sabit indekslere düşülüyordu.
    ///
    /// Başlık yine de bulunamazsa ölçülen sabit indekslere düşülür ve taranan satırlarda
    /// <b>ne görüldüğü</b> <see cref="EkstreParseSonuc.Uyarilar"/> ile raporlanır.
    /// </summary>
    public class VakifbankVadesizParser : IEkstreParser
    {
        public const string Tip = "VAKIFBANK_VADESIZ";

        public string ParserTipi => Tip;
        public string Ad => "Vakıfbank — Vadesiz TL";

        /// <summary>Ölçülen veri başlangıcı (1 tabanlı Excel satır numarası).</summary>
        private const int VarsayilanIlkVeriSatiri = 8;

        // Ölçülen 0 tabanlı kolon indeksleri (Excel'de +1).
        // Tarih = İŞLEM TARİHİ (3), HAREKET TARIH (2) değil: ikincisi saat de içeriyor.
        private const int IdxTarih = 3;
        private const int IdxIslemTipi = 5;
        private const int IdxTutar = 6;
        private const int IdxKanal = 8;
        private const int IdxVkn = 14;
        private const int IdxBorcAlacak = 15;
        private const int IdxAciklama = 16;

        // Adaylar sırayla denenir, ilk bulunan kazanır. Sıra önemli: gerçek dosyada hem
        // "İŞLEM TARİHİ" hem "HAREKET TARIH" var; saatsiz olan tercih edilir.
        private static readonly string[] BaslikTarih =
            { "İşlem Tarihi", "Tarih", "Valör Tarihi", "Hareket Tarih", "Hareket Tarihi" };

        // Gerçek dosyada kolon yalnız "İŞLEM" yazıyor. "İşlem No" ayrı bir kolon olduğu
        // için karışma yok: eşleşme tam ad üzerinden, önek araması yapılmıyor.
        private static readonly string[] BaslikIslemTipi =
            { "İşlem Tipi", "İşlem Türü", "İşlem Adı", "İşlem" };

        private static readonly string[] BaslikTutar = { "Tutar", "İşlem Tutarı" };
        private static readonly string[] BaslikKanal = { "Kanal", "İşlem Kanalı" };
        private static readonly string[] BaslikVkn = { "VKN", "Karşı VKN", "VKN/TCKN", "Vergi No" };
        private static readonly string[] BaslikBorcAlacak = { "B/A", "BA", "Borç/Alacak" };
        private static readonly string[] BaslikAciklama = { "Açıklama", "İşlem Açıklaması" };

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
            var gorulenler = new List<string>();

            for (var satirNo = 1; satirNo <= sonTaranan; satirNo++)
            {
                var satir = sayfa.Row(satirNo);
                var harita = BasliklariEsle(satir);
                if (harita is not null) return (harita, satirNo + 1);

                gorulenler.Add($"  satır {satirNo}: {SatirOzeti(satir)}");
            }

            // Bir dahaki sefere tahmin edilmesin diye taranan satırlarda ne görüldüğü yazılır.
            sonuc.Uyarilar.Add(
                "Başlık satırı bulunamadı; ölçülen sabit kolon indekslerine düşüldü " +
                $"(tarih={IdxTarih}, işlem tipi={IdxIslemTipi}, tutar={IdxTutar}, açıklama={IdxAciklama}). " +
                "Başlık satırı sayılması için tarih + tutar + açıklama kolonlarının üçü birden " +
                "tanınmalı. Taranan satırlarda görülen metinler:" + Environment.NewLine +
                string.Join(Environment.NewLine, gorulenler));

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
            // Anahtarlar Türkçe sadeleştirilmiş, tek boşluklu ve kırpılmış hâlde tutulur;
            // adaylar da aynı işlemden geçtiği için "AÇIKLAMA" ile "Açıklama" eşleşir.
            var harita = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var hucre in satir.CellsUsed())
            {
                var metin = Normalizasyon.MetinNormalize(hucre.GetString());
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
                if (harita.TryGetValue(Normalizasyon.MetinNormalize(ad), out var kolon)) return kolon;
            return null;
        }

        /// <summary>Uyarıya yazılacak satır özeti: dolu hücrelerin ham metinleri.</summary>
        private static string SatirOzeti(IXLRow satir)
        {
            var parcalar = satir.CellsUsed()
                .Select(h => h.GetString().Trim())
                .Where(m => m.Length > 0)
                .Take(20)
                .ToList();

            return parcalar.Count == 0 ? "(boş)" : string.Join(" | ", parcalar);
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
        /// Yön <b>önce B/A kolonundan</b> okunur; kolon yoksa tutarın işaretine düşülür.
        ///
        /// Gerçek dosyada iki sinyal de var ve tam uyumlu: 173 "A" satırının hepsinde tutar
        /// pozitif ve bakiye artıyor, 114 "B" satırının hepsinde tutar negatif ve bakiye
        /// azalıyor (bakiye farkının mutlak değeri her satırda tutara eşit). Yani
        /// <c>A = alacak = giren</c>, <c>B = borç = çıkan</c> — veriden doğrulandı, varsayılmadı.
        ///
        /// Öncelik B/A'da: işaret kullanmayan bir ekstre biçiminde tüm satırlar "giren"
        /// okunur ve 120/329 kararı tamamen ters giderdi.
        /// </summary>
        private static Yon YonBul(decimal tutar, string? borcAlacak)
        {
            if (!string.IsNullOrWhiteSpace(borcAlacak))
            {
                var ba = Normalizasyon.TurkceSadelestir(borcAlacak).Trim();
                if (ba.StartsWith("B", StringComparison.Ordinal)) return Yon.Cikan;
                if (ba.StartsWith("A", StringComparison.Ordinal)) return Yon.Giren;
            }

            return tutar < 0m ? Yon.Cikan : Yon.Giren;
        }
    }
}
