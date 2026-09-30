using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;

namespace CatalogService.Api.Features.Yapilacaklar.Services
{
    /// <summary>
    /// Firma işlerinin görünen satırlarını kurar. Saf fonksiyon: veritabanı yok, saat yok —
    /// "bugün" parametre. Kart da Yapılacaklar ekranı da satırlarını buradan alır; iki ekran
    /// aynı işi farklı gösterirse sebebi veri olur, iki ayrı hesap değil.
    ///
    /// <b>Yasal işler türetilir, saklanmaz:</b> firmanın mükellefiyet kodları × takvimin
    /// aktif satırları. Firma bir mükellefiyetten çıkınca liste kendiliğinden düzelir.
    ///
    /// <b>Bir işin hangi dönemleri görünür?</b> İş bir dönem kuyruğu gibi ilerler:
    /// <list type="bullet">
    /// <item>son günü geçmiş ve yapılmamış bütün dönemler (her biri ayrı gecikmiş iş);</item>
    /// <item>gecikmiş dönem yoksa sıradaki ilk yapılmamış dönem;</item>
    /// <item>bunların hemen önünde yapılmış son dönem — üstü çizili, "bu dönem yapıldı".
    /// İşaretlenen iş listeden kaybolmaz, bir sonraki dönem altında belirir.</item>
    /// </list>
    /// </summary>
    public static class YapilacaklarKurucu
    {
        /// <summary>Kurucunun firma hakkında bildiği her şey.</summary>
        public sealed record FirmaGirdisi(int FirmaId, string Ad, int? SorumluKullaniciId, string? SorumluKullaniciAdi,
                                          IReadOnlyCollection<string> Kodlar);

        /// <summary>Sonsuz tekrarlayan özel işte güvenlik sınırı (yüz yıllık aylık iş).</summary>
        private const int EnFazlaDonem = 1200;

        private sealed record Donem(int KaynakId, string Anahtar, DateTime SonGun, string Baslik, string? Etiket,
                                    string? Kod, IsTekrari Tekrar);

        public static List<IsSatiriDto> Satirlar(DateTime bugun,
                                                 IEnumerable<FirmaGirdisi> firmalar,
                                                 IEnumerable<VergiTakvimi> takvim,
                                                 IEnumerable<FirmaIsi> ozelIsler,
                                                 IEnumerable<IsTamamlama> tamamlamalar)
        {
            bugun = bugun.Date;

            var tamamlar = tamamlamalar.ToDictionary(t => (t.KaynakTip, t.KaynakId, t.FirmaId, t.DonemAnahtari));

            // Seriler: aynı kodun aynı tekrardaki takvim satırları, son güne göre sıralı.
            var seriler = takvim
                .Where(t => t.Aktif)
                .GroupBy(t => (t.MukellefiyetKodu, t.Tekrar))
                .ToDictionary(g => g.Key, g => g.OrderBy(t => t.SonGun).ThenBy(t => t.Id)
                    .Select(t => new Donem(t.Id, new IsDonemi(t.Tekrar, t.Yil, t.DonemNo).Anahtar, t.SonGun.Date,
                                           t.Ad, new IsDonemi(t.Tekrar, t.Yil, t.DonemNo).Etiket,
                                           t.MukellefiyetKodu, t.Tekrar))
                    .ToList());

            var ozelByFirma = ozelIsler.Where(i => i.Aktif).ToLookup(i => i.FirmaId);

            var sonuc = new List<IsSatiriDto>();

            foreach (var firma in firmalar)
            {
                // ---- Yasal ----
                foreach (var ((kod, _), donemler) in seriler.OrderBy(s => s.Key.MukellefiyetKodu).ThenBy(s => s.Key.Tekrar))
                {
                    if (!firma.Kodlar.Contains(kod)) continue;

                    var kaynaklar = donemler.Select(d => d.KaynakId).ToHashSet();
                    var sonYapilma = SonYapilma(tamamlar.Values, IsKaynagi.Yasal, firma.FirmaId, kaynaklar);

                    foreach (var (donem, tamam) in Gorunenler(donemler, d => Tamam(tamamlar, IsKaynagi.Yasal, d, firma.FirmaId), bugun))
                        sonuc.Add(Satir(IsKaynagi.Yasal, donem, firma, tamam, bugun, null, null, null, sonYapilma));
                }

                // ---- Firmaya özel ----
                foreach (var isi in ozelByFirma[firma.FirmaId].OrderBy(i => i.Baslik, StringComparer.CurrentCultureIgnoreCase))
                {
                    var sonYapilma = SonYapilma(tamamlar.Values, IsKaynagi.Ozel, firma.FirmaId, new HashSet<int> { isi.Id });

                    foreach (var (donem, tamam) in Gorunenler(OzelDonemler(isi), d => Tamam(tamamlar, IsKaynagi.Ozel, d, firma.FirmaId), bugun))
                        sonuc.Add(Satir(IsKaynagi.Ozel, donem, firma, tamam, bugun, isi.Aciklama,
                                        isi.SorumluKullaniciId, isi.SorumluKullaniciAdi, sonYapilma));
                }
            }

            return sonuc;
        }

