using CatalogService.Api.Features.BankaEkstre.Domain;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    /// <summary>Karşı hesap adayı (onay ekranında listelenir).</summary>
    public class AdayKayit
    {
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public decimal Skor { get; set; }
    }

    /// <summary>
    /// Bankalar arası bir satırda bulunan karşı banka hesabı. Aynı bankada birden fazla
    /// hesap olabildiği için sonuç her zaman tek hesaba inmez: ayırt edilemeyen adaylar
    /// <see cref="Adaylar"/> içinde döner ve satır onaya düşer.
    /// </summary>
    public sealed class BankaEslesmesi
    {
        public static readonly BankaEslesmesi Yok = new();

        /// <summary>Tek adaya inildiyse dolu.</summary>
        public BankaHesabi? Hesap { get; init; }

        /// <summary>Ayırt edilemeyen adaylar; onay ekranı hepsini seçenek olarak gösterir.</summary>
        public IReadOnlyList<BankaHesabi> Adaylar { get; init; } = Array.Empty<BankaHesabi>();

        /// <summary>Ne anahtar ne banka adı ayırt edebildi.</summary>
        public bool Belirsiz => Hesap is null && Adaylar.Count > 1;

        /// <summary>
        /// Açıklama üretiminde kullanılacak hesap. Belirsizlikte de adayların banka adı
        /// aynı olur (aynı bankanın iki hesabı), açıklama yine doğru yazılır.
        /// </summary>
        public BankaHesabi? Temsilci => Hesap ?? Adaylar.FirstOrDefault();
    }

    /// <summary>Bir katmanın ürettiği karşı hesap önerisi.</summary>
    public class EslestirmeSonuc
    {
        public string? HesapKodu { get; set; }
        public string? HesapAdi { get; set; }
        public decimal Guven { get; set; }
        public KaynakKatman Katman { get; set; } = KaynakKatman.Yok;

        /// <summary>Yakın ikinci aday (yalnız unvan benzerliği katmanında dolabilir).</summary>
        public string? IkinciAdayKodu { get; set; }
        public string? IkinciAdayAdi { get; set; }
        public decimal? IkinciAdaySkoru { get; set; }

        /// <summary>Aynı unvan ailesinden tüm adaylar; onay ekranı hepsini seçenek olarak gösterir.</summary>
        public List<AdayKayit> Adaylar { get; set; } = new();

        /// <summary>
        /// Aile tespit edildiyse çekirdeğe eklenecek ayırt edici kelime; onayda öğrenme
        /// anahtarı <c>çekirdek + ek</c> olarak yazılır.
        /// </summary>
        public string? AyirtEdiciEk { get; set; }

        /// <summary>Satırın alacağı durum: Otomatik / OnayBekliyor / Cozulemedi.</summary>
        public SatirDurum Durum { get; set; } = SatirDurum.Cozulemedi;
    }

    /// <summary>
    /// Hesap planının çıpa aramaları için ön indeksi: ana grup başına, normalize adına göre
    /// ordinal sıralı dizi. Çıpayla başlayan hesaplar ikili aramayla bulunan bitişik bir
    /// aralıktır — 6.000+ kayıt her satırda baştan taranmaz.
    ///
    /// Yükleme başına bir kez kurulur (<see cref="EslestirmeVerisi.Indeks"/>).
    /// </summary>
    public sealed class HesapPlaniIndeksi
    {
        private static readonly HesapPlaniKaydi[] Bos = Array.Empty<HesapPlaniKaydi>();

        private readonly Dictionary<string, HesapPlaniKaydi[]> _gruplar;

        private HesapPlaniIndeksi(Dictionary<string, HesapPlaniKaydi[]> gruplar) => _gruplar = gruplar;

        public static HesapPlaniIndeksi Kur(IReadOnlyList<HesapPlaniKaydi> plan)
            => new(plan.Where(h => h.Aktif && h.AnaGrup.Length > 0)
                       .GroupBy(h => h.AnaGrup, StringComparer.Ordinal)
                       .ToDictionary(g => g.Key,
                                     g => g.OrderBy(h => h.NormalizeAd, StringComparer.Ordinal).ToArray(),
                                     StringComparer.Ordinal));

        public bool GrupDolu(string anaGrup) => _gruplar.ContainsKey(anaGrup);

        /// <summary>
        /// Normalize adı verilen çıpayla başlayan hesaplar. Çıpa tek token olduğu için
        /// (boşluk içermez) "adın öneki" ile "ilk kelimenin öneki" aynı şeydir.
        /// </summary>
        public ArraySegment<HesapPlaniKaydi> CipaylaBaslayanlar(string anaGrup, string cipa)
        {
            if (cipa.Length == 0 || !_gruplar.TryGetValue(anaGrup, out var dizi))
                return new ArraySegment<HesapPlaniKaydi>(Bos);

            // Ordinal sıralı dizide bir önekle başlayan kayıtlar bitişik bir blok oluşturur;
            // blok, önekin alt sınırından başlar.
            var bas = AltSinir(dizi, cipa);
            var son = bas;
            while (son < dizi.Length && dizi[son].NormalizeAd.StartsWith(cipa, StringComparison.Ordinal))
                son++;

            return new ArraySegment<HesapPlaniKaydi>(dizi, bas, son - bas);
        }

        /// <summary>Normalize adı <paramref name="deger"/>'den küçük olmayan ilk kaydın indeksi.</summary>
        private static int AltSinir(HesapPlaniKaydi[] dizi, string deger)
        {
            var alt = 0;
            var ust = dizi.Length;

            while (alt < ust)
            {
                var orta = alt + ((ust - alt) >> 1);
                if (string.CompareOrdinal(dizi[orta].NormalizeAd, deger) < 0) alt = orta + 1;
                else ust = orta;
            }

            return alt;
        }
    }

    /// <summary>Eşleştiricinin ihtiyaç duyduğu, satır başına değişmeyen veriler.</summary>
    public class EslestirmeVerisi
    {
        /// <summary>Firma bazlı öğrenilmiş eşleşmeler (Katman 1).</summary>
        public IReadOnlyList<HesapEslesmesi> Eslesmeler { get; init; } = Array.Empty<HesapEslesmesi>();

        public IReadOnlyList<BankaHesabi> BankaHesaplari { get; init; } = Array.Empty<BankaHesabi>();
        public IReadOnlyList<SabitKural> SabitKurallar { get; init; } = Array.Empty<SabitKural>();
        public IReadOnlyList<HesapPlaniKaydi> HesapPlani { get; init; } = Array.Empty<HesapPlaniKaydi>();

        /// <summary>Ekstresi işlenen hesabın kendisi; kendi kendine eşleşmesin diye elenir.</summary>
        public int IslenenBankaHesabiId { get; init; }

        /// <summary>Ekstresi işlenen hesapta IBAN öğrenme katmanı açık mı (varsayılan kapalı).</summary>
        public bool IbanKatmaniAktif { get; init; }

        /// <summary>Ekstresi işlenen hesapta VKN öğrenme katmanı açık mı (varsayılan kapalı).</summary>
        public bool VknKatmaniAktif { get; init; }

        private HesapPlaniIndeksi? _indeks;

        /// <summary>
        /// Hesap planının çıpa indeksi; ilk kullanımda kurulur ve yükleme boyunca tekrar
        /// kullanılır. Satır başına yeniden kurulsaydı indeksin anlamı kalmazdı.
        /// </summary>
        public HesapPlaniIndeksi Indeks => _indeks ??= HesapPlaniIndeksi.Kur(HesapPlani);
    }

    public interface IHesapEslestirici
    {
        EslestirmeSonuc Coz(SatirBaglami baglam, EslestirmeVerisi veri);

        /// <summary>Bankalar arası hareketlerde karşı banka hesabını bulur (açıklama üretimi de kullanır).</summary>
        BankaHesabi? BankaBul(SatirBaglami baglam, EslestirmeVerisi veri);

        /// <summary>
        /// <see cref="BankaBul"/>'un ayrıntılı hâli: tek hesaba inilemediğinde adayları da
        /// döner. Eşleştirme bunu kullanır, açıklama üretimi tek hesapla yetinir.
        /// </summary>
        BankaEslesmesi BankaEslesmesiBul(SatirBaglami baglam, EslestirmeVerisi veri);
    }

    /// <summary>
    /// Katmanlı karşı hesap eşleştirmesi. Katmanlar sırayla denenir, ilk çözen kazanır;
    /// hangi katmanın çözdüğü <see cref="EslestirmeSonuc.Katman"/> alanına yazılır.
    /// Belirsizlikte tahmin edilmez, satır onaya düşer.
    ///
    /// Sıra: geçmiş onay → banka kayıt defteri → sabit kural → unvan benzerliği.
    /// IBAN ve VKN katmanları kod tarafında duruyor ama banka bazlı bayrakla kapalı
    /// (bkz. <see cref="BankaHesabi.IbanKatmaniAktif"/> / <see cref="BankaHesabi.VknKatmaniAktif"/>).
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

        /// <summary>Onay ekranında listelenecek en fazla aday.</summary>
        private const int EnFazlaAday = 8;

        public EslestirmeSonuc Coz(SatirBaglami baglam, EslestirmeVerisi veri)
        {
            // --- IBAN (kapalı katman) ---
            // Kullanıcı IBAN verisini düzenli tutmuyor; bayrak açılmadıkça okunmaz.
            if (veri.IbanKatmaniAktif)
            {
                var ibanAnahtar = Normalizasyon.IbanAnahtar(baglam.KarsiIban);
                if (ibanAnahtar.Length > 0)
                {
                    var kayit = EslesmeBul(veri, AnahtarTipi.Iban, ibanAnahtar, null, baglam.Yon);
                    if (kayit is not null) return Kesin(kayit, KaynakKatman.Iban);
                }
            }

            // --- VKN (kapalı katman) ---
            // Vakıfbank'ta VKN kolonu hesap sahibinin VKN'si; açık kalsaydı tüm satırlar
            // ilk onaydan sonra güven 1.0 ile aynı hesaba eşleşir ve onaya bile düşmezdi.
            if (veri.VknKatmaniAktif)
            {
                var vknAnahtar = Normalizasyon.VknAnahtar(baglam.KarsiVkn);
                if (vknAnahtar.Length > 0)
                {
                    var kayit = EslesmeBul(veri, AnahtarTipi.Vkn, vknAnahtar, null, baglam.Yon);
                    if (kayit is not null) return Kesin(kayit, KaynakKatman.Vkn);
                }
            }

            // --- Katman 1: Geçmiş onay (unvan çekirdeği) ---
            var cekirdek = AnahtarCekirdek(baglam);
            if (cekirdek.Length > 0)
            {
                // Aramada sıra: önce genişletilmiş anahtar (çekirdek + ayırt edici kelime),
                // tutmazsa sade çekirdek.
                var genisletilmis = veri.Eslesmeler
                    .Where(e => e.AnahtarTipi == AnahtarTipi.UnvanCekirdek &&
                                e.Yon == baglam.Yon &&
                                string.Equals(e.AnahtarCekirdek, cekirdek, StringComparison.Ordinal) &&
                                !string.IsNullOrWhiteSpace(e.AyirtEdiciEk))
                    .ToList();

                if (genisletilmis.Count > 0)
                {
                    var metin = Normalizasyon.MetinNormalize(baglam.HamAciklama);
                    var uyanlar = genisletilmis
                        .Where(e => TokenVarMi(metin, e.AyirtEdiciEk!))
                        .ToList();

                    if (uyanlar.Count == 1)
                    {
                        var sonucEk = Kesin(uyanlar[0], KaynakKatman.GecmisOnay);
                        sonucEk.AyirtEdiciEk = uyanlar[0].AyirtEdiciEk;
                        return sonucEk;
                    }

                    // Birden fazla aile üyesi metinde geçiyorsa tahmin edilmez: onaya düşer.
                    if (uyanlar.Count > 1)
                        return AileOnayaDusur(uyanlar, cekirdek);
                }

                var sade = EslesmeBul(veri, AnahtarTipi.UnvanCekirdek, cekirdek, null, baglam.Yon);
                if (sade is not null) return Kesin(sade, KaynakKatman.GecmisOnay);
            }

            // --- Katman 2: Banka kayıt defteri ---
            // Ölçümde en yüksek getirili katman (174 satırın 54'ü), hiç cari eşleştirmesi gerekmeden.
            var bankaEslesmesi = BankaEslesmesiBul(baglam, veri);

            if (bankaEslesmesi.Hesap is { } banka && !string.IsNullOrWhiteSpace(banka.OrkaHesapKodu))
                return BankaSonucu(banka);

            if (bankaEslesmesi.Belirsiz)
            {
                var kodlular = bankaEslesmesi.Adaylar
                    .Where(h => !string.IsNullOrWhiteSpace(h.OrkaHesapKodu))
                    .ToList();

                // Kodu girilmemiş hesaplar elenince tek aday kaldıysa belirsizlik de kalmaz.
                if (kodlular.Count == 1) return BankaSonucu(kodlular[0]);
                if (kodlular.Count > 1) return BankaOnayaDusur(kodlular);
            }

            // --- Katman 3: Sabit kural tablosu ---
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

            // --- Katman 4: Unvan benzerliği ---
            return UnvanaGoreCoz(baglam, veri);
        }

        /// <summary>
        /// Satırın öğrenme anahtarı çekirdeği: unvan varsa normalize çekirdeği, yoksa
        /// işlem tipi (banka masrafı, HGS, vergi gibi unvansız satırlar için).
        /// </summary>
        public static string AnahtarCekirdek(SatirBaglami baglam)
        {
            var cekirdek = Normalizasyon.UnvanCekirdek(baglam.Unvan);
            return cekirdek.Length > 0 ? cekirdek : Normalizasyon.IslemAnahtari(baglam.IslemTipi);
        }

        /// <summary>Açıklama üretimi için karşı banka hesabı; belirsizlikte adaylardan biri döner.</summary>
        public BankaHesabi? BankaBul(SatirBaglami baglam, EslestirmeVerisi veri)
            => BankaEslesmesiBul(baglam, veri).Temsilci;

        /// <summary>
        /// Bankalar arası hareketlerde karşı banka hesabını bulur. Sıra:
        /// <list type="number">
        /// <item>IBAN — kullanıcının kendi tanımladığı hesap IBAN'ı (öğrenilmiş veri değil).</item>
        /// <item><see cref="BankaHesabi.EslestirmeAnahtarlari"/> — hesaba özel ayırt edici ifadeler.</item>
        /// <item><see cref="BankaHesabi.BankaAdi"/> — hiçbir anahtar tutmadıysa son çare.</item>
        /// </list>
        ///
        /// Her iki metin katmanında da <b>en uzun eşleşen</b> kazanır: "Otomatik Süpürme"
        /// (16 karakter) "Vakıfbank"ı (9) yener, böylece açıklamada ikisi birden geçtiğinde
        /// süpürme hesabı seçilir.
        ///
        /// En uzun eşleşmede birden fazla hesap berabere kalırsa (aynı bankanın iki hesabı,
        /// açıklamada yalnız "Vakıfbank" geçiyor) tahmin edilmez: adaylar döner ve satır
        /// onaya düşer. Tek hesaplı bankada eskisi gibi doğrudan çözülür.
        /// </summary>
        public BankaEslesmesi BankaEslesmesiBul(SatirBaglami baglam, EslestirmeVerisi veri)
        {
            if (baglam.Sablon?.BankalarArasi != true) return BankaEslesmesi.Yok;

            var adaylar = veri.BankaHesaplari
                .Where(h => h.Aktif && h.Id != veri.IslenenBankaHesabiId)
                .ToList();

            if (adaylar.Count == 0) return BankaEslesmesi.Yok;

            var ibanAnahtar = Normalizasyon.IbanAnahtar(baglam.KarsiIban);
            if (ibanAnahtar.Length > 0)
            {
                var ibanEsi = adaylar.FirstOrDefault(h =>
                    Normalizasyon.IbanAnahtar(h.Iban) == ibanAnahtar);
                if (ibanEsi is not null) return new BankaEslesmesi { Hesap = ibanEsi };
            }

            var metin = Normalizasyon.MetinNormalize(baglam.HamAciklama + " " + baglam.IslemTipi);
            if (metin.Length == 0) return BankaEslesmesi.Yok;

            var anahtarla = EnUzunEslesenler(adaylar, metin,
                h => EslestirmeAnahtari.NormalizeAnahtarlar(h.EslestirmeAnahtarlari));
            if (anahtarla.Count > 0) return Eslesme(anahtarla);

            var adla = EnUzunEslesenler(adaylar, metin,
                h => new[] { Normalizasyon.MetinNormalize(h.BankaAdi) });

            return adla.Count == 0 ? BankaEslesmesi.Yok : Eslesme(adla);
        }

        private static BankaEslesmesi Eslesme(List<BankaHesabi> kazananlar)
            => kazananlar.Count == 1
                ? new BankaEslesmesi { Hesap = kazananlar[0] }
                : new BankaEslesmesi { Adaylar = kazananlar };

        /// <summary>
        /// Anahtarı metinde <b>en uzun</b> eşleşen hesaplar. Beraberlikte hepsi döner;
        /// hangisinin kastedildiğine karar vermek çağıranın işi.
        ///
        /// Eşleşme tam kelime sınırıyla aranır (<see cref="Normalizasyon.IfadeVarMi"/>):
        /// düz <c>Contains</c> "TEB" anahtarını "OTEBANK" içinde de bulurdu.
        /// </summary>
        private static List<BankaHesabi> EnUzunEslesenler(
            IReadOnlyList<BankaHesabi> adaylar, string metin,
            Func<BankaHesabi, IEnumerable<string>> anahtarlar)
        {
            var kazananlar = new List<BankaHesabi>();
            var enUzun = 0;

            foreach (var hesap in adaylar)
            {
                var uzunluk = anahtarlar(hesap)
                    .Where(a => a.Length >= EslestirmeAnahtari.EnKisaAnahtar &&
                                Normalizasyon.IfadeVarMi(metin, a))
                    .Select(a => a.Length)
                    .DefaultIfEmpty(0)
                    .Max();

                if (uzunluk == 0 || uzunluk < enUzun) continue;

                if (uzunluk > enUzun)
                {
                    enUzun = uzunluk;
                    kazananlar.Clear();
                }

                kazananlar.Add(hesap);
            }

            return kazananlar;
        }

        private static EslestirmeSonuc BankaSonucu(BankaHesabi banka) => new()
        {
            HesapKodu = banka.OrkaHesapKodu,
            HesapAdi = banka.BankaAdi,
            Guven = 0.95m,
            Katman = KaynakKatman.BankaKayitDefteri,
            Durum = SatirDurum.Otomatik
        };

        /// <summary>
        /// Aynı bankanın birden fazla hesabı açıklamaya uyuyor. Kod <b>önerilmez</b>:
        /// "ilk bulunanı" seçmek yanlış banka hesabına kayıt atmak demek. Kullanıcı
        /// onay ekranında adaylardan birini seçer.
        /// </summary>
        private static EslestirmeSonuc BankaOnayaDusur(IReadOnlyList<BankaHesabi> adaylar) => new()
        {
            Guven = 0m,
            Katman = KaynakKatman.BankaKayitDefteri,
            Adaylar = adaylar
                .Take(EnFazlaAday)
                .Select(h => new AdayKayit
                {
                    Kod = h.OrkaHesapKodu,
                    Ad = string.IsNullOrWhiteSpace(h.HesapAdi) ? h.BankaAdi : h.HesapAdi!,
                    Skor = 0m
                })
                .ToList(),
            Durum = SatirDurum.OnayBekliyor
        };

        private static EslestirmeSonuc Kesin(HesapEslesmesi kayit, KaynakKatman katman) => new()
        {
            HesapKodu = kayit.HesapKodu,
            HesapAdi = kayit.HesapAdi,
            Guven = 1.0m,
            Katman = katman,
            Durum = SatirDurum.Otomatik
        };

        /// <summary>
        /// Öğrenilmiş kaydın birden fazla üyesi metinde geçiyorsa hangisinin kastedildiği
        /// belli değildir; kullanıcı seçsin diye hepsi aday olarak listelenir.
        /// </summary>
        private static EslestirmeSonuc AileOnayaDusur(IReadOnlyList<HesapEslesmesi> uyanlar, string cekirdek)
        {
            var adaylar = uyanlar
                .Select(e => new AdayKayit { Kod = e.HesapKodu, Ad = e.HesapAdi ?? cekirdek, Skor = 1.0m })
                .Take(EnFazlaAday)
                .ToList();

            return new EslestirmeSonuc
            {
                HesapKodu = adaylar[0].Kod,
                HesapAdi = adaylar[0].Ad,
                Guven = 1.0m,
                Katman = KaynakKatman.GecmisOnay,
                Adaylar = adaylar,
                IkinciAdayKodu = adaylar.Count > 1 ? adaylar[1].Kod : null,
                IkinciAdayAdi = adaylar.Count > 1 ? adaylar[1].Ad : null,
                IkinciAdaySkoru = adaylar.Count > 1 ? adaylar[1].Skor : null,
                Durum = SatirDurum.OnayBekliyor
            };
        }

        private static HesapEslesmesi? EslesmeBul(
            EslestirmeVerisi veri, AnahtarTipi tip, string cekirdek, string? ek, Yon yon)
            => veri.Eslesmeler.FirstOrDefault(e =>
                   e.AnahtarTipi == tip &&
                   e.Yon == yon &&
                   string.Equals(e.AnahtarCekirdek, cekirdek, StringComparison.Ordinal) &&
                   string.Equals(e.AyirtEdiciEk ?? string.Empty, ek ?? string.Empty, StringComparison.Ordinal));

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
        /// Unvan benzerliği katmanı. Arama uzayı yön → ana grup ile daraltılır; ardından
        /// normalize unvanın **her token'ı sırayla çıpa olarak** denenir. Banka unvanın önüne
        /// kendi iç kodunu ekleyebiliyor ("NAOSKZ NAOS İSTANBUL KOZMETİK"), bu yüzden yalnız
        /// ilk kelimeye bakmak yetmiyor.
        ///
        /// Çıpanın kaç aday getirdiğine **bakılmaz**. Kalabalık çıpalar gürültü değil, meşru
        /// cari aileleri: PKF 89 hesap (grup şirketleri), PARDUS 101 (portföy fonları),
        /// ISTANBUL 126. Ölçümde aday sayısına eşik koymak "PKF İstanbul YMM" skorunu
        /// 0.95'ten 0.48'e, "İstanbul Portföy Yönetimi"ni 1.00'dan 0.61'e düşürüp satırları
        /// alakasız ana hesaplara (373, 110, 121 1) eşliyordu. Yanlış eşleşmeye karşı koruma
        /// zaten iki kuralda: 0.85 otomatik eşiği ve 0.05 aday farkı.
        ///
        /// Hiçbir çıpa sonuç vermezse satır onay kuyruğuna düşer, kod önerilmez.
        /// </summary>
        private static EslestirmeSonuc UnvanaGoreCoz(SatirBaglami baglam, EslestirmeVerisi veri)
        {
            var normalUnvan = Normalizasyon.UnvanNormalize(baglam.Unvan);
            if (normalUnvan.Length == 0)
                return new EslestirmeSonuc { Durum = SatirDurum.Cozulemedi, Katman = KaynakKatman.Yok };

            var anaGrup = AnaGrupBul(baglam.Yon);
            if (!veri.Indeks.GrupDolu(anaGrup))
                return new EslestirmeSonuc { Durum = SatirDurum.Cozulemedi, Katman = KaynakKatman.Yok };

            var adaylar = CipalarlaAra(normalUnvan, anaGrup, veri.Indeks);
            if (adaylar.Count == 0)
                return new EslestirmeSonuc { Durum = SatirDurum.Cozulemedi, Katman = KaynakKatman.Yok };

            var enIyi = adaylar[0];
            var ikinci = adaylar.Count > 1 ? adaylar[1] : null;
            var yakinIkinci = ikinci is not null && enIyi.Skor - ikinci.Skor < AdayFarki;

            var sonuc = new EslestirmeSonuc
            {
                HesapKodu = enIyi.Kod,
                HesapAdi = enIyi.Ad,
                Guven = enIyi.Skor,
                Katman = KaynakKatman.UnvanBenzerligi
            };

            if (!yakinIkinci)
            {
                // Tek belirgin aday: anahtar sade çekirdek kalır. Gereksiz kelime eklemek
                // anahtarın ikinci ay tutmamasına yol açar.
                sonuc.Adaylar = new List<AdayKayit> { enIyi };
                sonuc.Durum = enIyi.Skor >= OtomatikEsik ? SatirDurum.Otomatik : SatirDurum.OnayBekliyor;
                return sonuc;
            }

            // Aynı unvan ailesinden birden fazla cari (Park Plaza Aidat / Elektrik / 19. Kat).
            var aile = adaylar.Where(a => enIyi.Skor - a.Skor < AdayFarki).ToList();
            sonuc.Adaylar = aile;
            sonuc.IkinciAdayKodu = ikinci!.Kod;
            sonuc.IkinciAdayAdi = ikinci.Ad;
            sonuc.IkinciAdaySkoru = ikinci.Skor;

            var secilen = AileyiAyikla(aile, baglam.HamAciklama, out var ayirtEdici);
            if (secilen is null)
            {
                // Ayırt edici kelime metinde yok: tahmin edilmez, tüm aile listelenir.
                sonuc.Durum = SatirDurum.OnayBekliyor;
                return sonuc;
            }

            sonuc.HesapKodu = secilen.Kod;
            sonuc.HesapAdi = secilen.Ad;
            sonuc.Guven = secilen.Skor;
            sonuc.AyirtEdiciEk = ayirtEdici;
            sonuc.Durum = secilen.Skor >= OtomatikEsik ? SatirDurum.Otomatik : SatirDurum.OnayBekliyor;
            return sonuc;
        }

        /// <summary>
        /// Normalize unvanın her token'ını çıpa olarak dener. Çıpayla başlayan hesapları
        /// ön indeksten alır ve kalan metinle (çıpa dahil, sonrası) skorlar; her hesap için
        /// en yüksek skor tutulur. Aday sayısına bakılmaz — kalabalık çıpalar meşru cari
        /// aileleri, eleyince doğru eşleşme kayboluyor.
        /// </summary>
        private static List<AdayKayit> CipalarlaAra(string normalUnvan, string anaGrup, HesapPlaniIndeksi indeks)
        {
            var tokenlar = normalUnvan.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var enIyiler = new Dictionary<string, AdayKayit>(StringComparer.Ordinal);

            for (var i = 0; i < tokenlar.Length; i++)
            {
                var cipa = tokenlar[i];
                if (cipa.Length < 2) continue;

                var kalanMetin = string.Join(' ', tokenlar.Skip(i));

                foreach (var kayit in indeks.CipaylaBaslayanlar(anaGrup, cipa))
                {
                    var skor = Benzerlik.Oran(kalanMetin, kayit.NormalizeAd);

                    if (enIyiler.TryGetValue(kayit.Kod, out var mevcut) && mevcut.Skor >= skor) continue;

                    enIyiler[kayit.Kod] = new AdayKayit { Kod = kayit.Kod, Ad = kayit.Ad, Skor = skor };
                }
            }

            return enIyiler.Values
                .OrderByDescending(a => a.Skor)
                .ThenBy(a => a.Kod, StringComparer.Ordinal)
                .Take(EnFazlaAday)
                .ToList();
        }

        /// <summary>
        /// Aile üyelerinin ortak olmayan kelimelerini ham banka açıklamasında arar
        /// ("Park Plaza Yönetimi, Aidat" → ayırt edici "AIDAT"). Tam olarak bir üye
        /// bulunursa o seçilir; sıfır veya birden fazla üye bulunursa null döner
        /// (satır onaya düşer, tüm aile listelenir).
        /// </summary>
        private static AdayKayit? AileyiAyikla(IReadOnlyList<AdayKayit> aile, string? hamAciklama, out string? ayirtEdici)
        {
            ayirtEdici = null;
            if (aile.Count < 2) return null;

            var metin = Normalizasyon.MetinNormalize(hamAciklama);
            if (metin.Length == 0) return null;

            var tokenSetleri = aile
                .Select(a => Normalizasyon.UnvanNormalize(a.Ad)
                                          .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                          .Where(t => t.Length > 1)
                                          .ToHashSet(StringComparer.Ordinal))
                .ToList();

            // Ortak kısım çekirdek, farklı kısımlar ayırt edici kelimelerdir.
            var ortak = new HashSet<string>(tokenSetleri[0], StringComparer.Ordinal);
            foreach (var kume in tokenSetleri.Skip(1)) ortak.IntersectWith(kume);

            AdayKayit? kazanan = null;
            string? kazananKelime = null;

            for (var i = 0; i < aile.Count; i++)
            {
                var farkli = tokenSetleri[i].Where(t => !ortak.Contains(t)).ToList();
                var bulunan = farkli.FirstOrDefault(t => TokenVarMi(metin, t));
                if (bulunan is null) continue;

                // İkinci bir üye de metinde geçiyorsa ayrım güvenilmez.
                if (kazanan is not null) return null;

                kazanan = aile[i];
                kazananKelime = bulunan;
            }

            ayirtEdici = kazananKelime;
            return kazanan;
        }

        /// <summary>Kelime, normalize metinde tam token olarak geçiyor mu?</summary>
        private static bool TokenVarMi(string normalizeMetin, string kelime)
        {
            if (string.IsNullOrWhiteSpace(kelime) || normalizeMetin.Length == 0) return false;

            foreach (var token in normalizeMetin.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (string.Equals(token, kelime, StringComparison.Ordinal)) return true;

            return false;
        }

        /// <summary>
        /// Yön ana grubu belirler. İstisna satırlar (giren olup 329'a giden gibi) burada
        /// sessizce ters yöne yazılmaz; eşleşme tutmazsa satır onaya düşer.
        /// </summary>
        public static string AnaGrupBul(Yon yon) => yon == Yon.Giren ? GirenAnaGrup : CikanAnaGrup;
    }
}
