using CatalogService.Api.Features.FirmaPortfoyu.Domain;

namespace CatalogService.Api.Features.FirmaPortfoyu.Services
{
    /// <summary>
    /// Firma Portföyü'nün bütün hesap kuralları TEK yerde; saf (veritabanı yok), testli. Önizleme ve kayıt aynı
    /// fonksiyondan geçer (<see cref="Degerlendir"/>).
    ///
    /// İşaret: bakiye borç-pozitif saklanır (Borç − Alacak). Gelir hesapları alacak bakiyeli olduğundan net satış
    /// = −Σ(600–602) − Σ(610–612); gider ve maliyetler pozitif çıkar.
    ///
    /// 6'lı ↔ 7'li: Firma Kontrol'ün gelir tablosundaki kuralın aynısı (kod paylaşılmaz, istemcide). 6'lı hesap
    /// boşsa tutar 7'li karşılığından alınır; 6'lı doluysa 7'liye dokunulmaz — çift sayma yok.
    /// </summary>
    public static class PortfoyHesaplayici
    {
        /// <summary>Denge toleransı (kuruş yuvarlaması).</summary>
        public const decimal Tolerans = 0.01m;

        public const string GelirDusukUyarisi =
            "Gelir önceki aydan düşük: dönemlik mizan yüklenmiş olabilir veya düzeltme kaydı var.";

        public const string GelirTablosuYokUyarisi =
            "Gelir tablosu hesabı (600–689) yok: gelir/gider göstergeleri hesaplanamaz.";

        public const string KapanisSonrasiUyarisi =
            "Aralık mizanı kapanış kayıtlarından sonra alınmış (6xx sıfır, 690/692 ya da 590/591 dolu): " +
            "Aralık'ın aylık gelir/gider farkı hesaplanmaz. Kapanış öncesi mizanı yüklemeniz önerilir.";

        public const string IkiGiderYontemiUyarisi =
            "Hem 630–632 (7/B) hem 750/760/770 (7/A) dolu: faaliyet giderleri 630–632'den alındı, 7'li hesaplar çift sayılmadı.";

        /// <summary>Hesap kodu → bakiye (borç-pozitif). Aynı kod bir kez.</summary>
        public static IReadOnlyDictionary<string, decimal> Bakiyeler(IEnumerable<AylikMizanSatiri> satirlar)
            => satirlar.GroupBy(s => s.HesapKodu).ToDictionary(g => g.Key, g => g.Sum(s => s.Bakiye));

        private static decimal B(IReadOnlyDictionary<string, decimal> b, string kod) => b.TryGetValue(kod, out var v) ? v : 0m;

        private static decimal Toplam(IReadOnlyDictionary<string, decimal> b, int bas, int bit)
        {
            var t = 0m;
            for (var k = bas; k <= bit; k++) t += B(b, k.ToString());
            return t;
        }

        private static bool Dolu(IReadOnlyDictionary<string, decimal> b, string kod) => Math.Abs(B(b, kod)) >= Tolerans;

        /// <summary>Net satışlar = 600+601+602 − (610+611+612); kümülatif (mizan nasıl geldiyse).</summary>
        public static decimal NetSatis(IReadOnlyDictionary<string, decimal> b)
            => -Toplam(b, 600, 602) - Toplam(b, 610, 612);

        /// <summary>SMM = 620+621+622+623; 622 boşsa 740 (Hizmet üretim maliyeti, 7/A).</summary>
        public static decimal Smm(IReadOnlyDictionary<string, decimal> b)
            => B(b, "620") + B(b, "621") + (Dolu(b, "622") ? B(b, "622") : B(b, "740")) + B(b, "623");

        /// <summary>Faaliyet giderleri hangi hesaplarda: 630–632 (7/B), 750/760/770 (7/A) ya da ikisi.</summary>
        public static GiderYontemi GiderYontemiBul(IReadOnlyDictionary<string, decimal> b)
        {
            var yediB = Dolu(b, "630") || Dolu(b, "631") || Dolu(b, "632");
            var yediA = Dolu(b, "750") || Dolu(b, "760") || Dolu(b, "770");
            return (yediB, yediA) switch
            {
                (true, true) => GiderYontemi.Ikisi,
                (true, false) => GiderYontemi.YediB,
                (false, true) => GiderYontemi.YediA,
                _ => GiderYontemi.Belirsiz
            };
        }