        /// <summary>
        /// Özel işin dönemleri, ilk dönemden başlayarak (sonsuz; tüketen durdurur). İlk dönem
        /// işin eklendiği günün dönemidir: ayın 28'inde eklenen "ayın 1'i" işi o ayın 1'ini
        /// kaçırmıştır, daha eski aylar üretilmez.
        /// </summary>
        private static IEnumerable<Donem> OzelDonemler(FirmaIsi isi)
        {
            if (isi.Tekrar == IsTekrari.TekSefer)
            {
                if (isi.TekSeferTarih is { } t)
                    yield return new Donem(isi.Id, IsDonemi.TekSeferAnahtari, t.Date, isi.Baslik, null, null, isi.Tekrar);
                yield break;
            }

            var donem = IsDonemi.Icin(isi.Tekrar, OlusturmaGunu(isi));
            for (var i = 0; i < EnFazlaDonem; i++, donem = donem.Sonraki())
            {
                yield return new Donem(isi.Id, donem.Anahtar, IsTakvimHesabi.SonGun(isi.GunKurali, isi.AyinGunu, donem),
                                       isi.Baslik, donem.Etiket, null, isi.Tekrar);
            }
        }

        /// <summary>Eklenme anının yerel günü (kayıt UTC).</summary>
        private static DateTime OlusturmaGunu(FirmaIsi isi)
            => isi.OlusturmaZamani.Kind == DateTimeKind.Local
                ? isi.OlusturmaZamani.Date
                : DateTime.SpecifyKind(isi.OlusturmaZamani, DateTimeKind.Utc).ToLocalTime().Date;

        /// <summary>Kuyruk kuralı — sınıf açıklamasına bakın.</summary>
        private static IEnumerable<(Donem Donem, bool Tamam)> Gorunenler(IEnumerable<Donem> sirali, Func<Donem, bool> tamamMi,
                                                                         DateTime bugun)
        {
            Donem? sonTamam = null;
            var acik = new List<Donem>();

            foreach (var d in sirali)
            {
                if (tamamMi(d))
                {
                    // Yalnız ilk açık dönemin ÖNÜNDEKİ yapılmış dönem gösterilir.
                    if (acik.Count == 0) sonTamam = d;
                    continue;
                }

                if (d.SonGun < bugun)
                {
                    acik.Add(d);
                    continue;
                }

                if (acik.Count == 0) acik.Add(d);
                break;
            }

            if (sonTamam is not null) yield return (sonTamam, true);
            foreach (var d in acik) yield return (d, false);
        }

        private static bool Tamam(Dictionary<(IsKaynagi, int, int, string), IsTamamlama> tamamlar, IsKaynagi tip,
                                  Donem d, int firmaId)
            => tamamlar.ContainsKey((tip, d.KaynakId, firmaId, d.Anahtar));

        private static DateTime? SonYapilma(IEnumerable<IsTamamlama> tamamlar, IsKaynagi tip, int firmaId, HashSet<int> kaynaklar)
            => tamamlar.Where(t => t.KaynakTip == tip && t.FirmaId == firmaId && kaynaklar.Contains(t.KaynakId))
                       .Select(t => (DateTime?)DateTime.SpecifyKind(t.TamamlanmaZamani, DateTimeKind.Utc))
                       .Max();

        private static IsSatiriDto Satir(IsKaynagi tip, Donem d, FirmaGirdisi firma, bool tamam, DateTime bugun,
                                         string? aciklama, int? sorumluId, string? sorumluAdi, DateTime? sonYapilma)
        {
            var (durum, gun) = IsTakvimHesabi.Durum(d.SonGun, bugun, tamam);
            return new IsSatiriDto
            {
                KaynakTip = tip,
                KaynakId = d.KaynakId,
                FirmaId = firma.FirmaId,
                FirmaAdi = firma.Ad,
                Baslik = d.Baslik,
                Aciklama = aciklama,
                MukellefiyetKodu = d.Kod,
                Tekrar = d.Tekrar,
                DonemAnahtari = d.Anahtar,
                DonemEtiketi = string.IsNullOrEmpty(d.Etiket) ? null : d.Etiket,
                SonGun = d.SonGun,
                Durum = durum,
                Gun = gun,
                Tamamlandi = tamam,
                SonYapilma = sonYapilma,
                SorumluKullaniciId = sorumluId ?? firma.SorumluKullaniciId,
                SorumluKullaniciAdi = sorumluId is null ? firma.SorumluKullaniciAdi : sorumluAdi
            };
        }

        // ---- Yapılacaklar ekranı ----

        public static IEnumerable<IsSatiriDto> Suz(IEnumerable<IsSatiriDto> satirlar, IsFiltresi filtre, int? benimId)
            => filtre switch
            {
                IsFiltresi.Bende => satirlar.Where(s => benimId is not null && s.SorumluKullaniciId == benimId),
                IsFiltresi.Yasal => satirlar.Where(s => s.KaynakTip == IsKaynagi.Yasal),
                IsFiltresi.Ozel => satirlar.Where(s => s.KaynakTip == IsKaynagi.Ozel),
                _ => satirlar
            };

