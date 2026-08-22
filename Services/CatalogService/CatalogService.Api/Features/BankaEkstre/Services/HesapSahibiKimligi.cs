namespace CatalogService.Api.Features.BankaEkstre.Services
{
    /// <summary>
    /// Hesap sahibinin (firmanın) kendini banka açıklamalarında bulduğu <b>tüm</b> yazımları.
    ///
    /// Tek metin alanı yetmiyordu: bankalar aynı firmayı çok farklı yazıyor. Gerçek dosyada
    /// sayılan yazımlar ve geçiş sayıları:
    /// <code>
    /// PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ   168
    /// ADAY BAĞIMSIZ DENETİM                      135
    /// PKF ADAY BAĞIMSIZ DENETİM A.Ş.             128
    /// PKF ADAY                                    22
    /// ADAY BAĞIMSIZ DENETİM VE SMMM A.Ş.
    /// PKF ADAY BAĞIMSIZ DENETİM AŞ.
    /// </code>
    /// Kullanıcı tek yazım girdiğinde kalanlar elenmiyor ve karşı taraf sanılıyordu.
    ///
    /// Karşılaştırma <b>çekirdek eşitliği değil kapsama</b> ile yapılır: "PKF ADAY BAGIMSIZ
    /// DENETIM" ile "ADAY BAGIMSIZ DENETIM" çekirdek olarak eşit değil ama aynı firma.
    /// </summary>
    public sealed class HesapSahibiKimligi
    {
        /// <summary>Hiç unvan tanımlanmamış hesap; eleme yapılmaz.</summary>
        public static readonly HesapSahibiKimligi Yok = new(Array.Empty<string>());

        /// <summary>
        /// Kapsama kontrolüne girecek en kısa çekirdek. Kısa bir yazım ("ADAY") tüm
        /// carileri eleyebilirdi; benzersiz önek indeksindeki alt sınırla aynı tutuldu.
        /// </summary>
        public const int EnKisaCekirdek = 6;

        private readonly IReadOnlyList<string> _cekirdekler;

        private HesapSahibiKimligi(IReadOnlyList<string> cekirdekler) => _cekirdekler = cekirdekler;

        /// <summary>Tanımlı yazımların normalize çekirdekleri (ekranda göstermek için).</summary>
        public IReadOnlyList<string> Cekirdekler => _cekirdekler;

        public bool Bos => _cekirdekler.Count == 0;

        /// <summary>
        /// Ana unvan + takma adlardan kimlik kurar. Takma adlar satır satır tek metin
        /// alanında durur (bkz. <see cref="Ayikla"/>).
        /// </summary>
        public static HesapSahibiKimligi Kur(string? anaUnvan, string? takmaAdlar = null)
            => Kur(Ayikla(anaUnvan).Concat(Ayikla(takmaAdlar)));

        public static HesapSahibiKimligi Kur(IEnumerable<string?> yazimlar)
        {
            var cekirdekler = new List<string>();

            foreach (var yazim in yazimlar)
            {
                var cekirdek = Normalizasyon.UnvanCekirdek(yazim);
                if (cekirdek.Length < EnKisaCekirdek) continue;
                if (cekirdekler.Contains(cekirdek, StringComparer.Ordinal)) continue;

                cekirdekler.Add(cekirdek);
            }

            return cekirdekler.Count == 0 ? Yok : new HesapSahibiKimligi(cekirdekler);
        }

        /// <summary>Verilen unvan hesap sahibinin kendisi mi?</summary>
        public bool Kendisi(string? unvan)
        {
            if (Bos) return false;

            var cekirdek = Normalizasyon.UnvanCekirdek(unvan);
            if (cekirdek.Length == 0) return false;

            return _cekirdekler.Any(s => Normalizasyon.CekirdekKapsiyorMu(s, cekirdek));
        }

        /// <summary>
        /// Hesap planı kaydı hesap sahibinin kendisi mi? Benzersiz önek indeksi bu kontrolle
        /// süzülür: firmanın kendi adını taşıyan cariler indekste kalırsa açıklamalarda
        /// firmanın kendi unvanı geçtiği için her satır onlara eşleşir.
        ///
        /// Hesap adı çekirdeği <see cref="Normalizasyon.Cekirdek"/> ile üretilir, unvan
        /// çekirdeğiyle değil; ikisi farklı token kümesi verir ve indeks bu boru hattını kullanır.
        /// </summary>
        public bool HesapKendisi(string? hesapCekirdegi)
        {
            if (Bos || string.IsNullOrEmpty(hesapCekirdegi)) return false;

            return _cekirdekler.Any(s => Normalizasyon.CekirdekKapsiyorMu(s, hesapCekirdegi));
        }

        /// <summary>
        /// Açıklamanın token dizisinden hesap sahibinin kendi adını çıkarır ve kalan
        /// <b>parçaları ayrı ayrı</b> döner.
        ///
        /// Neden şart: ölçülen 287 satırın 268'inde açıklamada firmanın kendi unvanı geçiyor.
        /// Çıkarılmazsa benzersiz önek katmanı "BAGIMSIZ DENETIM" dizisini üretir ve
        /// <c>120 B58 Bağımsız Denetim Derneği</c> gibi <b>başka</b> bir cariye eşler.
        ///
        /// Parçalar birleştirilmez: çıkarılan adın iki yanındaki kelimeler yan yana gelip
        /// gerçekte açıklamada olmayan bir token dizisi üretmemeli.
        /// </summary>
        public List<IReadOnlyList<string>> Parcala(IReadOnlyList<string> tokenlar)
        {
            var parcalar = new List<IReadOnlyList<string>>();
            if (tokenlar.Count == 0) return parcalar;

            if (Bos)
            {
                parcalar.Add(tokenlar);
                return parcalar;
            }

            var gecerli = new List<string>();
            var i = 0;

            while (i < tokenlar.Count)
            {
                var uzunluk = SahipDizisiUzunlugu(tokenlar, i);
                if (uzunluk == 0)
                {
                    gecerli.Add(tokenlar[i]);
                    i++;
                    continue;
                }

                if (gecerli.Count > 0)
                {
                    parcalar.Add(gecerli);
                    gecerli = new List<string>();
                }

                i += uzunluk;
            }

            if (gecerli.Count > 0) parcalar.Add(gecerli);
            return parcalar;
        }

        /// <summary>
        /// <paramref name="bas"/>'tan başlayan ve tanımlı çekirdeklerden birinin içinde tam
        /// kelime dizisi olarak geçen en uzun token dizisinin uzunluğu; yoksa 0. En az iki
        /// token aranır — tek kelime ("ADAY", "PKF") başka bir cariyi de eleyebilirdi.
        /// </summary>
        private int SahipDizisiUzunlugu(IReadOnlyList<string> tokenlar, int bas)
        {
            var enFazla = Math.Min(EnUzunDizi, tokenlar.Count - bas);

            for (var uzunluk = enFazla; uzunluk >= 2; uzunluk--)
            {
                var ifade = string.Join(' ', tokenlar.Skip(bas).Take(uzunluk));
                if (_cekirdekler.Any(c => Normalizasyon.IfadeVarMi(c, ifade))) return uzunluk;
            }

            return 0;
        }

        /// <summary>Tanımlı çekirdeklerin en uzunundaki token sayısı; tarama bununla sınırlanır.</summary>
        private int EnUzunDizi => _enUzunDizi ??= _cekirdekler
            .Select(c => c.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length)
            .DefaultIfEmpty(0)
            .Max();

        private int? _enUzunDizi;

        /// <summary>
        /// Satır satır (veya noktalı virgülle) ayrılmış metni tek tek yazımlara böler.
        /// Virgül ayraç değildir: unvanların içinde geçebiliyor.
        /// </summary>
        public static IEnumerable<string> Ayikla(string? metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) yield break;

            foreach (var parca in metin.Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var temiz = parca.Trim();
                if (temiz.Length > 0) yield return temiz;
            }
        }

        /// <summary>
        /// Tek metinden kimlik: mevcut çağrıların (<c>Cikar(..., "PKF ADAY …")</c>) imzası
        /// değişmeden çalışması için.
        /// </summary>
        public static implicit operator HesapSahibiKimligi(string? unvan) => Kur(unvan);
    }
}