        /// <summary>
        /// Faaliyet giderleri = 630+631+632 (7/B) VEYA 750+760+770 (7/A; 751/761/771 yansıtma hesapları hariç —
        /// zaten ayrı kodlar). İkisi de doluysa 630–632 alınır (7/A'da aylık yansıtma yapan firmada ikisi de
        /// meşru olarak doludur; aynı para iki kez sayılmaz).
        /// </summary>
        public static decimal FaaliyetGiderleri(IReadOnlyDictionary<string, decimal> b)
            => GiderYontemiBul(b) == GiderYontemi.YediA
                ? B(b, "750") + B(b, "760") + B(b, "770")
                : Toplam(b, 630, 632);

        /// <summary>Gelir tablosu hesabı (600–689) dolu mu.</summary>
        public static bool GelirTablosuVar(IReadOnlyDictionary<string, decimal> b)
        {
            for (var k = 600; k <= 689; k++)
                if (Dolu(b, k.ToString())) return true;
            return false;
        }

        /// <summary>Aralık mizanı kapanış sonrası mı: 6xx sıfır, 690/692 ya da 590/591 dolu.</summary>
        public static bool KapanisSonrasi(int ay, IReadOnlyDictionary<string, decimal> b)
            => ay == 12 && !GelirTablosuVar(b)
               && (Dolu(b, "690") || Dolu(b, "692") || Dolu(b, "590") || Dolu(b, "591"));

        /// <summary>
        /// Gelir düşüş kontrolünde kullanılan kümülatif ana gelir: 600, yoksa 601, yoksa 602 (alacak bakiyesi
        /// artı). Hiçbiri yoksa null.
        /// </summary>
        public static decimal? AnaGelir(IReadOnlyDictionary<string, decimal> b)
        {
            foreach (var kod in new[] { "600", "601", "602" })
                if (Dolu(b, kod)) return -B(b, kod);
            return null;
        }

        /// <summary>
        /// Kümülatif ana gelir önceki aydan düşük mü. Yalnız aynı yılın önceki ayı (Ocak'ta karşılaştırma yok —
        /// kümülatif yeniden başlar); önceki ay yüklenmemişse ya da gelir yoksa false.
        /// </summary>
        public static bool GelirDustu(IReadOnlyDictionary<string, decimal> bu, IReadOnlyDictionary<string, decimal>? onceki)
        {
            if (onceki is null) return false;
            var g = AnaGelir(bu);
            var o = AnaGelir(onceki);
            return g is not null && o is not null && g.Value < o.Value - Tolerans;
        }

        /// <summary>Bir mizanın değerlendirmesi — önizleme ve kayıt aynı sonucu kullanır.</summary>
        public sealed record Degerlendirme(
            int SatirSayisi,
            decimal BorcToplami,
            decimal AlacakToplami,
            decimal DengeFarki,
            bool Dengeli,
            bool KapanisSonrasi,
            GiderYontemi GiderYontemi,
            bool GelirTablosuVar,
            decimal? NetSatis,
            decimal? Smm,
            decimal? FaaliyetGiderleri,
            IReadOnlyList<string> Uyarilar)
        {
            /// <summary>"Mizan sorunlu": yalnız dengesiz mizan. Uyarılar (gelir düşük, 6xx yok) sorunlu saymaz.</summary>
            public bool Sorunlu => !Dengeli;
        }

