using WebApp.Domain.Models.FirmaKontrol;

namespace WebApp.Application.Services
{
    /// <summary>
    /// Dikey Yüzdeler ekranının SUNUM modeli (PROMPT 4). Hiçbir tutar burada yeniden
    /// hesaplanmaz: bilanço satırları <see cref="MizanHesaplayici.Compute"/>'tan, gelir
    /// tablosu satırları <see cref="GelirTablosuCalculator.Compute"/>'tan OKUNUR; bu sınıf
    /// yalnızca satır türünü, yüzdeyi (tutar / payda) ve Özet Grafik gruplamasını üretir.
    ///
    /// Paydalar eskisiyle aynı:
    ///  • Bilanço: aktif kalemleri Aktif Total'ine, pasif kalemleri Pasif Total'ine
    ///    (dönem kârı düzeltmesi eklenmez — eski Dikey Yüzdeler de eklemiyordu).
    ///  • Gelir tablosu: "C-NET SATIŞLAR" ara toplamına.
    ///
    /// İki farklı ÇUBUK kuralı bilerek var:
    ///  • Görünüm 1-2 (<see cref="CubukDikey"/>): genişlik = |dikey %|, tam genişlik = %100.
    ///  • Özet Grafik (<see cref="CubukBolumEnBuyugu"/>): genişlik = kalem / bölümün en büyük
    ///    kalemi (sıralama okunsun diye); yanında gerçek yüzde ayrıca yazılır.
    /// </summary>
    public static class DikeyYuzdeSunumu
    {
        public const decimal Tolerans = 0.01m;

        public enum SatirTuru { Bolum, Roma, AltGrup, Hesap, AraToplam, Toplam, TicariKar, DonemNetKari }

        public sealed record Satir(
            SatirTuru Tur,
            string Kod,
            string Ad,
            decimal? Onceki,
            decimal? Cari,
            decimal? Yuzde)
        {
            /// <summary>Tutar ya da önceki dönem hareketi var mı ("Sadece hareket görenler").</summary>
            public bool HareketVar => (Cari ?? 0m) != 0m || (Onceki ?? 0m) != 0m;
        }

        // ── Çubuk kuralları ──

        /// <summary>Görünüm 1-2: genişlik yüzdesi = |dikey %|, en fazla 100.</summary>
        public static decimal CubukDikey(decimal? yuzde) =>
            yuzde is decimal y ? Math.Min(Math.Abs(y), 100m) : 0m;

        /// <summary>Özet Grafik: genişlik yüzdesi = |tutar| / bölümün en büyük |kalemi|.</summary>
        public static decimal CubukBolumEnBuyugu(decimal tutar, decimal enBuyuk) =>
            enBuyuk == 0m ? 0m : Math.Min(Math.Abs(tutar) / Math.Abs(enBuyuk) * 100m, 100m);

        public static decimal? Oran(decimal? pay, decimal? payda) =>
            pay is decimal p && payda is decimal d && d != 0m ? p / d * 100m : null;

        // ── Görünüm 1: Bilanço ──

        public sealed class BilancoSonucu
        {
            public List<Satir> Satirlar { get; } = new();
            public decimal? AktifToplam { get; set; }
            public decimal? PasifToplam { get; set; }

            /// <summary>Aktif − Pasif. |Fark| &gt; 0,01 ise ayrı taban uyarısı zorunludur.</summary>
            public decimal Fark => (AktifToplam ?? 0m) - (PasifToplam ?? 0m);
            public bool FarkVar => Math.Abs(Fark) > Tolerans;
        }

        /// <summary>
        /// AKTİF ve PASİF tek listede, alt alta. Seviyeler BilancoPanel ile aynı kuralla:
        /// Roma = üst düzey MainGroup; alt grup = SubGroup, üst düzey olmayan MainGroup ve
        /// Other (özkaynak alt başlıkları — Compute bunlara değer üretmediği için altlarındaki
        /// hesapların Compute değerleri toplanır, BilancoPanel'deki gibi).
        /// </summary>
        public static BilancoSonucu Bilanco(List<MizanSatir> aktif, List<MizanSatir> pasif)
        {
            var sonuc = new BilancoSonucu();

            sonuc.AktifToplam = BolumEkle(sonuc.Satirlar, "AKTİF", "AKTİF TOPLAM", aktif, MaliTabloBolumu.Aktif);
            sonuc.PasifToplam = BolumEkle(sonuc.Satirlar, "PASİF", "PASİF TOPLAM", pasif, MaliTabloBolumu.Pasif);

            return sonuc;
        }

