using System.Security.Cryptography;
using System.Text;
using CatalogService.Api.Features.BankaEkstre.Domain;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    /// <summary>Benzersiz önek aramasının sonucu.</summary>
    public sealed class OnekSonuc
    {
        public static readonly OnekSonuc Yok = new();

        /// <summary>Eşleşmeyi üreten n-gram; belirsizlik öğrenmesinin anahtarı budur.</summary>
        public string Anahtar { get; init; } = string.Empty;

        /// <summary>Bulunan hesaplar. Tek eleman = kesin aday, çok eleman = belirsizlik.</summary>
        public IReadOnlyList<HesapPlaniKaydi> Hesaplar { get; init; } = Array.Empty<HesapPlaniKaydi>();

        /// <summary>Eşleşme önek ile mi bulundu, yoksa alt metin yedeğiyle mi?</summary>
        public bool AltMetinYedegi { get; init; }

        public bool Bulundu => Hesaplar.Count > 0;
        public bool Belirsiz => Hesaplar.Count > 1;
    }

    /// <summary>
    /// Cari hesapların <b>ters yönde</b> aranması: açıklamadan unvan çıkarıp hesap planında
    /// benzerlik aramak yerine, hesap adı çekirdeği açıklamanın bir token dizisiyle
    /// <b>başlayan</b> carileri bulur.
    ///
    /// Yöntem kullanıcının ORKA'da elle yaptığı şeyi taklit eder: arama kutusuna kısa bir
    /// önek yazar ("baycan elek"), tek sonuç çıkarsa onu seçer, birden fazla çıkarsa bakıp
    /// karar verir.
    ///
    /// <b>"Başlıyor" kritik, "içeriyor" değil.</b> ORKA hesap adlarını 50 karakterde kesiyor:
    /// ölçülen 6.128 kaydın 914'ü 48–50 karakter ve son kelimesi ortasından kopmuş
    /// (<c>120 B62 "Baycan Elektrik Müteahhitlik Sanayi Ve Ticaret Ano"</c>). Açıklamada
    /// "… MÜTEAHHİTLİK SANAYİ VE TİCARET ANONİM" yazdığı için bitişik alt metin eşleşmesi
    /// tutmuyor; önek eşleşmesi kesilmeden etkilenmiyor.
    ///
    /// <b>Ölçüm</b> (gerçek dosya, 87 cari satırı):
    /// <code>
    /// Bitişik alt metin + en uzun    69 çözüldü, 60 doğru,  9 yanlış  → %87
    /// Benzersiz önek (+ alt metin)   57 çözüldü, 56 doğru,  1 yanlış  → %98
    /// </code>
    /// Daha az satır çözer ama neredeyse hiç yanlış yapmaz; kalan satırlar çoklu adayla
    /// onaya düşer. <b>Bu değiş tokuş bilinçli</b> — muhasebede yanlış kayıt, onaya düşen
    /// satırdan pahalıdır. Kapsamı artırmak için gevşetilmemeli.
    /// </summary>
    public sealed class CariOnekIndeksi
    {
        /// <summary>Boş plan; hesap planı içe aktarılmamış firmada katman sessizce kapalı kalır.</summary>
        public static readonly CariOnekIndeksi Bos = new(Array.Empty<OnekKaydi>());

        /// <summary>
        /// İndekse girecek ana gruplar. <b>Yalnız cari grupları</b>: gider hesapları girmesin,
        /// planda <c>622 0 03 00 PKF ADAY BAĞIMSIZ DENETİM</c>, <c>740 0 BAĞIMSIZ DENETİM</c>
        /// gibi firmanın kendi adını taşıyan kayıtlar var; indekse girerse her satır onlara
        /// eşleşir.
        /// </summary>
        public static readonly IReadOnlyList<string> CariGruplari =
            new[] { "120", "329", "136", "159", "195", "196", "320", "331", "336" };

        /// <summary>
        /// İndekse girecek en kısa hesap adı çekirdeği. Ölçümde 8 de aynı sonucu veriyor;
        /// 12 yapılınca isabet çöküyor — yükseltilmemeli.
        /// </summary>
        public const int EnKisaCekirdek = 6;

        /// <summary>Dolaşılacak en uzun ve en kısa token dizisi.</summary>
        public const int EnUzunNgram = 4;
        public const int EnKisaNgram = 2;

        /// <summary>Hesap adı çekirdeği + kayıt. Çekirdeğe göre ordinal sıralı tutulur.</summary>
        public sealed record OnekKaydi(string Cekirdek, HesapPlaniKaydi Kayit);

        private readonly OnekKaydi[] _sirali;

        private CariOnekIndeksi(OnekKaydi[] sirali) => _sirali = sirali;

        public int Sayi => _sirali.Length;

        /// <summary>
        /// İndeksi kurar. <b>Yükleme başına bir kez</b> çağrılır (bkz.
        /// <see cref="EslestirmeVerisi.OnekIndeksi"/>), satır başına değil.
        /// </summary>
        public static CariOnekIndeksi Kur(IReadOnlyList<HesapPlaniKaydi> plan, HesapSahibiKimligi? sahip = null)
        {
            var gruplar = new HashSet<string>(CariGruplari, StringComparer.Ordinal);
            var kayitlar = new List<OnekKaydi>();

            foreach (var hesap in plan)
            {
                if (!hesap.Aktif || !gruplar.Contains(hesap.AnaGrup)) continue;

                // Banka isimli cariler: açıklamalarda gönderen/alıcı bankanın adı geçiyor.
                if (Normalizasyon.BankaAdliMi(hesap.Ad)) continue;

                var cekirdek = Normalizasyon.Cekirdek(hesap.Ad);
                if (cekirdek.Length < EnKisaCekirdek) continue;

                // Firmanın kendi adını taşıyan cariler (grup şirketleri, eski kayıtlar).
                if (sahip?.HesapKendisi(cekirdek) == true) continue;

                kayitlar.Add(new OnekKaydi(cekirdek, hesap));
            }

            kayitlar.Sort((a, b) =>
            {
                var fark = string.CompareOrdinal(a.Cekirdek, b.Cekirdek);
                return fark != 0 ? fark : string.CompareOrdinal(a.Kayit.Kod, b.Kayit.Kod);
            });

            return kayitlar.Count == 0 ? Bos : new CariOnekIndeksi(kayitlar.ToArray());
        }

        /// <summary>
        /// Açıklamanın token dizilerini n=4'ten n=2'ye inerek dolaşır ve hesap adı çekirdeği
        /// o diziyle <b>başlayan</b> carileri arar.
        ///
        /// Bulunan hesaplar <b>tüm n seviyelerinden birleştirilir</b>; uzun n-gram'dan gelen
        /// eşleşme listenin başına gelir. Tek hesaba inilirse aday, birden fazlaysa satır
        /// onaya düşer ve hepsi listelenir.
        ///
        /// Birleştirme şart: "KEMAL GÜLMAN VK POLAT GÜLMAN PARK PLAZA 19.KAT" satırında
        /// n=3'teki "PARK PLAZA KAT" tek sonuç veriyor, ama n=2'deki "KEMAL GULMAN" ve
        /// "POLAT GULMAN" de birer sonuç veriyor. Uzun n-gram'ın tek sonucunu kabul etmek
        /// yanlış cariye otomatik kayıt atmak olurdu — bu satırda karşı taraf üç adaydan biri.
        /// </summary>
        /// <param name="altMetinIlkKelimeSarti">
        /// Alt metin yedeğinde eşleşen hesabın ilk kelimesi de metinde geçmeli mi? Ham
        /// açıklamada <b>evet</b> (bkz. <see cref="IcerenlerGuvenli"/>); desenle çıkarılmış
        /// tek kelimelik unvanda <b>hayır</b> — orada metin zaten o tek kelimeden ibaret,
        /// "Superonline" ile <c>329 T06 Turkcell Superonlıne</c> başka türlü eşleşemez.
        /// </param>
        /// <param name="enKisaNgram">
        /// Dolaşılacak en kısa token dizisi. Ham açıklamada 2 (tek kelimelik diziler gürültü
        /// üretiyor); <b>desenle çıkarılmış unvanda 1</b> — orada gürültü yok ve tek kelimelik
        /// unvanlar (Belbim, Superonline, Turknet) ancak böyle bulunur.
        /// </param>
        public OnekSonuc Ara(IReadOnlyList<IReadOnlyList<string>> parcalar, int enKisaNgram = EnKisaNgram,
                             bool altMetinIlkKelimeSarti = true)
        {
            if (_sirali.Length == 0 || parcalar.Count == 0) return OnekSonuc.Yok;

            var onekle = Tara(parcalar, enKisaNgram, OneklerleBaslayanlar);
            if (onekle.Bulundu) return onekle;

            // Yedek: bitişik alt metin. Önek hiç tutmadığında ("Naos İstanbul Kozmetik"
            // carisi banka metninde "NAOSKZ NAOS İSTANBUL KOZMETİK" diye geçiyor) hesap
            // adının ortasından yakalar. Aynı karar kuralı: tek sonuç kabul, çoklu onaya.
            var altMetinle = Tara(parcalar, enKisaNgram,
                ngram => altMetinIlkKelimeSarti ? IcerenlerGuvenli(ngram, parcalar) : Icerenler(ngram));
            return altMetinle.Bulundu
                ? new OnekSonuc { Anahtar = altMetinle.Anahtar, Hesaplar = altMetinle.Hesaplar, AltMetinYedegi = true }
                : OnekSonuc.Yok;
        }

        /// <summary>Tek parçalı arama (hesap sahibi maskelemesi gerekmeyen çağrılar için).</summary>
        public OnekSonuc Ara(IReadOnlyList<string> tokenlar, int enKisaNgram = EnKisaNgram,
                             bool altMetinIlkKelimeSarti = true)
            => Ara(new[] { tokenlar }, enKisaNgram, altMetinIlkKelimeSarti);

        /// <summary>
        /// n-gram'lar <b>parça sınırını aşmaz</b>: hesap sahibinin adı çıkarıldığında iki
        /// yanındaki kelimeler yan yana gelip gerçekte açıklamada olmayan bir dizi üretmemeli.
        ///
        /// Sonuç kırpılmaz: aday kümesinin özeti (öğrenilen belirsizlik kararının güvenlik
        /// kaydı) <b>tam küme</b> üzerinden hesaplanmalı. Ekranda gösterilecek sayıyı çağıran
        /// sınırlar.
        /// </summary>
        private OnekSonuc Tara(IReadOnlyList<IReadOnlyList<string>> parcalar, int enKisaNgram,
                               Func<string, List<HesapPlaniKaydi>> bul)
        {
            var enUzun = Math.Min(EnUzunNgram, parcalar.Max(p => p.Count));
            if (enUzun < enKisaNgram) return OnekSonuc.Yok;

            // Kod → (bulunduğu en uzun n-gram, kayıt). Uzun n-gram'dan gelen önce listelenir.
            var bulunanlar = new Dictionary<string, (int Uzunluk, HesapPlaniKaydi Kayit)>(StringComparer.Ordinal);
            var anahtarlar = new List<string>();

            for (var n = enUzun; n >= enKisaNgram; n--)
            {
                foreach (var tokenlar in parcalar)
                {
                    for (var i = 0; i + n <= tokenlar.Count; i++)
                    {
                        var ngram = string.Join(' ', tokenlar.Skip(i).Take(n));
                        var yeniAnahtar = false;

                        foreach (var kayit in bul(ngram))
                        {
                            if (bulunanlar.ContainsKey(kayit.Kod)) continue;

                            bulunanlar[kayit.Kod] = (n, kayit);
                            yeniAnahtar = true;
                        }

                        if (yeniAnahtar && !anahtarlar.Contains(ngram, StringComparer.Ordinal))
                            anahtarlar.Add(ngram);
                    }
                }
            }

            if (bulunanlar.Count == 0) return OnekSonuc.Yok;

            return new OnekSonuc
            {
                // Belirsizlik anahtarı: eşleşmeyi üreten n-gram'lar. Birden fazla n-gram
                // eşleştiyse hepsi anahtarın parçası olur, yoksa aynı belirsizlik farklı
                // satırlarda farklı anahtara yazılır ve öğrenme tutmaz.
                Anahtar = string.Join(" | ", anahtarlar),
                Hesaplar = bulunanlar.Values
                    .OrderByDescending(v => v.Uzunluk)
                    .ThenBy(v => v.Kayit.Kod, StringComparer.Ordinal)
                    .Select(v => v.Kayit)
                    .ToList()
            };
        }

        /// <summary>
        /// Çekirdeği verilen n-gram ile başlayan hesaplar. Ordinal sıralı dizide bir önekle
        /// başlayan kayıtlar bitişik bir blok oluşturur; blok ikili aramayla bulunur.
        ///
        /// Eşleşme <b>token sınırında</b> biter: "BAYCAN ELEKTRIK" öneki
        /// "BAYCAN ELEKTRIKCILIK" adını tutmamalı.
        /// </summary>
        public List<HesapPlaniKaydi> OneklerleBaslayanlar(string ngram)
        {
            var sonuc = new List<HesapPlaniKaydi>();
            if (ngram.Length == 0) return sonuc;

            for (var i = AltSinir(ngram); i < _sirali.Length; i++)
            {
                var cekirdek = _sirali[i].Cekirdek;
                if (!cekirdek.StartsWith(ngram, StringComparison.Ordinal)) break;
                if (cekirdek.Length != ngram.Length && cekirdek[ngram.Length] != ' ') continue;

                sonuc.Add(_sirali[i].Kayit);
            }

            return sonuc;
        }

        /// <summary>
        /// Çekirdeği verilen n-gram'ı token sınırlarıyla <b>içeren</b> hesaplar (yedek katman).
        /// İndeks sıralaması burada işe yaramadığı için tam tarama yapılır; yalnız önek hiç
        /// tutmadığında çağrıldığı için maliyeti satır başına değil, çözülemeyen satır başınadır.
        /// </summary>
        public List<HesapPlaniKaydi> Icerenler(string ngram)
        {
            var sonuc = new List<HesapPlaniKaydi>();
            if (ngram.Length == 0) return sonuc;

            foreach (var kayit in _sirali)
                if (Normalizasyon.IfadeVarMi(kayit.Cekirdek, ngram))
                    sonuc.Add(kayit.Kayit);

            return sonuc;
        }

        /// <summary>
        /// Alt metin yedeğinin güvenli hâli: eşleşen hesabın <b>ilk kelimesi</b> de metinde
        /// geçmiş olmalı.
        ///
        /// Yedek katman, açıklamanın unvanın önüne bir şey eklediği durum için var
        /// ("NAOSKZ NAOS İSTANBUL KOZMETİK" → "Naos İstanbul Kozmetik"). Tersi geçerli
        /// değil: <b>hesabın</b> adında olup metinde hiç geçmeyen bir ilk kelime varsa
        /// eşleşme başka bir firmayı gösteriyordur. Ölçülen örnek —
        /// "… SAĞLAMOĞLU YETKİLİ MÜESSESE ANONİM ŞİRKETİ hesabından …" metni
        /// <c>120 H30 Hakan Yetkili Müessese</c> hesabının ortasına ("YETKILI MUESSESE")
        /// oturuyor ve satırı yanlış cariye çözüyordu.
        /// </summary>
        private List<HesapPlaniKaydi> IcerenlerGuvenli(string ngram, IReadOnlyList<IReadOnlyList<string>> parcalar)
        {
            var sonuc = new List<HesapPlaniKaydi>();
            if (ngram.Length == 0) return sonuc;

            foreach (var kayit in _sirali)
            {
                if (!Normalizasyon.IfadeVarMi(kayit.Cekirdek, ngram)) continue;

                var ilkKelime = kayit.Cekirdek.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                if (parcalar.Any(p => p.Contains(ilkKelime, StringComparer.Ordinal)))
                    sonuc.Add(kayit.Kayit);
            }

            return sonuc;
        }

        /// <summary>Çekirdeği <paramref name="deger"/>'den küçük olmayan ilk kaydın indeksi.</summary>
        private int AltSinir(string deger)
        {
            var alt = 0;
            var ust = _sirali.Length;

            while (alt < ust)
            {
                var orta = alt + ((ust - alt) >> 1);
                if (string.CompareOrdinal(_sirali[orta].Cekirdek, deger) < 0) alt = orta + 1;
                else ust = orta;
            }

            return alt;
        }

        // ---- Yön kuralı (madde 2) ----

        /// <summary>Para girişinde tercih sırası; ilk sırada tek aday kalırsa o seçilir.</summary>
        private static readonly string[] GirenGruplari = { "120", "159" };

        /// <summary>Para çıkışında tercih sırası.</summary>
        private static readonly string[] CikanGruplari = { "329", "320" };

        /// <summary>
        /// Sahte belirsizliği yön kuralıyla çözer.
        ///
        /// Onaya düşen satırların büyük kısmı gerçek belirsizlik değil, <b>aynı carinin iki
        /// grup altındaki kopyası</b>: gerçek dosyada Zafer Genç, Burak Günel, Yurtiçi Kargo,
        /// Aras Kargo, Ufuk Çolak — hepsi 159 + 329 çifti ve hesap adları birebir aynı.
        ///
        /// Adayların hesap adı çekirdeği aynı ve fark yalnız ana gruptaysa yön karar verir:
        /// para çıkıyorsa 329/320, giriyorsa 120/159. Adlar <b>farklıysa</b> (Park Plaza
        /// Aidat / Elektrik / 19. Kat, Pardus Portföy fonları, Cms Jant / Cms Jant Makina)
        /// belirsizlik gerçektir ve satır onaya düşmeye devam eder.
        /// </summary>
        public static HesapPlaniKaydi? YonleCoz(IReadOnlyList<HesapPlaniKaydi> adaylar, Yon yon)
        {
            if (adaylar.Count < 2) return null;

            var ilk = Normalizasyon.Cekirdek(adaylar[0].Ad);
            if (adaylar.Any(a => !string.Equals(Normalizasyon.Cekirdek(a.Ad), ilk, StringComparison.Ordinal)))
                return null;

            foreach (var grup in yon == Yon.Giren ? GirenGruplari : CikanGruplari)
            {
                var uyanlar = adaylar.Where(a => string.Equals(a.AnaGrup, grup, StringComparison.Ordinal)).ToList();
                if (uyanlar.Count == 1) return uyanlar[0];
            }

            return null;
        }

        // ---- Belirsizlik öğrenmesi (madde 3) ----

        /// <summary>
        /// Aday kümesinin özeti: kod listesinin sıralı hash'i.
        ///
        /// Öğrenilen belirsizlik kararı bu özetle birlikte saklanır. Yeni bir cari açılıp
        /// aday kümesi değişirse eski karar sessizce uygulanmaz, satır tekrar onaya düşer —
        /// aksi hâlde yeni açılan bir Park Plaza hesabı hiç görünmez olurdu.
        /// </summary>
        public static string AdayOzeti(IEnumerable<string> kodlar)
        {
            var sirali = kodlar.Select(Normalizasyon.HesapKoduNormalize)
                               .Where(k => k.Length > 0)
                               .Distinct(StringComparer.Ordinal)
                               .OrderBy(k => k, StringComparer.Ordinal);

            var birlesik = string.Join('|', sirali);
            if (birlesik.Length == 0) return string.Empty;

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(birlesik));
            return Convert.ToHexString(hash, 0, 16);
        }
    }
}
