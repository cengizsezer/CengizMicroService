using WebApp.Application.Services.Interfaces;
using WebApp.Domain.Models.FirmaKontrol;
using WebApp.Shared.Dto.FirmaKontrol;

namespace WebApp.Application.Services
{
    /// <summary>
    /// YÖNETSEL görünüm ve GRUP MATRİSİ hesabı — PROMPT 3B'nin tek hesap yeri.
    ///
    /// ── KURAL: YÖNETSEL GÖRÜNÜM YALNIZCA EKRANDIR ──
    /// Bu sınıfın çıktısı hiçbir yere yazılmaz ve hiçbir resmi hesaba girmez.
    /// <see cref="MizanHesaplayici"/>, <see cref="GelirTablosuCalculator"/>,
    /// <see cref="MaliTabloIsareti"/>, vergi motoru, bilanço, dikey yüzdeler ve dışa aktarım
    /// bu sınıfı ÇAĞIRMAZ; yalnızca <c>GelirTablosuGorunum</c> ve <c>GrupGorunumuTab</c>
    /// bileşenleri kullanır. Girdi yönü tek yönlüdür: yasal rakam buraya OKUNARAK gelir,
    /// buradan hiçbir şey geri yazılmaz.
    ///
    /// Dağılımın kendisi 3A'daki <see cref="EtiketMotoru"/>'dur (kural motoru + çift sayım
    /// koruması); burada aynen çağrılır, yeniden yazılmaz.
    ///
    /// Ciro kümesi: boyutun kapsamındaki hesaplardan yalnızca NET SATIŞ hesapları
    /// (60x brüt satışlar, 61x satış indirimleri). "Gelir dağıtıldı, gider dağıtılmadı" —
    /// boyut gider hesaplarını da kapsasa bile onlar buraya girmez.
    /// </summary>
    public static class GrupGorunumu
    {
        /// <summary>Denklik toleransı (kuruş).</summary>
        public const decimal Tolerans = EtiketMotoru.Tolerans;

        // ── Ciro kümesi ──

        /// <summary>Net satışları oluşturan ana hesap mı (60x, 61x).</summary>
        public static bool NetSatisHesabiMi(string? anaHesapKodu) =>
            anaHesapKodu is { Length: 3 } k && (k.StartsWith("60") || k.StartsWith("61"));

        /// <summary>Ağacı boyutun kapsamına VE net satış hesaplarına indirger.</summary>
        public static List<HesapDugumu> CiroAgaci(IEnumerable<HesapDugumu> agac, string? kapsamHesaplari) =>
            EtiketMotoru.Kapsa(agac, kapsamHesaplari)
                .Where(d => NetSatisHesabiMi(d.AnaHesapKodu))
                .ToList();

        /// <summary>Boyut kapsamında olup ciro kümesine alınmayan ana hesaplar (ekranda not düşülür).</summary>
        public static List<string> CiroDisiKapsamHesaplari(IEnumerable<HesapDugumu> agac, string? kapsamHesaplari) =>
            EtiketMotoru.Kapsa(agac, kapsamHesaplari)
                .Where(d => d.Seviye == 1 && !NetSatisHesabiMi(d.AnaHesapKodu))
                .Select(d => d.Kod)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();

        // ── Yasal görünüm: ana hesap altında bilgi amaçlı etiket kırılımı ──

        public sealed record KirilimSatiri(string Ad, string? Renk, decimal Tutar, bool Atanmamis);