        private static decimal? BolumEkle(
            List<Satir> hedef, string bolumAdi, string toplamAdi, List<MizanSatir> rows, MaliTabloBolumu bolum)
        {
            var computed = MizanHesaplayici.Compute(rows, bolum);

            var toplamCari = computed.Where(r => r.Source.Tip == SatirTipi.Total).Select(r => r.Cari).LastOrDefault(v => v.HasValue);
            var toplamOnceki = computed.Where(r => r.Source.Tip == SatirTipi.Total).Select(r => r.Onceki).LastOrDefault(v => v.HasValue);

            hedef.Add(new Satir(SatirTuru.Bolum, string.Empty, bolumAdi, null, null, null));

            for (int i = 0; i < computed.Count; i++)
            {
                var r = computed[i];
                var tip = r.Source.Tip;
                var ad = r.Source.Ad?.Trim() ?? string.Empty;

                if (tip == SatirTipi.Total) continue;   // bölüm sonunda tek toplam satırı olarak

                if (tip == SatirTipi.MainGroup && MizanHesaplayici.UstDuzeyMainGroupMu(ad))
                {
                    hedef.Add(new Satir(SatirTuru.Roma, string.Empty, ad, r.Onceki, r.Cari, Oran(r.Cari, toplamCari)));
                }
                else if (tip is SatirTipi.SubGroup or SatirTipi.MainGroup)
                {
                    hedef.Add(new Satir(SatirTuru.AltGrup, string.Empty, ad, r.Onceki, r.Cari, Oran(r.Cari, toplamCari)));
                }
                else if (tip == SatirTipi.Other)
                {
                    var (onceki, cari) = AltHesapToplami(computed, i);
                    hedef.Add(new Satir(SatirTuru.AltGrup, string.Empty, ad, onceki, cari, Oran(cari, toplamCari)));
                }
                else if (tip == SatirTipi.Account)
                {
                    hedef.Add(new Satir(SatirTuru.Hesap, r.Source.Kod ?? string.Empty, ad, r.Onceki, r.Cari, Oran(r.Cari, toplamCari)));
                }
            }

            hedef.Add(new Satir(SatirTuru.Toplam, string.Empty, toplamAdi, toplamOnceki, toplamCari, Oran(toplamCari, toplamCari)));
            return toplamCari;
        }

        /// <summary>Other başlığının altındaki (bir sonraki başlığa kadar) hesapların Compute değerleri.</summary>
        private static (decimal? Onceki, decimal? Cari) AltHesapToplami(List<MizanHesaplayici.ComputedRow> c, int baslik)
        {
            decimal? onceki = null, cari = null;
            for (int j = baslik + 1; j < c.Count && c[j].Source.Tip == SatirTipi.Account; j++)
            {
                if (c[j].Onceki is decimal o) onceki = (onceki ?? 0m) + o;
                if (c[j].Cari is decimal k) cari = (cari ?? 0m) + k;
            }
            return (onceki, cari);
        }

        // ── Görünüm 2: Gelir tablosu ──

        public sealed class GelirTablosuSonucu
        {
            public List<Satir> Satirlar { get; } = new();
            public decimal? NetSatislar { get; set; }
            public decimal? TicariKar { get; set; }
            public decimal? DonemNetKari { get; set; }
        }

        /// <summary>Gelir tablosu satırları; yüzde paydası "C-NET SATIŞLAR".</summary>
        public static GelirTablosuSonucu GelirTablosu(
            List<MizanSatir> rows,
            IReadOnlyDictionary<string, decimal?> rawCari,
            IReadOnlyDictionary<string, decimal?> rawOnceki,
            decimal? hesaplananVergi691Cari)
        {
            var computed = GelirTablosuCalculator.Compute(rows, rawCari, rawOnceki, hesaplananVergi691Cari);
            var sonuc = new GelirTablosuSonucu
            {
                NetSatislar = computed.FirstOrDefault(NetSatisSatiriMi)?.Cari,
                TicariKar = computed.FirstOrDefault(r => r.Source.Kod == "690")?.Cari,
                DonemNetKari = computed.FirstOrDefault(r => r.Source.Kod == "692")?.Cari
            };

            foreach (var r in computed)
            {
                var kod = r.Source.Kod ?? string.Empty;
                var tur = kod switch
                {
                    "690" => SatirTuru.TicariKar,
                    "692" => SatirTuru.DonemNetKari,
                    "691" => SatirTuru.Hesap,
                    _ => r.Source.Tip switch
                    {
                        SatirTipi.Total => SatirTuru.AraToplam,
                        SatirTipi.SubGroup or SatirTipi.MainGroup => SatirTuru.AltGrup,
                        _ => SatirTuru.Hesap
                    }
                };

                sonuc.Satirlar.Add(new Satir(tur, kod, r.Source.Ad?.Trim() ?? string.Empty,
                    r.OncekiCari, r.Cari, Oran(r.Cari, sonuc.NetSatislar)));
            }

            return sonuc;
        }