        /// <summary>
        /// Aynı iş TEK SATIR: aynı kaynak türü, aynı ad (büyük/küçük harf ve boşluk farkı
        /// yok sayılır) ve aynı son gün. 25 firma × 5 yasal iş 125 satır değil, 5 satır olur.
        /// </summary>
        public static List<IsGrubuDto> Grupla(IEnumerable<IsSatiriDto> satirlar, DateTime bugun)
        {
            bugun = bugun.Date;

            return satirlar
                .GroupBy(s => $"{(byte)s.KaynakTip}|{s.Baslik.Trim().ToUpper(IsTakvimHesabi.Tr)}|{s.SonGun:yyyyMMdd}")
                .Select(g =>
                {
                    var ilk = g.First();
                    var acikVar = g.Any(s => !s.Tamamlandi);
                    var (durum, gun) = IsTakvimHesabi.Durum(ilk.SonGun, bugun, !acikVar);
                    var etiketler = g.Select(s => s.DonemEtiketi).Distinct().ToList();

                    return new IsGrubuDto
                    {
                        Anahtar = g.Key,
                        KaynakTip = ilk.KaynakTip,
                        Baslik = ilk.Baslik.Trim(),
                        Tekrar = ilk.Tekrar,
                        DonemEtiketi = etiketler.Count == 1 ? etiketler[0] : null,
                        SonGun = ilk.SonGun,
                        Durum = durum,
                        Gun = gun,
                        Firmalar = g.OrderBy(s => s.Tamamlandi)
                                    .ThenBy(s => s.FirmaAdi, StringComparer.Create(IsTakvimHesabi.Tr, true))
                                    .ToList()
                    };
                })
                .OrderBy(g => g.Durum)
                .ThenBy(g => g.SonGun)
                .ThenBy(g => g.KaynakTip)
                .ThenBy(g => g.Baslik, StringComparer.Create(IsTakvimHesabi.Tr, true))
                .ToList();
        }

        /// <summary>Anasayfa şeridi: Yapılacaklar'ın filtresiz satır sayıları (aynı gruplama).</summary>
        public static YapilacaklarOzetDto Ozet(IEnumerable<IsGrubuDto> gruplar)
        {
            var liste = gruplar.ToList();
            return new YapilacaklarOzetDto
            {
                Gecikti = liste.Count(g => g.Durum == IsDurumu.Gecikti),
                BuHafta = liste.Count(g => g.Durum == IsDurumu.BuHafta)
            };
        }

        // ---- Firma kartı ----

        public static FirmaIsleriKartDto Kart(int firmaId, IEnumerable<IsSatiriDto> satirlar, IEnumerable<FirmaIsi> tanimlar,
                                             IReadOnlyCollection<string> kodlar, IEnumerable<string> takvimKodlari,
                                             DateTime bugun)
        {
            bugun = bugun.Date;
            var firmaninki = satirlar.Where(s => s.FirmaId == firmaId).ToList();
            var takvimde = takvimKodlari.ToHashSet();

            return new FirmaIsleriKartDto
            {
                FirmaId = firmaId,
                Yasal = firmaninki.Where(s => s.KaynakTip == IsKaynagi.Yasal).ToList(),
                Ozel = firmaninki.Where(s => s.KaynakTip == IsKaynagi.Ozel).ToList(),
                OzelTanimlar = tanimlar.Where(t => t.FirmaId == firmaId)
                    .OrderByDescending(t => t.Aktif)
                    .ThenBy(t => t.Baslik, StringComparer.Create(IsTakvimHesabi.Tr, true))
                    .Select(Tanim)
                    .ToList(),
                GeciktiSayisi = firmaninki.Count(s => s.Durum == IsDurumu.Gecikti),
                BuAySayisi = firmaninki.Count(s => !s.Tamamlandi && s.Durum != IsDurumu.Gecikti
                                                   && s.SonGun.Year == bugun.Year && s.SonGun.Month == bugun.Month),
                MukellefiyetKodlari = kodlar.ToList(),
                TakvimdeOlmayanKodlar = kodlar.Where(k => !takvimde.Contains(k)).ToList()
            };
        }

        public static FirmaIsiDto Tanim(FirmaIsi t) => new()
        {
            Id = t.Id,
            FirmaId = t.FirmaId,
            Baslik = t.Baslik,
            Aciklama = t.Aciklama,
            Tekrar = t.Tekrar,
            GunKurali = t.GunKurali,
            AyinGunu = t.AyinGunu,
            TekSeferTarih = t.TekSeferTarih,
            SorumluKullaniciId = t.SorumluKullaniciId,
            SorumluKullaniciAdi = t.SorumluKullaniciAdi,
            Aktif = t.Aktif,
            KuralMetni = IsTakvimHesabi.KuralMetni(t.Tekrar, t.GunKurali, t.AyinGunu, t.TekSeferTarih)
        };
    }
}
