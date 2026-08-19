using CatalogService.Api.Features.BankaEkstre.Domain;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    /// <summary>Bir katmanın ürettiği karşı hesap önerisi.</summary>
    public class EslestirmeSonuc
    {
        public string? HesapKodu { get; set; }
        public string? HesapAdi { get; set; }
        public decimal Guven { get; set; }
        public KaynakKatman Katman { get; set; } = KaynakKatman.Yok;

        /// <summary>Yakın ikinci aday (yalnız Katman 5'te dolabilir).</summary>
        public string? IkinciAdayKodu { get; set; }
        public string? IkinciAdayAdi { get; set; }
        public decimal? IkinciAdaySkoru { get; set; }

        /// <summary>Satırın alacağı durum: Otomatik / OnayBekliyor / Cozulemedi.</summary>
        public SatirDurum Durum { get; set; } = SatirDurum.Cozulemedi;
    }

    /// <summary>Eşleştiricinin ihtiyaç duyduğu, satır başına değişmeyen veriler.</summary>
    public class EslestirmeVerisi
    {
        public IReadOnlyList<OgrenmeKaydi> OgrenmeKayitlari { get; init; } = Array.Empty<OgrenmeKaydi>();
        public IReadOnlyList<BankaHesabi> BankaHesaplari { get; init; } = Array.Empty<BankaHesabi>();
        public IReadOnlyList<SabitKural> SabitKurallar { get; init; } = Array.Empty<SabitKural>();
        public IReadOnlyList<HesapPlaniKaydi> HesapPlani { get; init; } = Array.Empty<HesapPlaniKaydi>();

        /// <summary>Ekstresi işlenen hesabın kendisi; kendi kendine eşleşmesin diye elenir.</summary>
        public int IslenenBankaHesabiId { get; init; }
    }

    public interface IHesapEslestirici
    {
        EslestirmeSonuc Coz(SatirBaglami baglam, EslestirmeVerisi veri);

        /// <summary>Bankalar arası hareketlerde metinde geçen banka adını bulur (açıklama üretimi de kullanır).</summary>
        BankaHesabi? BankaBul(SatirBaglami baglam, EslestirmeVerisi veri);
    }

    /// <summary>
    /// Katmanlı karşı hesap eşleştirmesi. Katmanlar sırayla denenir, ilk çözen kazanır;
    /// hangi katmanın çözdüğü <see cref="EslestirmeSonuc.Katman"/> alanına yazılır.
    /// Belirsizlikte tahmin edilmez, satır onaya düşer.
    /// </summary>
    public class HesapEslestirici : IHesapEslestirici
    {
        /// <summary>Otomatik kabul eşiği.</summary>
        public const decimal OtomatikEsik = 0.85m;

        /// <summary>İkinci adayla bu farktan yakınsa satır onaya düşer (ölçümde iki hatanın da sebebi).</summary>
        public const decimal AdayFarki = 0.05m;

        /// <summary>Yön → ana grup. Ölçüm: giren 141/142 → 120, çıkan 33/35 → 329.</summary>
        public const string GirenAnaGrup = "120";
        public const string CikanAnaGrup = "329";

        public EslestirmeSonuc Coz(SatirBaglami baglam, EslestirmeVerisi veri)
        {
            // --- Katman 1: IBAN (öğrenilmiş) ---
            var ibanAnahtar = Normalizasyon.IbanAnahtar(baglam.KarsiIban);
            if (ibanAnahtar.Length > 0)
            {
                var kayit = OgrenmeBul(veri, AnahtarTipi.Iban, ibanAnahtar, baglam.Yon);
                if (kayit is not null) return Kesin(kayit, KaynakKatman.Iban);
            }

            // --- Katman 1b: VKN (öğrenilmiş) ---
            var vknAnahtar = Normalizasyon.VknAnahtar(baglam.KarsiVkn);
            if (vknAnahtar.Length > 0)
            {
                var kayit = OgrenmeBul(veri, AnahtarTipi.Vkn, vknAnahtar, baglam.Yon);
                if (kayit is not null) return Kesin(kayit, KaynakKatman.Vkn);
            }

            // --- Katman 2: Geçmiş onay (normalize açıklama hash'i) ---
            var hash = Normalizasyon.AciklamaHash(baglam.HamAciklama);
            if (hash.Length > 0)
            {
                var kayit = OgrenmeBul(veri, AnahtarTipi.AciklamaHash, hash, baglam.Yon);
                if (kayit is not null) return Kesin(kayit, KaynakKatman.GecmisOnay);
            }

            // --- Katman 3: Banka kayıt defteri ---
            // Ölçümde en yüksek getirili katman (174 satırın 54'ü), hiç cari eşleştirmesi gerekmeden.
            var banka = BankaBul(baglam, veri);
            if (banka is not null && !string.IsNullOrWhiteSpace(banka.OrkaHesapKodu))
            {
                return new EslestirmeSonuc
                {
                    HesapKodu = banka.OrkaHesapKodu,
                    HesapAdi = banka.BankaAdi,
                    Guven = 0.95m,
                    Katman = KaynakKatman.BankaKayitDefteri,
                    Durum = SatirDurum.Otomatik
                };
            }

            // --- Katman 4: Sabit kural tablosu ---
            var kural = KuralBul(baglam, veri);
            if (kural is not null)
            {
                return new EslestirmeSonuc
                {
                    HesapKodu = kural.HesapKodu,
                    HesapAdi = kural.HesapAdi,
                    Guven = kural.Guven,
                    Katman = KaynakKatman.SabitKural,
                    Durum = SatirDurum.Otomatik
                };
            }

            // --- Katman 5: Unvan benzerliği ---
            return UnvanaGoreCoz(baglam, veri);
        }

        /// <summary>
        /// Bankalar arası hareketlerde karşı bankayı bulur. Önce IBAN (kesin),
        /// sonra metinde geçen banka adı (en uzun eşleşme kazanır — "Vakıfbank" ile
        /// "Vakıfbank Yatırım" karışmasın).
        /// </summary>
        public BankaHesabi? BankaBul(SatirBaglami baglam, EslestirmeVerisi veri)
        {
            if (baglam.Sablon?.BankalarArasi != true) return null;

            var adaylar = veri.BankaHesaplari
                .Where(h => h.Aktif && h.Id != veri.IslenenBankaHesabiId)
                .ToList();

            if (adaylar.Count == 0) return null;

            var ibanAnahtar = Normalizasyon.IbanAnahtar(baglam.KarsiIban);
            if (ibanAnahtar.Length > 0)
            {
                var ibanEsi = adaylar.FirstOrDefault(h =>
                    Normalizasyon.IbanAnahtar(h.Iban) == ibanAnahtar);
                if (ibanEsi is not null) return ibanEsi;
            }

            var metin = Normalizasyon.TurkceSadelestir(baglam.HamAciklama + " " + baglam.IslemTipi);
            if (metin.Length == 0) return null;

            return adaylar
                .Where(h => !string.IsNullOrWhiteSpace(h.BankaAdi))
                .Select(h => new { Hesap = h, Ad = Normalizasyon.TurkceSadelestir(h.BankaAdi) })
                .Where(x => x.Ad.Length >= 3 && metin.Contains(x.Ad, StringComparison.Ordinal))
                .OrderByDescending(x => x.Ad.Length)
                .Select(x => x.Hesap)
                .FirstOrDefault();
        }

        private static EslestirmeSonuc Kesin(OgrenmeKaydi kayit, KaynakKatman katman) => new()
        {
            HesapKodu = kayit.HesapKodu,
            HesapAdi = kayit.HesapAdi,
            Guven = 1.0m,
            Katman = katman,
            Durum = SatirDurum.Otomatik
        };

        private static OgrenmeKaydi? OgrenmeBul(EslestirmeVerisi veri, AnahtarTipi tip, string anahtar, Yon yon)
            => veri.OgrenmeKayitlari.FirstOrDefault(o =>
                   o.AnahtarTipi == tip &&
                   o.Yon == yon &&
                   string.Equals(o.Anahtar, anahtar, StringComparison.Ordinal));

        private static SabitKural? KuralBul(SatirBaglami baglam, EslestirmeVerisi veri)
        {
            var hedef = Normalizasyon.TurkceSadelestir(baglam.IslemTipi).Trim();

            foreach (var kural in veri.SabitKurallar.Where(k => k.Aktif).OrderBy(k => k.Sira))
            {
                if (kural.Yon is Yon y && y != baglam.Yon) continue;

                var desen = Normalizasyon.TurkceSadelestir(kural.IslemTipiDeseni).Trim();
                if (desen.Length == 0) continue;

                var uyuyor = kural.EslesmeTuru switch
                {
                    EslesmeTuru.Tam => string.Equals(hedef, desen, StringComparison.Ordinal),
                    EslesmeTuru.Icerir => hedef.Contains(desen, StringComparison.Ordinal),
                    EslesmeTuru.Regex => System.Text.RegularExpressions.Regex.IsMatch(
                        baglam.IslemTipi, kural.IslemTipiDeseni,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase,
                        TimeSpan.FromMilliseconds(250)),
                    _ => false
                };

                if (uyuyor) return kural;
            }

            return null;
        }

        /// <summary>
        /// Katman 5. Arama uzayı önce yön → ana grup, sonra unvanın ilk harfi ile daraltılır;
        /// harf daraltmasında aday bulunamazsa tüm gruba genişletilir.
        /// </summary>
        private static EslestirmeSonuc UnvanaGoreCoz(SatirBaglami baglam, EslestirmeVerisi veri)
        {
            var normalUnvan = Normalizasyon.UnvanNormalize(baglam.Unvan);
            if (normalUnvan.Length == 0)
                return new EslestirmeSonuc { Durum = SatirDurum.Cozulemedi, Katman = KaynakKatman.Yok };

            var anaGrup = AnaGrupBul(baglam.Yon);

            var grup = veri.HesapPlani
                .Where(h => h.Aktif && string.Equals(h.AnaGrup, anaGrup, StringComparison.Ordinal))
                .ToList();

            if (grup.Count == 0)
                return new EslestirmeSonuc { Durum = SatirDurum.Cozulemedi, Katman = KaynakKatman.Yok };

            var ilkHarf = Normalizasyon.IlkHarf(normalUnvan);

            var daraltilmis = ilkHarf is null
                ? grup
                : grup.Where(h => string.Equals(h.BaslangicHarfi, ilkHarf, StringComparison.Ordinal)).ToList();

            if (daraltilmis.Count == 0) daraltilmis = grup;

            var siralanmis = daraltilmis
                .Select(h => new { Kayit = h, Skor = Benzerlik.Oran(normalUnvan, h.NormalizeAd) })
                .OrderByDescending(x => x.Skor)
                .ThenBy(x => x.Kayit.Kod, StringComparer.Ordinal)
                .Take(2)
                .ToList();

            if (siralanmis.Count == 0)
                return new EslestirmeSonuc { Durum = SatirDurum.Cozulemedi, Katman = KaynakKatman.Yok };

            var enIyi = siralanmis[0];
            var ikinci = siralanmis.Count > 1 ? siralanmis[1] : null;

            var sonuc = new EslestirmeSonuc
            {
                HesapKodu = enIyi.Kayit.Kod,
                HesapAdi = enIyi.Kayit.Ad,
                Guven = enIyi.Skor,
                Katman = KaynakKatman.UnvanBenzerligi
            };

            var yakinIkinci = ikinci is not null && enIyi.Skor - ikinci.Skor < AdayFarki;

            if (yakinIkinci)
            {
                // Ölçümdeki iki yüksek güvenli hatanın ikisi de "aynı unvan ailesinden
                // birden fazla cari" tipindeydi; bu yüzden yakın aday varsa otomatik yok.
                sonuc.IkinciAdayKodu = ikinci!.Kayit.Kod;
                sonuc.IkinciAdayAdi = ikinci.Kayit.Ad;
                sonuc.IkinciAdaySkoru = ikinci.Skor;
            }

            sonuc.Durum = enIyi.Skor >= OtomatikEsik && !yakinIkinci
                ? SatirDurum.Otomatik
                : SatirDurum.OnayBekliyor;

            return sonuc;
        }

        /// <summary>
        /// Yön ana grubu belirler. İstisna satırlar (giren olup 329'a giden gibi) burada
        /// sessizce ters yöne yazılmaz; eşleşme tutmazsa satır onaya düşer.
        /// </summary>
        public static string AnaGrupBul(Yon yon) => yon == Yon.Giren ? GirenAnaGrup : CikanAnaGrup;
    }
}
