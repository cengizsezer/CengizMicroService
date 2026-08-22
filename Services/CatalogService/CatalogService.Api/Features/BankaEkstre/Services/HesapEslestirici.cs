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

        /// <summary>
        /// Satır çoklu adayla onaya düştüyse belirsizliği üreten n-gram. Kullanıcı seçim
        /// yaptığında karar bu anahtarla öğrenilir (bkz. <see cref="AnahtarTipi.Belirsizlik"/>).
        /// </summary>
        public string? BelirsizlikAnahtari { get; set; }

        /// <summary>Belirsizliğin aday kümesi özeti; öğrenilen karar bununla doğrulanır.</summary>
        public string? AdayKumesiOzeti { get; set; }

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

        /// <summary>
        /// Hesap sahibinin tüm yazımları. Hem unvan çıkarmada (karşı taraf sanılmasın) hem de
        /// benzersiz önek indeksinde (firmanın kendi cari kayıtları indekse girmesin) kullanılır.
        /// </summary>
        public HesapSahibiKimligi HesapSahibi { get; init; } = HesapSahibiKimligi.Yok;

        /// <summary>Vergi kodu / anahtar kelime → hesap eşleme tablosu (global).</summary>
        public IReadOnlyList<VergiKoduEslemesi> VergiKodlari { get; init; } = Array.Empty<VergiKoduEslemesi>();

        private HesapPlaniIndeksi? _indeks;
        private CariOnekIndeksi? _onekIndeksi;

        /// <summary>
        /// Benzersiz önek katmanının cari indeksi; ilk kullanımda kurulur ve yükleme boyunca
        /// tekrar kullanılır. Satır başına kurulsaydı 6.000+ kayıt her satırda yeniden
        /// süzülür ve sıralanırdı.
        /// </summary>
        public CariOnekIndeksi OnekIndeksi => _onekIndeksi ??= CariOnekIndeksi.Kur(HesapPlani, HesapSahibi);

        /// <summary>
        /// Hesap planının çıpa indeksi; ilk kullanımda kurulur ve yükleme boyunca tekrar
        /// kullanılır. Satır başına yeniden kurulsaydı indeksin anlamı kalmazdı.
        /// </summary>
        public HesapPlaniIndeksi Indeks => _indeks ??= HesapPlaniIndeksi.Kur(HesapPlani);
    }

    public interface IHesapEslestirici
    {
        EslestirmeSonuc Coz(SatirBaglami baglam, EslestirmeVerisi veri);

        /// <summary>
        /// Ham açıklamada aranan sabit kural (Katman 0). Satır kurulumu bunu <b>unvan
        /// çıkarmadan önce</b> çağırır: kural "unvan çıkarılmasın" diyorsa açıklamadaki isim
        /// bir cari değil, ödeme yapılan kişidir.
        /// </summary>
        SabitKural? AciklamaKuraliBul(SatirBaglami baglam, EslestirmeVerisi veri);

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

        /// <summary>
        /// Bu skorun altındaki unvan benzerliği <b>öneri olarak bile gösterilmez</b>; satır
        /// "Çözülemedi" olur ve kod kutusu boş kalır.
        ///
        /// Ölçümde "Superonline Tahsilatı" satırına 0.20 skorla <c>329 A33 Adobe Systems
        /// Ireland</c>, "Turknet Tahsilatı" satırına 0.21 ile <c>329 N21 Novatek</c>
        /// öneriliyordu. Alakasız öneri boş kutudan kötüdür: kullanıcı yanlışlıkla onaylar
        /// ve sistem onu öğrenir.
        /// </summary>
        public const decimal EnAzOneriEsigi = 0.40m;

        /// <summary>Benzersiz önek katmanının güveni (önek eşleşmesi / alt metin yedeği).</summary>
        private const decimal OnekGuveni = 0.95m;
        private const decimal AltMetinGuveni = 0.90m;

        /// <summary>Yön → ana grup. Ölçüm: giren 141/142 → 120, çıkan 33/35 → 329.</summary>
        public const string GirenAnaGrup = "120";
        public const string CikanAnaGrup = "329";

        /// <summary>Onay ekranında listelenecek en fazla aday.</summary>
        private const int EnFazlaAday = 8;

        public EslestirmeSonuc Coz(SatirBaglami baglam, EslestirmeVerisi veri)
        {
            // --- Katman 0: açıklama kapsamlı sabit kural ---
            // Öğrenme katmanından ÖNCE çalışır. "iş avansı / maaş avansı / masraf ödemesi"
            // işlemin niteliğini belirler; karşı taraf bir cari değil, personeldir. Geçmiş
            // onay katmanı önce çalışsaydı bu satırlar işlem tipi anahtarından (ör. "ISLEM:
            // GÖNDERİLEN HAVALE") ilgisiz bir cariye çözülürdü.
            var aciklamaKurali = AciklamaKuraliBul(baglam, veri);
            if (aciklamaKurali is not null) return KuralSonucu(aciklamaKurali, baglam, veri);

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

            // --- Katman 3: Vergi tahsilatı (kod + anahtar kelime + plaka) ---
            // Sabit kuraldan önce: vergi satırlarında karşı hesap metnin içeriğine göre
            // değişiyor (gerçek dosyada 5 vergi satırı dört farklı hesaba gitmiş), tek
            // kural yetmiyor. Unvan benzerliğine hiç düşmemeli — açıklamadaki
            // "Soyadi/Unvani :PKF ADAY …" hesap sahibinin kendi unvanı.
            var vergi = VergiyleCoz(baglam, veri);
            if (vergi is not null) return vergi;

            // --- Katman 4: Sabit kural tablosu (işlem tipi kapsamı) ---
            var kural = KuralBul(baglam, veri, KuralKapsami.IslemTipi);
            if (kural is not null) return KuralSonucu(kural, baglam, veri);

            // --- Katman 5: Benzersiz önek ---
            // Ters yönde çalışır: açıklamadan unvan çıkarıp benzerlik aramak yerine, hesap
            // adı çekirdeği açıklamanın bir token dizisiyle başlayan cariyi bulur. Ölçümde
            // isabeti %98 (unvan benzerliğinde %87); desen tabanlı katmandan önce denenir.
            var onek = OnekleCoz(baglam, veri);
            if (onek is not null) return onek;

            // --- Katman 6: Unvan benzerliği ---
            return UnvanaGoreCoz(baglam, veri);
        }

        /// <summary>
        /// Satırın öğrenme anahtarı çekirdeği: unvan varsa normalize çekirdeği, yoksa
        /// işlem tipi (banka masrafı, HGS, vergi gibi unvansız satırlar için).
        /// </summary>
        public static string AnahtarCekirdek(SatirBaglami baglam)
        {
            // Hesap sahibinin kendi adı yakalandığında veya kişi bazlı bir sabit kural
            // tuttuğunda anahtar hiç üretilmez: ne aranır ne de öğrenilir.
            if (baglam.AnahtarUretilmesin) return string.Empty;

            var cekirdek = Normalizasyon.UnvanCekirdek(baglam.Unvan);
            return cekirdek.Length > 0 ? cekirdek : Normalizasyon.IslemAnahtari(baglam.IslemTipi);
        }

        /// <summary>Açıklama üretimi için karşı banka hesabı; belirsizlikte adaylardan biri döner.</summary>
        public BankaHesabi? BankaBul(SatirBaglami baglam, EslestirmeVerisi veri)
            => BankaEslesmesiBul(baglam, veri).Temsilci;

        /// <summary>
        /// Kendi hesapları arası hareketlerde karşı banka hesabını bulur.
        ///
        /// <b>Katman ne zaman açılır?</b> En az biri sağlanmalı:
        /// <list type="bullet">
        /// <item><b>(a)</b> Metinde bankalar arası ifadesi var: <i>hesaplar arası</i>,
        /// <i>hesaplararası</i>, <i>virman</i>, <i>süpürme</i>. Ölçüm: gerçek 48 bankalar
        /// arası satırın 42'sinde bu ifade geçiyor.</item>
        /// <item><b>(b)</b> Çıkarılan karşı taraf hesap sahibinin kendisi. Gerçek bir kendi
        /// hesapları arası transferde gönderen de alıcı da aynı firmadır ("… PKF ADAY
        /// BAĞIMSIZ DENETİM ANONİM ŞİRKETİ tarafından PKF ADAY BAĞIMSIZ DENETİM ANONİM
        /// ŞİRKETİ tarafına …"). Kalan 6 bankalar arası satır bu koşulla yakalanıyor.</item>
        /// </list>
        ///
        /// <b>Neden bu kadar dar?</b> Katman önceden yalnız "açıklamada banka adı geçiyor"
        /// diye tetikleniyordu; ama müşteri ödemelerinde de <b>gönderenin bankası</b> yazıyor.
        /// Ölçüm: 87 cari satırının <b>59'unda</b> açıklamada banka adı geçiyor
        /// ("BAYCAN A.Ş. CARİ HESAP ÖDEME/TÜRKİYE CUMHURİYETİ ZİRAAT BANKASI …",
        /// "NAOSKZ NAOS İSTANBUL KOZMETİK…/TÜRKİYE GARANTİ BANKASI …", tüm personel masraf
        /// ödemeleri). Bunların hepsi cari katmanlarına gitmeli.
        ///
        /// İkisi de tutmazsa katman atlanır ve satır cari katmanlarına düşer; orada da
        /// çözülemezse onaya gider — yanlış çözmektense sorar.
        ///
        /// <b>Arama sırası</b>
        /// <list type="number">
        /// <item>IBAN — kullanıcının kendi tanımladığı hesap IBAN'ı (öğrenilmiş veri değil).</item>
        /// <item><b>Başka bankalar</b>: önce <see cref="BankaHesabi.EslestirmeAnahtarlari"/>,
        /// sonra <see cref="BankaHesabi.BankaAdi"/>; ikisi de metinde aranır.</item>
        /// <item><b>Ekstrenin kendi bankası</b>: anahtarlar metinde aranır; hiçbiri tutmazsa
        /// bankayı zaten ekstrenin kendisi belirlediği için aynı bankanın diğer hesapları
        /// aday olur.</item>
        /// </list>
        ///
        /// Ekstrenin kendi bankası ikinci tura bırakılır: "HESAPLAR ARASI E.F.T.
        /// VAKIFBANK/DENİZBANK …" satırında "Vakıfbank" biziz, karşı taraf Denizbank.
        /// Aynı turda yarışsalardı ikisi de 9 karakterle berabere kalır, satır gereksiz
        /// yere onaya düşerdi.
        ///
        /// Metin katmanlarında <b>en uzun eşleşen</b> kazanır: "Otomatik Süpürme" (16 karakter)
        /// "Vakıfbank"ı (9) yener. Beraberlikte tahmin edilmez; adaylar döner ve satır onaya düşer.
        /// </summary>
        public BankaEslesmesi BankaEslesmesiBul(SatirBaglami baglam, EslestirmeVerisi veri)
        {
            var bankalarArasi = BankalarArasiIfadeVarMi(baglam);
            if (!bankalarArasi && !KarsiTarafHesapSahibiMi(baglam, veri)) return BankaEslesmesi.Yok;

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

            var islenenBanka = veri.BankaHesaplari
                .FirstOrDefault(h => h.Id == veri.IslenenBankaHesabiId)?.BankaAdi;

            // 1. tur: başka bankaların hesapları.
            var digerler = adaylar.Where(h => !AyniBankaMi(h.BankaAdi, islenenBanka)).ToList();
            var bulunan = MetinleAra(digerler, metin);
            if (bulunan is not null) return bulunan;

            // 2. tur: ekstrenin kendi bankasındaki diğer hesaplar.
            var ayniBanka = adaylar.Where(h => AyniBankaMi(h.BankaAdi, islenenBanka)).ToList();
            if (ayniBanka.Count == 0) return BankaEslesmesi.Yok;

            var anahtarla = EnUzunEslesenler(ayniBanka, metin,
                h => EslestirmeAnahtari.NormalizeAnahtarlar(h.EslestirmeAnahtarlari));
            if (anahtarla.Count > 0) return Eslesme(anahtarla);

            // Hiçbir anahtar tutmadı. "Hesaplararası Virman" satırlarında açıklamada banka
            // adı hiç geçmiyor — ayrım ekstrenin kendi bankasından geliyor, o yüzden aynı
            // bankanın tüm hesapları aday olur; birden fazlaysa satır onaya düşer.
            //
            // Bu genişletme YALNIZ (a) koşuluyla açılan satırlarda yapılır. (b) ile açılan
            // satırlarda yapılsaydı, karşı tarafı gerçek bir cari olan satırlar cari
            // eşleştirmesine hiç gidemeden banka adaylarıyla onaya düşerdi.
            return bankalarArasi ? Eslesme(ayniBanka) : BankaEslesmesi.Yok;
        }

        /// <summary>
        /// (a) Bankalar arası ifadeleri. Ham açıklama <b>ve</b> işlem tipi taranır: aynı
        /// bilgi bazen açıklamada ("HESAPLAR ARASI E.F.T. VAKIFBANK/DENİZBANK …"), bazen
        /// işlem tipinde ("Virman", "Otomatik Süpürme İşlemleri Virman") duruyor.
        ///
        /// Karşılaştırma <see cref="Normalizasyon.KisaltmaNormalize"/> üzerinden ve tam
        /// kelime sınırıyla; "HESAPLARARASI" bitişik yazımı ayrı bir ifade olarak aranıyor.
        /// </summary>
        private static readonly string[] BankalarArasiIfadeleri =
        {
            "HESAPLAR ARASI", "HESAPLARARASI", "VIRMAN", "SUPURME"
        };

        private static bool BankalarArasiIfadeVarMi(SatirBaglami baglam)
        {
            var metin = Normalizasyon.KisaltmaNormalize(baglam.HamAciklama + " " + baglam.IslemTipi);
            if (metin.Length == 0) return false;

            return BankalarArasiIfadeleri.Any(ifade => Normalizasyon.IfadeVarMi(metin, ifade));
        }

        /// <summary>
        /// (b) Karşı taraf, hesap sahibinin kendisi mi?
        ///
        /// En az bir desen hesap sahibinin unvanını karşı taraf olarak yakalamış olmalı
        /// (<see cref="SatirBaglami.HesapSahibiElendi"/>) <b>ve</b> geriye gerçek bir firma
        /// kalmamalı. "Gerçek firma kalmamış" iki biçimde olur:
        /// <list type="bullet">
        /// <item>Hiçbir desen başka unvan vermedi.</item>
        /// <item>Kalan yakalama bir <b>banka adı</b>: "İŞ BANKASI  (PKF ADAY … VADESİZ
        /// HESABINDAN … NO(apostrof)LU PKF ADAY … HESABINA …)" satırında parantez öncesi
        /// serbest metin unvan sanılıyor. Banka adı karşı taraf değil, transferin gittiği
        /// bankadır.</item>
        /// </list>
        ///
        /// Karşı taraf <b>başka</b> bir firmaysa bu bir müşteri/tedarikçi hareketidir ve
        /// katman çalışmamalıdır (MARBAŞ MENKUL DEĞERLER, DEMET DÖVİZ satırları).
        /// </summary>
        private static bool KarsiTarafHesapSahibiMi(SatirBaglami baglam, EslestirmeVerisi veri)
        {
            if (!baglam.HesapSahibiElendi) return false;
            if (string.IsNullOrWhiteSpace(baglam.Unvan)) return true;

            return BankaAdiMiUnvan(baglam.Unvan, veri.BankaHesaplari);
        }

        /// <summary>
        /// Çıkarılan unvan bir bankayı mı gösteriyor? Önce genel banka kelimeleri
        /// ("… BANKASI", "… BANK"), sonra <b>kayıt defterindeki</b> banka adları ve
        /// eşleştirme anahtarları — "DENİZBANK HESABINA" gibi yazımlarda genel kelime yok,
        /// ayırt eden şey bankanın kayıt defterinde tanımlı olması.
        /// </summary>
        private static bool BankaAdiMiUnvan(string unvan, IReadOnlyList<BankaHesabi> bankaHesaplari)
        {
            if (Normalizasyon.BankaAdliMi(unvan)) return true;

            var metin = Normalizasyon.MetinNormalize(unvan);
            if (metin.Length == 0) return false;

            foreach (var hesap in bankaHesaplari)
            {
                if (Normalizasyon.IfadeVarMi(metin, Normalizasyon.MetinNormalize(hesap.BankaAdi))) return true;

                foreach (var anahtar in EslestirmeAnahtari.NormalizeAnahtarlar(hesap.EslestirmeAnahtarlari))
                    if (anahtar.Length >= EslestirmeAnahtari.EnKisaAnahtar && Normalizasyon.IfadeVarMi(metin, anahtar))
                        return true;
            }

            return false;
        }

        /// <summary>Önce anahtarlar, sonra banka adı; ikisi de metinde aranır. Hiçbiri tutmazsa null.</summary>
        private static BankaEslesmesi? MetinleAra(IReadOnlyList<BankaHesabi> adaylar, string metin)
        {
            if (adaylar.Count == 0) return null;

            var anahtarla = EnUzunEslesenler(adaylar, metin,
                h => EslestirmeAnahtari.NormalizeAnahtarlar(h.EslestirmeAnahtarlari));
            if (anahtarla.Count > 0) return Eslesme(anahtarla);

            var adla = EnUzunEslesenler(adaylar, metin,
                h => new[] { Normalizasyon.MetinNormalize(h.BankaAdi) });

            return adla.Count == 0 ? null : Eslesme(adla);
        }

        /// <summary>
        /// İki banka adı aynı bankayı mı gösteriyor? Karşılaştırma normalize edilmiş metin
        /// üzerinden: "Vakıfbank" ile "VAKIFBANK" aynı bankadır.
        /// </summary>
        private static bool AyniBankaMi(string? a, string? b)
            => !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
               string.Equals(Normalizasyon.MetinNormalize(a), Normalizasyon.MetinNormalize(b),
                             StringComparison.Ordinal);

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

        public SabitKural? AciklamaKuraliBul(SatirBaglami baglam, EslestirmeVerisi veri)
            => KuralBul(baglam, veri, KuralKapsami.Aciklama);

        /// <summary>
        /// Kural sonucunu üretir. Kural yalnız <b>ana grubu</b> veriyorsa
        /// (<see cref="SabitKural.AltHesapGerekli"/>) alt hesap kullanıcıdan beklenmeden
        /// önce unvanla aranır: arama uzayı yönün ana grubu (120/329) değil, <b>kuralın</b>
        /// ana grubudur. "masraf ödemesi … İlyas Ömeroğlu hesabına" satırı böylece 195
        /// içindeki kişi muavinine iner; bulunamazsa satır eskisi gibi ana grupla onaya düşer.
        /// </summary>
        private static EslestirmeSonuc KuralSonucu(SabitKural kural, SatirBaglami baglam, EslestirmeVerisi veri)
        {
            if (kural.AltHesapGerekli)
            {
                var altHesap = UnvanaGoreCoz(baglam, veri, Normalizasyon.AnaGrup(kural.HesapKodu),
                                             KaynakKatman.SabitKural);

                if (altHesap.Durum != SatirDurum.Cozulemedi) return altHesap;
            }

            // Plaka anahtarı: HGS ve otoyol yükleme satırlarında metindeki plakayı adında
            // taşıyan hesaplar varsa öne çıkarılır. Plaka tek başına karar vermez — aynı
            // plakanın birden fazla hesabı olabiliyor ("34 Mrp 081 Araç Kira Bedeli" /
            // "… Araç Otopark Yakıt Vb.") — adayları daraltır ve satır onaya düşer.
            // Alt hesabı kullanıcıdan beklenen kurallarda (personel/iş avansı) plaka aranmaz:
            // orada aday kümesi kişi muavinidir, araç hesabı değil.
            var plakalilar = kural.AltHesapGerekli
                ? new List<HesapPlaniKaydi>()
                : VergiPlakaCozucu.PlakaAdaylari(baglam.HamAciklama, veri.HesapPlani);

            if (plakalilar.Count > 0)
            {
                var adaylar = new List<AdayKayit>();
                foreach (var hesap in plakalilar) AdayEkle(adaylar, hesap.Kod, hesap.Ad);
                AdayEkle(adaylar, kural.HesapKodu, kural.HesapAdi);

                return new EslestirmeSonuc
                {
                    Guven = 0m,
                    Katman = KaynakKatman.VergiPlaka,
                    Adaylar = adaylar.Take(EnFazlaAday).ToList(),
                    Durum = SatirDurum.OnayBekliyor
                };
            }

            return new EslestirmeSonuc
            {
                HesapKodu = kural.HesapKodu,
                HesapAdi = kural.HesapAdi,
                // Yalnız ana grubu veren kuralda güven bildirilmez: kod eksik, kullanıcı tamamlayacak.
                Guven = kural.AltHesapGerekli ? 0m : kural.Guven,
                Katman = KaynakKatman.SabitKural,
                // Alt hesap (kişi/muavin) kullanıcıdan gelmek zorundaysa satır otomatik kapanmaz.
                Durum = kural.AltHesapGerekli ? SatirDurum.OnayBekliyor : SatirDurum.Otomatik
            };
        }

        /// <summary>
        /// Verilen kapsamdaki ilk uyan kural. İşlem tipi kapsamında desen işlem tipi
        /// metninde, açıklama kapsamında ham açıklamada aranır.
        ///
        /// Açıklama kapsamında <see cref="EslesmeTuru.Icerir"/> <b>tam kelime</b> arar
        /// (<see cref="Normalizasyon.IfadeVarMi"/>): düz <c>Contains</c> ile "AVANS" deseni
        /// "AVANSAS" gibi bir unvanın içinde de tutar ve satırı personel avansı sanardı.
        /// </summary>
        private static SabitKural? KuralBul(SatirBaglami baglam, EslestirmeVerisi veri, KuralKapsami kapsam)
        {
            var kaynak = kapsam == KuralKapsami.Aciklama ? baglam.HamAciklama : baglam.IslemTipi;
            if (string.IsNullOrWhiteSpace(kaynak)) return null;

            var hedef = Normalizasyon.TurkceSadelestir(kaynak).Trim();
            var normalHedef = kapsam == KuralKapsami.Aciklama ? Normalizasyon.MetinNormalize(kaynak) : string.Empty;

            foreach (var kural in veri.SabitKurallar.Where(k => k.Aktif && k.Kapsam == kapsam).OrderBy(k => k.Sira))
            {
                if (kural.Yon is Yon y && y != baglam.Yon) continue;

                var desen = Normalizasyon.TurkceSadelestir(kural.IslemTipiDeseni).Trim();
                if (desen.Length == 0) continue;

                var uyuyor = kural.EslesmeTuru switch
                {
                    EslesmeTuru.Tam => string.Equals(hedef, desen, StringComparison.Ordinal),
                    EslesmeTuru.Icerir => kapsam == KuralKapsami.Aciklama
                        ? Normalizasyon.IfadeVarMi(normalHedef, Normalizasyon.MetinNormalize(kural.IslemTipiDeseni))
                        : hedef.Contains(desen, StringComparison.Ordinal),
                    EslesmeTuru.Regex => System.Text.RegularExpressions.Regex.IsMatch(
                        kaynak, kural.IslemTipiDeseni,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase,
                        TimeSpan.FromMilliseconds(250)),
                    _ => false
                };

                if (uyuyor) return kural;
            }

            return null;
        }

        // ---- Katman 5: Benzersiz önek ----

        /// <summary>
        /// Benzersiz önek katmanı. Açıklamanın token dizileri n=4'ten n=2'ye inerek
        /// dolaşılır; hesap adı çekirdeği o diziyle <b>başlayan</b> cariler aranır.
        ///
        /// Tek hesaba inilirse otomatik. Birden fazlaysa önce yön kuralı denenir (aynı
        /// carinin 159/329 kopyası sahte belirsizliktir), sonra öğrenilmiş belirsizlik
        /// kararı; ikisi de çözmezse satır <b>tüm adaylarla</b> onaya düşer. Rastgele ya da
        /// "ilki" seçilmez.
        ///
        /// Hiç eşleşme yoksa null döner ve sıradaki katman (unvan benzerliği) devam eder.
        /// </summary>
        private static EslestirmeSonuc? OnekleCoz(SatirBaglami baglam, EslestirmeVerisi veri)
        {
            var indeks = veri.OnekIndeksi;
            if (indeks.Sayi == 0) return null;

            var sonuc = OnekAramasi(baglam, veri, indeks);
            if (sonuc is null) return null;

            if (!sonuc.Belirsiz) return OnekSonucu(sonuc.Hesaplar[0], sonuc);

            // Yön kuralı: adların çekirdeği aynı ve fark yalnız ana gruptaysa belirsizlik sahte.
            var yonle = CariOnekIndeksi.YonleCoz(sonuc.Hesaplar, baglam.Yon);
            if (yonle is not null) return OnekSonucu(yonle, sonuc);

            var tumAdaylar = sonuc.Hesaplar
                .Select(h => new AdayKayit { Kod = h.Kod, Ad = h.Ad, Skor = 0m })
                .ToList();

            var adaylar = tumAdaylar.Take(EnFazlaAday).ToList();

            // Aile ayrımı: adaylar ortak bir çekirdeği paylaşıyor ve ayırt edici kelimelerden
            // tam olarak biri metinde geçiyorsa (Park Plaza Yönetimi "Aidat" / "Elektrik")
            // belirsizlik gerçek değil. Birden fazla üye geçiyorsa tahmin edilmez.
            //
            // Kırpılmamış küme verilir: ekranda gösterilen ilk 8 üzerinden karar verilseydi
            // 37 üyeli Pardus ailesinde 8'in dışındaki üyeler görünmez olur ve ayrım
            // "tek üye uydu" sanılıp yanlış fona otomatik kayıt atılırdı.
            var secilen = AileyiAyikla(tumAdaylar, baglam.HamAciklama, out var ayirtEdici);
            if (secilen is not null)
            {
                var aileSonucu = OnekSonucu(
                    sonuc.Hesaplar.First(h => string.Equals(h.Kod, secilen.Kod, StringComparison.Ordinal)), sonuc);
                aileSonucu.AyirtEdiciEk = ayirtEdici;
                return aileSonucu;
            }

            var ozet = CariOnekIndeksi.AdayOzeti(sonuc.Hesaplar.Select(h => h.Kod));
            var anahtar = Normalizasyon.Kirp(sonuc.Anahtar, 200);

            // Kullanıcı bu belirsizliği daha önce çözdüyse bir daha sorulmaz — aday kümesi
            // aynı kaldığı sürece. Küme değiştiyse (yeni bir Park Plaza hesabı açılmış)
            // eski karar sessizce uygulanmaz.
            var karar = veri.Eslesmeler.FirstOrDefault(e =>
                e.AnahtarTipi == AnahtarTipi.Belirsizlik &&
                e.Yon == baglam.Yon &&
                string.Equals(e.AnahtarCekirdek, anahtar, StringComparison.Ordinal) &&
                string.Equals(e.AdayKumesiOzeti ?? string.Empty, ozet, StringComparison.Ordinal));

            if (karar is not null)
            {
                var ogrenilen = Kesin(karar, KaynakKatman.GecmisOnay);
                ogrenilen.BelirsizlikAnahtari = anahtar;
                ogrenilen.AdayKumesiOzeti = ozet;
                return ogrenilen;
            }

            return new EslestirmeSonuc
            {
                Guven = 0m,
                Katman = KaynakKatman.BenzersizOnek,
                Adaylar = adaylar,
                // Onay ekranı aday listesini kullanır; ikinci aday alanları eski
                // sözleşmeyi (iki adaylı gösterim) bozmamak için yine doldurulur.
                IkinciAdayKodu = adaylar[1].Kod,
                IkinciAdayAdi = adaylar[1].Ad,
                IkinciAdaySkoru = adaylar[1].Skor,
                BelirsizlikAnahtari = anahtar,
                AdayKumesiOzeti = ozet,
                Durum = SatirDurum.OnayBekliyor
            };
        }

        /// <summary>
        /// Önek aramasının iki kaynağı, sırayla:
        /// <list type="number">
        /// <item><b>Desenle çıkarılan unvan</b> — gürültüsüz olduğu için tek kelimelik
        /// diziler de aranır (n≥1). "Belbim", "Superonline", "Turknet" gibi tek kelimelik
        /// satıcılar ancak böyle bulunur; ham açıklamada tek kelime aramak gürültü üretirdi.</item>
        /// <item><b>Ham açıklamanın token dizileri</b> (n≥2), hesap sahibinin kendi adı
        /// çıkarılmış hâlde. Ölçülen 287 satırın 268'inde firmanın kendi unvanı geçiyor;
        /// çıkarılmazsa "BAGIMSIZ DENETIM" dizisi <c>120 B58 Bağımsız Denetim Derneği</c>
        /// gibi <b>başka</b> bir cariye eşleşir.</item>
        /// </list>
        /// </summary>
        private static OnekSonuc? OnekAramasi(SatirBaglami baglam, EslestirmeVerisi veri, CariOnekIndeksi indeks)
        {
            var tekKelime = TekKelimelikUnvan(baglam, veri);
            if (tekKelime is not null)
            {
                var unvanSonucu = indeks.Ara(new[] { tekKelime }, enKisaNgram: 1,
                                             altMetinIlkKelimeSarti: false);

                // Yalnız <b>tek</b> sonuç kabul edilir. Çoklu sonuçta ham açıklamaya
                // düşülür: orada satırın kalanı da tarandığı için aday kümesi daha eksiksiz
                // olur ve satır doğru adaylarla onaya düşer.
                if (unvanSonucu.Hesaplar.Count == 1) return unvanSonucu;
            }

            var parcalar = veri.HesapSahibi.Parcala(Normalizasyon.CekirdekTokenlari(baglam.HamAciklama));
            var metinSonucu = indeks.Ara(parcalar);

            return metinSonucu.Bulundu ? metinSonucu : null;
        }

        /// <summary>
        /// Tek kelimeden ibaret çıkarılmış unvan ("Belbim", "Superonline", "Turknet") — ham
        /// açıklamada n≥2 dizi aranması bu satırları hiç çözemez. Desen yakaladığı için bu
        /// kelime gürültü değil; tek kelimelik arama yalnız burada açılır.
        ///
        /// Çok kelimeli unvanlarda ham açıklama kullanılır: satırın kalanında geçen diğer
        /// cariler de aday olsun ("KEMAL GÜLMAN VK POLAT GÜLMAN PARK PLAZA 19.KAT" satırında
        /// desen yalnız "POLAT GÜLMAN"ı veriyor, ama karşı taraf üç adaydan biri).
        /// </summary>
        private static string[]? TekKelimelikUnvan(SatirBaglami baglam, EslestirmeVerisi veri)
        {
            if (string.IsNullOrWhiteSpace(baglam.Unvan)) return null;
            if (veri.HesapSahibi.Kendisi(baglam.Unvan)) return null;

            var tokenlar = Normalizasyon.CekirdekTokenlari(baglam.Unvan);
            if (tokenlar.Count != 1 || tokenlar[0].Length < EnKisaTekKelime) return null;

            return new[] { tokenlar[0] };
        }

        /// <summary>Tek kelimelik unvan aramasının alt sınırı; kısa kelime her şeye eşleşir.</summary>
        private const int EnKisaTekKelime = 4;

        private static EslestirmeSonuc OnekSonucu(HesapPlaniKaydi hesap, OnekSonuc sonuc) => new()
        {
            HesapKodu = hesap.Kod,
            HesapAdi = hesap.Ad,
            Guven = sonuc.AltMetinYedegi ? AltMetinGuveni : OnekGuveni,
            Katman = KaynakKatman.BenzersizOnek,
            Durum = SatirDurum.Otomatik
        };

        // ---- Katman 3: Vergi tahsilatı ve plaka ----

        /// <summary>
        /// Vergi tahsilatı satırının karşı hesabı. Vergi kodu (9085, 0040, 0033) ve anahtar
        /// kelimeler (TRAFİK CEZ, DAMGA, BEYANNAME) yönetilebilir tablodan; metinde plaka
        /// geçiyorsa o plakayı adında taşıyan hesaplar da aday olur.
        ///
        /// Tek aday varsa otomatik; birden fazla veya hiç yoksa satır onaya düşer. Plaka tek
        /// başına karar vermez — aynı plakanın birden fazla hesabı olabiliyor
        /// ("34 Mrp 081 Araç Kira Bedeli" / "… Araç Otopark Yakıt Vb.").
        ///
        /// Vergi satırı değilse null döner ve sıradaki katman devam eder.
        /// </summary>
        private static EslestirmeSonuc? VergiyleCoz(SatirBaglami baglam, EslestirmeVerisi veri)
        {
            if (!VergiPlakaCozucu.VergiSatiriMi(baglam.IslemTipi)) return null;

            var adaylar = new List<AdayKayit>();

            foreach (var eslesme in VergiPlakaCozucu.VergiAdaylari(baglam.HamAciklama, veri.VergiKodlari))
                AdayEkle(adaylar, eslesme.HesapKodu, eslesme.HesapAdi);

            foreach (var hesap in VergiPlakaCozucu.PlakaAdaylari(baglam.HamAciklama, veri.HesapPlani))
                AdayEkle(adaylar, hesap.Kod, hesap.Ad);

            if (adaylar.Count == 1)
                return new EslestirmeSonuc
                {
                    HesapKodu = adaylar[0].Kod,
                    HesapAdi = adaylar[0].Ad,
                    Guven = 0.95m,
                    Katman = KaynakKatman.VergiPlaka,
                    Durum = SatirDurum.Otomatik
                };

            // Hiç aday yoksa da onaya düşer: eşleme tablosunda karşılığı olmayan bir vergi
            // kodu (ölçümde "0010/KURUMLAR V.") kullanıcıya sorulmalı, tahmin edilmemeli.
            return new EslestirmeSonuc
            {
                Guven = 0m,
                Katman = KaynakKatman.VergiPlaka,
                Adaylar = adaylar.Take(EnFazlaAday).ToList(),
                Durum = SatirDurum.OnayBekliyor
            };
        }

        /// <summary>Aynı kod iki kaynaktan (vergi tablosu + plaka) gelirse tek kez listelenir.</summary>
        private static void AdayEkle(List<AdayKayit> adaylar, string? kod, string? ad)
        {
            var normal = Normalizasyon.HesapKoduNormalize(kod);
            if (normal.Length == 0) return;
            if (adaylar.Any(a => string.Equals(a.Kod, normal, StringComparison.Ordinal))) return;

            adaylar.Add(new AdayKayit { Kod = normal, Ad = ad ?? string.Empty, Skor = 0m });
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
            => UnvanaGoreCoz(baglam, veri, AnaGrupBul(baglam.Yon), KaynakKatman.UnvanBenzerligi);

        /// <summary>
        /// <see cref="UnvanaGoreCoz(SatirBaglami, EslestirmeVerisi)"/>'ün arama uzayı ve
        /// katman etiketi dışarıdan verilen hâli. Sabit kural katmanı, kuralın belirlediği
        /// ana grubun içinde alt hesap ararken bunu kendi etiketiyle kullanır.
        /// </summary>
        private static EslestirmeSonuc UnvanaGoreCoz(
            SatirBaglami baglam, EslestirmeVerisi veri, string anaGrup, KaynakKatman katman)
        {
            var normalUnvan = Normalizasyon.UnvanNormalize(baglam.Unvan);
            if (normalUnvan.Length == 0)
                return new EslestirmeSonuc { Durum = SatirDurum.Cozulemedi, Katman = KaynakKatman.Yok };

            if (anaGrup.Length == 0 || !veri.Indeks.GrupDolu(anaGrup))
                return new EslestirmeSonuc { Durum = SatirDurum.Cozulemedi, Katman = KaynakKatman.Yok };

            var adaylar = CipalarlaAra(normalUnvan, anaGrup, veri.Indeks);
            if (adaylar.Count == 0)
                return new EslestirmeSonuc { Durum = SatirDurum.Cozulemedi, Katman = KaynakKatman.Yok };

            var enIyi = adaylar[0];

            // Alakasız öneri boş kutudan kötüdür: kullanıcı yanlışlıkla onaylayabilir ve
            // sistem onu öğrenir. Eşik altındaki aday hiç gösterilmez.
            if (enIyi.Skor < EnAzOneriEsigi)
                return new EslestirmeSonuc { Durum = SatirDurum.Cozulemedi, Katman = KaynakKatman.Yok };

            var ikinci = adaylar.Count > 1 ? adaylar[1] : null;
            var yakinIkinci = ikinci is not null && enIyi.Skor - ikinci.Skor < AdayFarki;

            var sonuc = new EslestirmeSonuc
            {
                HesapKodu = enIyi.Kod,
                HesapAdi = enIyi.Ad,
                Guven = enIyi.Skor,
                Katman = katman
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

            // Bir üyenin ayırt edici kelimesi hiç yoksa (adı diğerlerinin ortak çekirdeğinden
            // ibaret: "Cms Jant" / "Cms Jant Makina") o üye hiçbir zaman kazanamaz. Böyle bir
            // ailede ayrım taraflı olur; karar kullanıcıya bırakılır.
            if (tokenSetleri.Any(k => k.All(ortak.Contains))) return null;

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