        private static bool NetSatisSatiriMi(GelirTablosuCalculator.ComputedRow r) =>
            r.Source.Tip == SatirTipi.Total && EtiketMotoru.Sadelestir(r.Source.Ad).Contains("net satis");

        // ── Görünüm 3: Özet Grafik ──

        public enum KalemTuru { Normal, Ozkaynak, Borc, Eksi }

        public sealed record AltKirilim(string Kod, string Ad, decimal Tutar, int Seviye);

        public sealed record Kalem(string Kod, string Ad, decimal Tutar, KalemTuru Tur)
        {
            public List<AltKirilim> AltKirilimlar { get; init; } = new();
        }

        public sealed class OzetBolum
        {
            public string Baslik { get; init; } = string.Empty;
            public List<Kalem> Kalemler { get; init; } = new();

            /// <summary>Bölüm toplamına GİRMEYEN, ayrıca gösterilen eksi satırlar (satış iadeleri).</summary>
            public List<Kalem> EksiSatirlar { get; init; } = new();

            public decimal Toplam { get; init; }

            /// <summary>Çubuk ölçeği: bölümün en büyük |kalemi|.</summary>
            public decimal EnBuyuk => Kalemler.Count == 0 ? 0m : Kalemler.Max(k => Math.Abs(k.Tutar));
        }

        public sealed class OzetGrafikSonucu
        {
            public OzetBolum Gelirler { get; init; } = new();
            public OzetBolum Giderler { get; init; } = new();
            public OzetBolum Varliklar { get; init; } = new();
            public OzetBolum Kaynaklar { get; init; } = new();

            public decimal SatisIadeleri => Gelirler.EksiSatirlar.Sum(k => k.Tutar);

            /// <summary>Gelirler − satış iadeleri − giderler. 690 ile AYNI olmak zorunda.</summary>
            public decimal TicariKar => Gelirler.Toplam - SatisIadeleri - Giderler.Toplam;

            /// <summary>Hesap ağacı yok: alt kırılım çizilmez, bölüm başlığında not.</summary>
            public bool AgacYok { get; init; }
        }

        // 690/691/692 sonuç satırıdır; 7'li yansıtma hesapları 6'lı karşılığı boşsa onun yerine
        // sayılır (GelirTablosuCalculator'daki fallback). İkisi birden asla sayılmaz.
        private static readonly HashSet<string> SonucKodlari = new(StringComparer.OrdinalIgnoreCase) { "690", "691", "692" };

        // Yalnızca bu görünümde netlenen kontra hesaplar: kontra → (asıl, net adı).
        private static readonly Dictionary<string, (string Asil, string NetAd)> Netlemeler = new(StringComparer.OrdinalIgnoreCase)
        {
            ["257"] = ("255", "Demirbaşlar (net)"),
            ["268"] = ("264", "Özel Maliyetler (net)"),
        };