        /// <summary>
        /// Mizanı değerlendirir. <paramref name="oncekiAy"/> aynı yılın önceki ayının bakiyeleri (yüklenmemişse
        /// ya da Ocak ise null). Değerler KÜMÜLATİF (mizan nasıl geldiyse); aylık fark Aşama 2'de.
        /// </summary>
        public static Degerlendirme Degerlendir(int ay, IReadOnlyCollection<AylikMizanSatiri> satirlar,
                                                IReadOnlyDictionary<string, decimal>? oncekiAy)
        {
            var b = Bakiyeler(satirlar);
            var borc = Math.Round(satirlar.Sum(s => s.BorcBakiye), 2);
            var alacak = Math.Round(satirlar.Sum(s => s.AlacakBakiye), 2);
            var fark = Math.Round(borc - alacak, 2);
            var dengeli = Math.Abs(fark) < Tolerans;

            var gelirVar = GelirTablosuVar(b);

            return new Degerlendirme(
                satirlar.Count, borc, alacak, fark, dengeli, KapanisSonrasi(ay, b), GiderYontemiBul(b), gelirVar,
                gelirVar ? NetSatis(b) : null,
                gelirVar ? Smm(b) : null,
                gelirVar ? FaaliyetGiderleri(b) : null,
                Uyarilar(ay, b, oncekiAy, fark));
        }

        /// <summary>
        /// Mizanın uyarıları. Önizleme, kayıt ve firma × ay matrisi aynı listeyi gösterir; matris dengeyi kayıttan
        /// (<paramref name="dengeFarki"/>) verir, bakiyeleri yalnız gelir tablosu/kapanış hesaplarından.
        /// </summary>
        public static List<string> Uyarilar(int ay, IReadOnlyDictionary<string, decimal> b,
                                            IReadOnlyDictionary<string, decimal>? oncekiAy, decimal dengeFarki)
        {
            var uyarilar = new List<string>();
            if (Math.Abs(dengeFarki) >= Tolerans)
                uyarilar.Add($"Mizan dengesiz: borç bakiye toplamı alacak bakiye toplamından {Tl(dengeFarki)} farklı. " +
                             "Mizan sorunlu olarak işaretlenir.");
            if (KapanisSonrasi(ay, b)) uyarilar.Add(KapanisSonrasiUyarisi);
            else if (!GelirTablosuVar(b)) uyarilar.Add(GelirTablosuYokUyarisi);
            if (GiderYontemiBul(b) == GiderYontemi.Ikisi) uyarilar.Add(IkiGiderYontemiUyarisi);
            if (ay > 1 && GelirDustu(b, oncekiAy)) uyarilar.Add(GelirDusukUyarisi);
            return uyarilar;
        }

        // ================= Aşama 2: göstergeler ve aylık seri =================

        public const string NotYuklenmedi = "mizan yüklenmedi";
        public const string NotOncekiAyEksik = "önceki ay eksik";
        public const string NotKapanisSonrasi = "kapanış sonrası";
        public const string NotGelirTablosuYok = "gelir tablosu hesabı yok";
        public const string NotGelecekAy = "gelecek ay";

        /// <summary>
        /// Dönem kârı (vergi öncesi), kümülatif = −Σ(600–689); 690/691/692 dahil değil. 6'lı ↔ 7'li kuralı SMM ve
        /// faaliyet giderleriyle aynı: 622 boşsa 740; 630–632 hiç dolu değilse 750+760+770; 660 boşsa 780.
        /// </summary>
        public static decimal DonemKari(IReadOnlyDictionary<string, decimal> b)
        {
            var t = Toplam(b, 600, 689);
            if (!Dolu(b, "622")) t += B(b, "740");
            if (GiderYontemiBul(b) == GiderYontemi.YediA) t += B(b, "750") + B(b, "760") + B(b, "770");
            if (!Dolu(b, "660")) t += B(b, "780");
            return -t;
        }

        /// <summary>Hazır değerler = 100+101+102+103+108 (103 Verilen Çekler alacak bakiyeli, kendiliğinden düşer).</summary>
        public static decimal HazirDegerler(IReadOnlyDictionary<string, decimal> b)
            => B(b, "100") + B(b, "101") + B(b, "102") + B(b, "103") + B(b, "108");

        /// <summary>Ticari alacaklar = 120+121+122 (122 reeskont alacak bakiyeli, düşer).</summary>
        public static decimal TicariAlacaklar(IReadOnlyDictionary<string, decimal> b)
            => B(b, "120") + B(b, "121") + B(b, "122");

        /// <summary>Ticari borçlar = 320+321+322, alacak bakiyesi artı (322 reeskont borç bakiyeli, düşer).</summary>
        public static decimal TicariBorclar(IReadOnlyDictionary<string, decimal> b)
            => -(B(b, "320") + B(b, "321") + B(b, "322"));