        /// <summary>
        /// Kapsamdaki her ana hesap için etiket kırılımı (ana hesap kodu → satırlar).
        /// Yalnızca en az bir etikete tutar düşen ana hesaplar döner. BİLGİ AMAÇLIDIR:
        /// gelir tablosunun hiçbir toplamını değiştirmez, oraya geri yazılmaz.
        /// Her ana hesap kendi başına dağıtılır; çift sayım koruması EtiketMotoru'ndan gelir.
        /// </summary>
        public static Dictionary<string, List<KirilimSatiri>> AnaHesapKirilimlari(
            IEnumerable<HesapDugumu> agac,
            EtiketBoyutuDto boyut)
        {
            var sonuc = new Dictionary<string, List<KirilimSatiri>>(StringComparer.OrdinalIgnoreCase);
            var degerler = boyut.Degerler.OrderBy(d => d.Sira).ThenBy(d => d.Id).ToList();

            foreach (var grup in EtiketMotoru.Kapsa(agac, boyut.KapsamHesaplari)
                         .GroupBy(d => d.AnaHesapKodu ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                if (grup.Key.Length == 0) continue;

                var dagilim = EtiketMotoru.Dagit(grup, boyut.Kurallar);
                if (dagilim.DegerToplamlari.Values.All(t => t == 0m)) continue;

                var satirlar = degerler
                    .Where(d => dagilim.DegerToplamlari.GetValueOrDefault(d.Id) != 0m)
                    .Select(d => new KirilimSatiri(d.Ad, d.Renk, dagilim.DegerToplamlari[d.Id], false))
                    .ToList();

                if (dagilim.Atanmamis != 0m)
                    satirlar.Add(new KirilimSatiri("Atanmamış", null, dagilim.Atanmamis, true));

                sonuc[grup.Key] = satirlar;
            }

            return sonuc;
        }

        // ── Defter: bir firmanın ağacı + o firmada geçerli kurallarla dağılımı ──

        public sealed class Defter
        {
            public int FirmaId { get; init; }
            public string FirmaAd { get; init; } = string.Empty;
            public HesapAgaciDurumu Durum { get; init; }

            /// <summary>Durum = Yuklu ise dolu.</summary>
            public EtiketMotoru.Dagilim? Dagilim { get; init; }

            /// <summary>
            /// Ciro kümesindeki ANA HESAPLARIN bakiye toplamı — dağılımdan BAĞIMSIZ hesaplanır.
            /// Matrisin satır toplamı budur; hücre toplamıyla karşılaştırılınca çift sayım
            /// yakalanır.
            /// </summary>
            public decimal AnaHesapToplami { get; init; }

            /// <summary>Satırın yanında gösterilecek bilgi notu (örn. boyut bu firmada geçerli değil).</summary>
            public string? Not { get; init; }

            /// <summary>Veri henüz gelmedi (aşamalı çizim: satır iskeletle gösterilir).</summary>
            public bool Bekleniyor { get; init; }

            public bool Yuklu => !Bekleniyor && Durum == HesapAgaciDurumu.Yuklu && Dagilim is not null;
        }

        /// <summary>Henüz okunmamış defter — toplamlara girmez, ekranda iskelet olarak durur.</summary>
        public static Defter BekleyenDefter(int firmaId, string firmaAd) =>
            new() { FirmaId = firmaId, FirmaAd = firmaAd, Durum = HesapAgaciDurumu.Ulasilamadi, Bekleniyor = true };

        public static Defter DefterOlustur(
            int firmaId,
            string firmaAd,
            HesapAgaciSonucu agac,
            string? kapsamHesaplari,
            IEnumerable<EtiketKuraliDto> kurallar,
            string? not = null)
        {
            if (agac.Durum != HesapAgaciDurumu.Yuklu || agac.Dugumler.Count == 0)
            {
                return new Defter
                {
                    FirmaId = firmaId,
                    FirmaAd = firmaAd,
                    Durum = agac.Durum == HesapAgaciDurumu.Yuklu ? HesapAgaciDurumu.MizanYuklenmedi : agac.Durum,
                    Not = not
                };
            }

            var ciro = CiroAgaci(agac.Dugumler, kapsamHesaplari);

            return new Defter
            {
                FirmaId = firmaId,
                FirmaAd = firmaAd,
                Durum = HesapAgaciDurumu.Yuklu,
                Dagilim = EtiketMotoru.Dagit(ciro, kurallar),
                AnaHesapToplami = ciro.Where(d => d.Seviye == 1).Sum(HesapDugumSunumu.Sunum),
                Not = not
            };
        }

        /// <summary>Ulaşılamayan defter (istisna yakalandığında satır bozulmasın diye).</summary>
        public static Defter UlasilamayanDefter(int firmaId, string firmaAd, string? not = null) =>
            new() { FirmaId = firmaId, FirmaAd = firmaAd, Durum = HesapAgaciDurumu.Ulasilamadi, Not = not };

        // ── Hangi firmaların defterine bakılır ──

        /// <summary>
        /// Matris satırları / yönetsel görünümde okunacak defterler: bakılan firma (ilk) +
        /// seçili boyutun değerlerinde FirmaId'si dolu olan firmalar, tekrarsız. Başka firma
        /// çekilmez.
        /// </summary>
        public static List<int> IlgiliFirmalar(int bakilanFirmaId, IEnumerable<EtiketDegeriDto> degerler)
        {
            var sonuc = new List<int> { bakilanFirmaId };

            foreach (var d in degerler.OrderBy(d => d.Sira).ThenBy(d => d.Id))
            {
                if (d.FirmaId is int f && !sonuc.Contains(f))
                    sonuc.Add(f);
            }

            return sonuc;
        }

        // ── Yönetsel görünüm ──

        public sealed record FirmaTutari(int? FirmaId, string Ad, decimal Tutar);

        public sealed class YonetselSonuc
        {
            /// <summary>Gelir tablosundaki "C-NET SATIŞLAR" (yasal), değişmeden.</summary>
            public decimal Yasal { get; init; }

            /// <summary>Bu defterde, FirmaId'si bakılan firmadan FARKLI (ve dolu) etiketlere düşen tutar.</summary>
            public decimal BaskaSirketeEtiketli { get; init; }
            public List<FirmaTutari> BaskaSirketeKirilim { get; init; } = new();

            /// <summary>Diğer defterlerde FirmaId'si bakılan firma olan etiketlere düşen tutar.</summary>
            public decimal DigerDefterlerdenGelen { get; init; }
            public List<FirmaTutari> GelenKirilim { get; init; } = new();

            /// <summary>Mizanı yüklenmemiş / ulaşılamayan defterler — toplam EKSİKTİR.</summary>
            public List<Defter> EksikDefterler { get; init; } = new();

            /// <summary>
            /// Firması tanımsız (FirmaId = null) etiketlere düşen tutar. Bu tutar başka bir
            /// şirkete taşınamaz; Atanmamış gibi defterin sahibinde kalır.
            /// </summary>
            public decimal FirmasizEtiketli { get; init; }

            public decimal Yonetsel => Yasal - BaskaSirketeEtiketli + DigerDefterlerdenGelen;
            public bool Eksik => EksikDefterler.Count > 0;
        }

        /// <summary>
        /// Net satışlar (yönetsel) = yasal − bu defterde başka şirkete etiketli
        ///                           + diğer defterlerde bu şirkete etiketli.
        /// Etiketsiz (Atanmamış) ve firması tanımsız etiketli tutarlar defterin sahibinde kalır.
        /// </summary>
        public static YonetselSonuc YonetselHesapla(
            int bakilanFirmaId,
            decimal yasalNetSatislar,
            Defter kendiDefteri,
            IEnumerable<Defter> digerDefterler,
            IReadOnlyList<EtiketDegeriDto> degerler)
        {
            var degerById = degerler.ToDictionary(d => d.Id);

            var giden = new List<FirmaTutari>();
            decimal firmasiz = 0m;

            if (kendiDefteri.Dagilim is { } kendi)
            {
                foreach (var (degerId, tutar) in kendi.DegerToplamlari)
                {
                    if (!degerById.TryGetValue(degerId, out var d)) continue;

                    if (d.FirmaId is null) firmasiz += tutar;
                    else if (d.FirmaId != bakilanFirmaId) giden.Add(new FirmaTutari(d.FirmaId, d.Ad, tutar));
                }
            }

            var gelen = new List<FirmaTutari>();
            var eksik = new List<Defter>();

            foreach (var defter in digerDefterler.Where(x => x.FirmaId != bakilanFirmaId))
            {
                if (!defter.Yuklu)
                {
                    eksik.Add(defter);
                    continue;
                }

                var tutar = defter.Dagilim!.DegerToplamlari
                    .Where(kv => degerById.TryGetValue(kv.Key, out var d) && d.FirmaId == bakilanFirmaId)
                    .Sum(kv => kv.Value);

                gelen.Add(new FirmaTutari(defter.FirmaId, defter.FirmaAd, tutar));
            }

            return new YonetselSonuc
            {
                Yasal = yasalNetSatislar,
                BaskaSirketeEtiketli = giden.Sum(x => x.Tutar),
                BaskaSirketeKirilim = giden,
                DigerDefterlerdenGelen = gelen.Sum(x => x.Tutar),
                GelenKirilim = gelen,
                EksikDefterler = eksik,
                FirmasizEtiketli = firmasiz
            };
        }

        // ── Grup matrisi ──

        public sealed class MatrisSatiri
        {
            public Defter Defter { get; init; } = default!;

            /// <summary>Değer Id → o defterde o etikete düşen tutar.</summary>
            public Dictionary<int, decimal> Hucreler { get; init; } = new();
            public decimal Atanmamis { get; init; }

            /// <summary>Ana hesap bakiyelerinin toplamı (hücrelerden bağımsız).</summary>
            public decimal SatirToplami { get; init; }

            public decimal HucreToplami => Hucreler.Values.Sum() + Atanmamis;
        }

        public sealed class Matris
        {
            public List<MatrisSatiri> Satirlar { get; init; } = new();
            public Dictionary<int, decimal> SutunToplamlari { get; init; } = new();
            public decimal AtanmamisToplami { get; init; }

            /// <summary>Yüklü satırların satır toplamlarının toplamı.</summary>
            public decimal SatirToplamlariToplami { get; init; }

            /// <summary>Sütun toplamlarının (değerler + Atanmamış) toplamı.</summary>
            public decimal SutunToplamlariToplami { get; init; }

            /// <summary>
            /// Satır toplamları ≠ sütun toplamları → bir hesap iki kurala birden uymuş
            /// (ya da bir tutar iki kez sayılmış) demektir. Ekranda kırmızı uyarı.
            /// </summary>
            public bool CiftSayimVar => !Denk(SatirToplamlariToplami, SutunToplamlariToplami);
        }

        public static bool Denk(decimal a, decimal b) => Math.Abs(a - b) <= Tolerans;

        /// <summary>
        /// Satır = kimin defteri · sütun = gelir gerçekte kime ait. Yüklü olmayan satırlar
        /// hücre üretmez ve toplamlara girmez (dağılımı bilinmiyor).
        /// </summary>
        public static Matris MatrisOlustur(IEnumerable<Defter> defterler, IReadOnlyList<EtiketDegeriDto> degerler)
        {
            var satirlar = new List<MatrisSatiri>();

            foreach (var defter in defterler)
            {
                if (!defter.Yuklu)
                {
                    satirlar.Add(new MatrisSatiri { Defter = defter });
                    continue;
                }

                var hucreler = degerler.ToDictionary(
                    d => d.Id,
                    d => defter.Dagilim!.DegerToplamlari.GetValueOrDefault(d.Id));

                satirlar.Add(new MatrisSatiri
                {
                    Defter = defter,
                    Hucreler = hucreler,
                    Atanmamis = defter.Dagilim!.Atanmamis,
                    SatirToplami = defter.AnaHesapToplami
                });
            }

            return MatrisTopla(satirlar, degerler);
        }

        /// <summary>Sütun toplamlarını ve çift sayım kontrolünü satırlardan çıkarır.</summary>
        public static Matris MatrisTopla(List<MatrisSatiri> satirlar, IReadOnlyList<EtiketDegeriDto> degerler)
        {
            var yuklu = satirlar.Where(s => s.Defter.Yuklu).ToList();

            var sutunlar = degerler.ToDictionary(
                d => d.Id,
                d => yuklu.Sum(s => s.Hucreler.GetValueOrDefault(d.Id)));

            var atanmamis = yuklu.Sum(s => s.Atanmamis);

            return new Matris
            {
                Satirlar = satirlar,
                SutunToplamlari = sutunlar,
                AtanmamisToplami = atanmamis,
                SatirToplamlariToplami = yuklu.Sum(s => s.SatirToplami),
                SutunToplamlariToplami = sutunlar.Values.Sum() + atanmamis
            };
        }
    }
}