        /// <summary>
        /// Dört bölüm. Gelir tablosu hesapları GelirTablosuCalculator'ın işaretli Account
        /// değerlerinden, bilanço hesapları MizanHesaplayici'den okunur. Sınıflama hesap
        /// koduna göre: 60x/64x/67x gelir, 61x satış indirimi (eksi satır), diğer 6'lılar
        /// (ve 6'lı boşken yerine geçen 7'li yansıtma) gider. Böylece her hesap TAM BİR
        /// bölüme düşer ve Gelirler − iadeler − Giderler = 690 kapanır.
        /// </summary>
        public static OzetGrafikSonucu OzetGrafik(
            List<MizanSatir> gelirTablosu,
            List<MizanSatir> aktif,
            List<MizanSatir> pasif,
            IReadOnlyDictionary<string, decimal?> rawCari,
            IReadOnlyDictionary<string, decimal?> rawOnceki,
            IReadOnlyDictionary<string, string> rawAdlar,
            IReadOnlyList<HesapDugumu> agac,
            bool altKirilim,
            bool sifirlar)
        {
            var cocuklar = agac
                .Where(d => d.UstKod is not null)
                .GroupBy(d => d.UstKod!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var gelirler = new List<Kalem>();
            var iadeler = new List<Kalem>();
            var giderler = new List<Kalem>();

            var gt = GelirTablosuCalculator.Compute(gelirTablosu, rawCari, rawOnceki);
            var yansitmaKodlari = GelirTablosuCalculator.YansitmaMap.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Sınıf, hesabın plandaki ALT GRUBUNDAN gelir (kod önekinden değil — planda örn.
            // 652 "Reeskont Faiz Gelirleri" F grubunda, yani gelirdir):
            //   B-SATIŞ İNDİRİMLERİ → iade · adında "(-)" olan grup → gider · diğerleri → gelir.
            var grup = string.Empty;

            foreach (var r in gt)
            {
                if (r.Source.Tip is SatirTipi.SubGroup or SatirTipi.MainGroup)
                {
                    grup = r.Source.Ad?.Trim() ?? string.Empty;
                    continue;
                }

                if (r.Source.Tip != SatirTipi.Account) continue;

                var kod = r.Source.Kod ?? string.Empty;
                if (kod.Length == 0 || SonucKodlari.Contains(kod) || yansitmaKodlari.Contains(kod)) continue;

                var tutar = r.Cari ?? 0m;
                if (tutar == 0m && !sifirlar) continue;

                // 6'lı boşken tutar 7'li yansıtmadan geldiyse kalem o hesapla (770 vb.) gösterilir.
                var gosterKod = kod;
                if (!r.Source.CariDonem.HasValue
                    && GelirTablosuCalculator.YansitmaMap.TryGetValue(kod, out var yKod)
                    && rawCari.TryGetValue(yKod, out var yDeger) && yDeger.HasValue)
                {
                    gosterKod = yKod;
                }

                var ad = rawAdlar.TryGetValue(gosterKod, out var mAd) && !string.IsNullOrWhiteSpace(mAd)
                    ? mAd
                    : AdTemizle(r.Source.Ad);

                var grupSade = EtiketMotoru.Sadelestir(grup);
                if (grupSade.Contains("satis indirimleri"))
                {
                    // Sunumda eksi (−666.790,50); satırda tutar olarak gösterilir, gelirden düşülür.
                    iadeler.Add(new Kalem(gosterKod, ad, -tutar, KalemTuru.Eksi));
                }
                else if (!grup.Contains("(-)"))
                {
                    gelirler.Add(new Kalem(gosterKod, ad, tutar, KalemTuru.Normal)
                        { AltKirilimlar = altKirilim ? Kirilim(gosterKod, cocuklar, isaret: 1m) : new() });
                }
                else
                {
                    // Giderler sunumda eksi; bölümde tutar olarak gösterilir.
                    giderler.Add(new Kalem(gosterKod, ad, -tutar, KalemTuru.Normal)
                        { AltKirilimlar = altKirilim ? Kirilim(gosterKod, cocuklar, isaret: -1m) : new() });
                }
            }

            var varliklar = BilancoKalemleri(aktif, MaliTabloBolumu.Aktif, sifirlar, netle: true);
            var kaynaklar = BilancoKalemleri(pasif, MaliTabloBolumu.Pasif, sifirlar, netle: false);

            return new OzetGrafikSonucu
            {
                Gelirler = new OzetBolum
                {
                    Baslik = "GELİRLER",
                    Kalemler = gelirler.OrderByDescending(k => k.Tutar).ToList(),
                    EksiSatirlar = iadeler,
                    Toplam = gelirler.Sum(k => k.Tutar)
                },
                Giderler = new OzetBolum
                {
                    Baslik = "GİDERLER",
                    Kalemler = giderler.OrderByDescending(k => k.Tutar).ToList(),
                    Toplam = giderler.Sum(k => k.Tutar)
                },
                Varliklar = new OzetBolum
                {
                    Baslik = "VARLIKLAR",
                    Kalemler = varliklar.OrderByDescending(k => k.Tutar).ToList(),
                    Toplam = varliklar.Sum(k => k.Tutar)
                },
                Kaynaklar = new OzetBolum
                {
                    Baslik = "KAYNAKLAR",
                    Kalemler = kaynaklar.OrderByDescending(k => k.Tutar).ToList(),
                    Toplam = kaynaklar.Sum(k => k.Tutar)
                },
                AgacYok = agac.Count == 0
            };
        }

        /// <summary>
        /// Hesap ağacından en fazla iki GÖRÜNÜR seviye alt kırılım. Tek çocuklu ve aynı tutarlı
        /// ara düğümler ("600 1" gibi) atlanır — aynı paranın tekrarı olmasın. BİLGİDİR:
        /// hiçbir toplama girmez.
        /// </summary>
        private static List<AltKirilim> Kirilim(string anaKod, Dictionary<string, List<HesapDugumu>> cocuklar, decimal isaret)
        {
            var sonuc = new List<AltKirilim>();
            Ekle(anaKod, 1);
            return sonuc;

            void Ekle(string ustKod, int seviye)
            {
                if (seviye > 2) return;

                var altlar = Gecis(ustKod);
                foreach (var d in altlar.OrderByDescending(d => isaret * HesapDugumSunumu.Sunum(d)))
                {
                    var tutar = isaret * HesapDugumSunumu.Sunum(d);
                    if (tutar == 0m) continue;

                    sonuc.Add(new AltKirilim(d.Kod, d.Ad, tutar, seviye));
                    Ekle(d.Kod, seviye + 1);
                }
            }

            // Tek çocuğu olan ve tutarı ebeveyniyle aynı zincirleri atla.
            List<HesapDugumu> Gecis(string kod)
            {
                var altlar = cocuklar.TryGetValue(kod, out var l) ? l : new List<HesapDugumu>();
                while (altlar.Count == 1 && cocuklar.TryGetValue(altlar[0].Kod, out var torun) && torun.Count > 0
                       && Math.Abs(torun.Sum(HesapDugumSunumu.Sunum) - HesapDugumSunumu.Sunum(altlar[0])) <= Tolerans)
                {
                    altlar = torun;
                }
                return altlar;
            }
        }

        private static List<Kalem> BilancoKalemleri(List<MizanSatir> rows, MaliTabloBolumu bolum, bool sifirlar, bool netle)
        {
            var hesaplar = MizanHesaplayici.Compute(rows, bolum)
                .Where(r => r.Source.Tip == SatirTipi.Account && !string.IsNullOrWhiteSpace(r.Source.Kod))
                .Select(r => (Kod: r.Source.Kod!, Ad: AdTemizle(r.Source.Ad), Tutar: r.Cari ?? 0m))
                .ToList();

            if (netle)
            {
                foreach (var (kontra, (asil, netAd)) in Netlemeler)
                {
                    var k = hesaplar.FindIndex(h => h.Kod == kontra);
                    var a = hesaplar.FindIndex(h => h.Kod == asil);
                    if (k < 0 || a < 0 || (hesaplar[k].Tutar == 0m && hesaplar[a].Tutar == 0m)) continue;

                    // Kontra tutar asıl satıra taşınır, kontra satırı düşer — toplam aynı kalır.
                    hesaplar[a] = (asil, netAd, hesaplar[a].Tutar + hesaplar[k].Tutar);
                    hesaplar.RemoveAt(k);
                }
            }

            return hesaplar
                .Where(h => sifirlar || h.Tutar != 0m)
                .Select(h => new Kalem(h.Kod, h.Ad, h.Tutar,
                    bolum == MaliTabloBolumu.Pasif
                        ? (h.Kod.StartsWith('5') ? KalemTuru.Ozkaynak : KalemTuru.Borc)
                        : KalemTuru.Normal))
                .ToList();
        }

        /// <summary>"1.Yurtiçi Satışlar" → "Yurtiçi Satışlar", sondaki "(-)" atılır.</summary>
        public static string AdTemizle(string? ad)
        {
            var s = (ad ?? string.Empty).Trim();
            var nokta = s.IndexOf('.');
            if (nokta is > 0 and <= 3 && s[..nokta].All(char.IsDigit)) s = s[(nokta + 1)..].Trim();
            if (s.EndsWith("(-)")) s = s[..^3].Trim();
            return s;
        }
    }
}