        public static decimal DevredenKdv(IReadOnlyDictionary<string, decimal> b) => B(b, "190");

        /// <summary>Bir firma-ayın kayıtlı mizanı (hesaplayıcının girdisi).</summary>
        public sealed record AyMizani(int Yil, int Ay, IReadOnlyDictionary<string, decimal> Bakiyeler, bool Dengeli,
                                      bool KapanisSonrasi, decimal DengeFarki);

        /// <summary>Gelir tablosu kalemlerinin AYLIK tutarı; değer yoksa <see cref="Not"/> nedenini söyler.</summary>
        public sealed record AylikGelirTablosu(decimal? NetSatis, decimal? Smm, decimal? FaaliyetGiderleri,
                                               decimal? DonemKari, string? Not)
        {
            public decimal? BrutKar => NetSatis - Smm;
            public decimal? FaaliyetKari => NetSatis - Smm - FaaliyetGiderleri;

            public static AylikGelirTablosu Yok(string not) => new(null, null, null, null, not);
        }

        /// <summary>Önceki takvim ayı (Ocak → geçen yılın Aralığı).</summary>
        public static (int Yil, int Ay) OncekiAy(int yil, int ay) => ay == 1 ? (yil - 1, 12) : (yil, ay - 1);

        /// <summary>
        /// Gelir tablosu kalemlerinin aylık tutarı. 6xx/7xx yıl başından kümülatif: aylık = bu ay − aynı yılın önceki
        /// ayı; Ocak'ta aylık = Ocak. Önceki ay yüklenmemişse değer yok ("önceki ay eksik") — tahmin yapılmaz.
        ///
        /// Kapanış sonrası Aralık: net satış / SMM / gider hesaplanmaz; dönem kârı yalnız 690 doluysa ve Kasım
        /// yüklüyse = yıllık (−690) − Kasım kümülatifi.
        /// </summary>
        public static AylikGelirTablosu Aylik(Func<int, int, AyMizani?> getir, int yil, int ay)
        {
            var bu = getir(yil, ay);
            if (bu is null) return AylikGelirTablosu.Yok(NotYuklenmedi);

            if (bu.KapanisSonrasi)
            {
                decimal? kar = null;
                if (Dolu(bu.Bakiyeler, "690") && ay > 1 && getir(yil, ay - 1) is { } kasim && !kasim.KapanisSonrasi)
                    kar = -B(bu.Bakiyeler, "690") - DonemKari(kasim.Bakiyeler);
                return new AylikGelirTablosu(null, null, null, kar, NotKapanisSonrasi);
            }

            if (!GelirTablosuVar(bu.Bakiyeler)) return AylikGelirTablosu.Yok(NotGelirTablosuYok);

            var b = bu.Bakiyeler;
            if (ay == 1)
                return new AylikGelirTablosu(NetSatis(b), Smm(b), FaaliyetGiderleri(b), DonemKari(b), null);

            var once = getir(yil, ay - 1);
            if (once is null || once.KapanisSonrasi) return AylikGelirTablosu.Yok(NotOncekiAyEksik);

            // Önceki ay yüklü ama gelir tablosu hesabı yoksa o ayın kümülatifi sıfırdır (henüz hareket yok).
            var o = once.Bakiyeler;
            return new AylikGelirTablosu(
                NetSatis(b) - NetSatis(o),
                Smm(b) - Smm(o),
                FaaliyetGiderleri(b) - FaaliyetGiderleri(o),
                DonemKari(b) - DonemKari(o),
                null);
        }

        /// <summary>
        /// Yıl başından dönem kârı (vergi öncesi) — kümülatif. Kapanış sonrası Aralık'ta yalnız 690 doluysa (−690).
        /// </summary>
        public static decimal? DonemKariYilBasindan(AyMizani? m)
        {
            if (m is null) return null;
            if (m.KapanisSonrasi) return Dolu(m.Bakiyeler, "690") ? -B(m.Bakiyeler, "690") : null;
            return GelirTablosuVar(m.Bakiyeler) ? DonemKari(m.Bakiyeler) : null;
        }

        private static string Tl(decimal v)
            => v.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("tr-TR")) + " ₺";
    }
}
