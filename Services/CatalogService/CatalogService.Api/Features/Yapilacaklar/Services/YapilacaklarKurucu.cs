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

            // Yalnız yapılmış kayıt (zamanı dolu); not/kanıt taşıyan yapılmamış kayıt kuyruğu değiştirmez.
            var tamamlar = tamamlamalar.Where(t => t.Yapildi)
                                       .ToDictionary(t => (t.KaynakTip, t.KaynakId, t.FirmaId, t.DonemAnahtari));

            // Seriler: aynı kodun aynı tekrardaki takvim satırları, son güne göre sıralı.
            var seriler = takvim
                .Where(t => t.Aktif)
                .GroupBy(t => (t.MukellefiyetKodu, t.Tekrar))
                .ToDictionary(g => g.Key, g => g.OrderBy(t => t.SonGun).ThenBy(t => t.Id)
                    .Select(t => new Donem(t.Id, new IsDonemi(t.Tekrar, t.Yil, t.DonemNo).Anahtar, t.SonGun.Date,
                                           t.Ad, new IsDonemi(t.Tekrar, t.Yil, t.DonemNo).Etiket,
                                           t.MukellefiyetKodu, t.Tekrar))
                    .ToList());

            // Yasal işin prosedür satırı özel iş değildir; dönemi takvimden gelir.
            var ozelByFirma = ozelIsler.Where(i => i.Aktif && i.YasalMukellefiyetKodu is null).ToLookup(i => i.FirmaId);

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

            var donem = OzelIlkDonem(isi);
            for (var i = 0; i < EnFazlaDonem; i++, donem = donem.Sonraki())
            {
                yield return new Donem(isi.Id, donem.Anahtar, IsTakvimHesabi.SonGun(isi.GunKurali, isi.AyinGunu, donem),
                                       isi.Baslik, donem.Etiket, null, isi.Tekrar);
            }
        }

        /// <summary>
        /// Tekrarlayan özel işin ilk dönemi: <see cref="FirmaIsi.IlkDonem"/> doluysa o ayın düştüğü dönem
        /// (Prompt 17 — Ekim'de girilen Eylül işi Eylül'den başlar), boşsa eklendiği günün dönemi.
        /// Kuyruk (<see cref="OzelDonemler"/>), Dönem panosu, Özet ve mail takvimi aynı kuralı buradan
        /// okur — ikinci bir dönem hesabı yok.
        /// </summary>
        public static IsDonemi OzelIlkDonem(FirmaIsi isi)
            => IsDonemi.Icin(isi.Tekrar, IsDonemi.Coz(IsTekrari.Aylik, isi.IlkDonem) is { } ilk ? ilk.Bas : OlusturmaGunu(isi));

        /// <summary>
        /// Başlangıç ayının kayda yazılacak hali (Prompt 17): geçersiz biçim reddedilir (<c>Gecersiz</c>);
        /// tek seferlik işte ve boş girişte <c>null</c>; eklenme ayına eşitse de <c>null</c> — o zaman
        /// davranış zaten eski kuralla aynıdır, kayıt "bugünkü davranıştaki iş" olarak kalır.
        /// </summary>
        public static (string? Deger, bool Gecersiz) IlkDonemNormalize(string? giris, IsTekrari tekrar, DateTime olusturmaZamani)
        {
            if (tekrar == IsTekrari.TekSefer || string.IsNullOrWhiteSpace(giris)) return (null, false);
            if (IsDonemi.Coz(IsTekrari.Aylik, giris) is not { } ay) return (null, true);

            var eklenme = OlusturmaGunu(new FirmaIsi { OlusturmaZamani = olusturmaZamani });
            return ay.Yil == eklenme.Year && ay.No == eklenme.Month ? (null, false) : (ay.Anahtar, false);
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
                       .Select(t => (DateTime?)DateTime.SpecifyKind(t.TamamlanmaZamani!.Value, DateTimeKind.Utc))
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
                                             DateTime bugun, SistemBaglami? sistem = null)
        {
            bugun = bugun.Date;
            sistem ??= SistemBaglami.Bos;
            var firmaninki = satirlar.Where(s => s.FirmaId == firmaId).ToList();
            var takvimde = takvimKodlari.ToHashSet();
            var firmaTanimlari = tanimlar.Where(t => t.FirmaId == firmaId).ToList();
            var adlar = firmaTanimlari.ToDictionary(t => t.Id, t => t.Baslik);

            return new FirmaIsleriKartDto
            {
                FirmaId = firmaId,
                Yasal = firmaninki.Where(s => s.KaynakTip == IsKaynagi.Yasal).ToList(),
                Ozel = firmaninki.Where(s => s.KaynakTip == IsKaynagi.Ozel).ToList(),
                OzelTanimlar = firmaTanimlari.Where(t => t.YasalMukellefiyetKodu is null)
                    .OrderByDescending(t => t.Aktif)
                    .ThenBy(t => t.Baslik, StringComparer.Create(IsTakvimHesabi.Tr, true))
                    .Select(t => Tanim(t, adlar, sistem.Adlar))
                    .ToList(),
                YasalProsedurler = firmaTanimlari.Where(t => t.YasalMukellefiyetKodu is not null)
                    .Select(t => Tanim(t, adlar, sistem.Adlar))
                    .ToList(),
                VarsayilanSistemId = sistem.Varsayilan(),
                Sistemler = sistem.Secenekler(firmaTanimlari.Select(t => t.SistemId)),
                GeciktiSayisi = firmaninki.Count(s => s.Durum == IsDurumu.Gecikti),
                BuAySayisi = firmaninki.Count(s => !s.Tamamlandi && s.Durum != IsDurumu.Gecikti
                                                   && s.SonGun.Year == bugun.Year && s.SonGun.Month == bugun.Month),
                MukellefiyetKodlari = kodlar.ToList(),
                TakvimdeOlmayanKodlar = kodlar.Where(k => !takvimde.Contains(k)).ToList()
            };
        }

        /// <param name="adlar">Firmanın iş Id → başlık sözlüğü; ön adım hedefinin adı buradan.</param>
        /// <param name="sistemAdlari">Sistem Id → ad; program çipinin metni.</param>
        public static FirmaIsiDto Tanim(FirmaIsi t, IReadOnlyDictionary<int, string>? adlar = null,
                                        IReadOnlyDictionary<int, string>? sistemAdlari = null) => new()
        {
            YasalMukellefiyetKodu = t.YasalMukellefiyetKodu,
            SistemId = t.SistemId,
            SistemAdi = t.SistemId is { } s && sistemAdlari is not null && sistemAdlari.TryGetValue(s, out var sAd) ? sAd : null,
            MenuYolu = t.MenuYolu,
            NasilYapilir = t.NasilYapilir,
            OnAdimiOlduguIsId = t.OnAdimiOlduguIsId,
            OnAdimiBaslik = t.OnAdimiOlduguIsId is { } hedef && adlar is not null && adlar.TryGetValue(hedef, out var ad) ? ad : null,
            Alicilar = t.Alicilar.OrderBy(a => a.Sira).ThenBy(a => a.Id).Select(Alici).ToList(),
            EkSayisi = t.Ekler.Count,
            Id = t.Id,
            FirmaId = t.FirmaId,
            Baslik = t.Baslik,
            Aciklama = t.Aciklama,
            Tekrar = t.Tekrar,
            GunKurali = t.GunKurali,
            AyinGunu = t.AyinGunu,
            TekSeferTarih = t.TekSeferTarih,
            IlkDonem = t.IlkDonem,
            BaslangicDonemi = t.Tekrar == IsTekrari.TekSefer || t.YasalMukellefiyetKodu is not null
                ? null : IsDonemi.Icin(IsTekrari.Aylik, OzelIlkDonem(t).Bas).Anahtar,
            SorumluKullaniciId = t.SorumluKullaniciId,
            SorumluKullaniciAdi = t.SorumluKullaniciAdi,
            Aktif = t.Aktif,
            KuralMetni = IsTakvimHesabi.KuralMetni(t.Tekrar, t.GunKurali, t.AyinGunu, t.TekSeferTarih)
        };

        public static FirmaIsiAlicisiDto Alici(FirmaIsiAlicisi a) => new()
        {
            Id = a.Id,
            AdSoyad = a.AdSoyad,
            Eposta = a.Eposta,
            Rol = a.Rol,
            AliciTipi = a.AliciTipi,
            Sira = a.Sira
        };

        /// <summary>Dönem eki prosedür ekiyle aynı görünümde (ayrı tablo, aynı DTO).</summary>
        public static FirmaIsiEkiDto Ek(IsTamamlamaEki e) => new()
        {
            Id = e.Id,
            FileId = e.FileId,
            DosyaAdi = e.DosyaAdi,
            ContentType = e.ContentType,
            Boyut = e.Boyut,
            YuklemeZamani = DateTime.SpecifyKind(e.YuklemeZamani, DateTimeKind.Utc),
            YukleyenKullaniciAdi = e.YukleyenKullaniciAdi
        };

        public static FirmaIsiEkiDto Ek(FirmaIsiEki e) => new()
        {
            Id = e.Id,
            FileId = e.FileId,
            DosyaAdi = e.DosyaAdi,
            ContentType = e.ContentType,
            Boyut = e.Boyut,
            YuklemeZamani = DateTime.SpecifyKind(e.YuklemeZamani, DateTimeKind.Utc),
            YukleyenKullaniciAdi = e.YukleyenKullaniciAdi
        };

        // ---- Prosedür paneli ----

        /// <summary>"Nasıl yapılır" metninin adımları: her dolu satır bir adım, boş satır atlanır.</summary>
        public static List<string> Adimlar(string? metin)
            => string.IsNullOrWhiteSpace(metin)
                ? new List<string>()
                : metin.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

        /// <summary>Özel işin geçmişi; <paramref name="adet"/> boşsa hepsi.</summary>
        public static (List<IsGecmisSatiriDto> Satirlar, int Toplam) OzelGecmis(FirmaIsi isi, IEnumerable<IsTamamlama> tamamlamalar,
                                                                              DateTime bugun, int? adet)
            => Gecmis(OzelDonemler(isi), IsKaynagi.Ozel, isi.FirmaId, tamamlamalar, bugun, adet);

        /// <summary>
        /// Yasal işin geçmişi: serinin (kod + tekrar) takvim satırları. Pasif satır yalnız
        /// işaretlenmişse görünür — seed'in pasif yazdığı eski dönemler "yapılmadı" diye dolmasın.
        /// </summary>
        public static (List<IsGecmisSatiriDto> Satirlar, int Toplam) YasalGecmis(IEnumerable<VergiTakvimi> seri, int firmaId,
                                                                               IEnumerable<IsTamamlama> tamamlamalar,
                                                                               DateTime bugun, int? adet)
        {
            var tamamlar = tamamlamalar.Where(t => t.Yapildi && t.KaynakTip == IsKaynagi.Yasal && t.FirmaId == firmaId)
                                       .Select(t => t.KaynakId).ToHashSet();

            var donemler = seri
                .Where(t => t.Aktif || tamamlar.Contains(t.Id))
                .OrderBy(t => t.SonGun).ThenBy(t => t.Id)
                .Select(t =>
                {
                    var d = new IsDonemi(t.Tekrar, t.Yil, t.DonemNo);
                    return new Donem(t.Id, d.Anahtar, t.SonGun.Date, t.Ad, d.Etiket, t.MukellefiyetKodu, t.Tekrar);
                });

            return Gecmis(donemler, IsKaynagi.Yasal, firmaId, tamamlamalar, bugun, adet);
        }

        /// <summary>
        /// Son günü geçmiş dönemler ve erken yapılmış olanlar, yeniden eskiye. Sıradaki açık
        /// dönemde durur (özel işin dönemleri sonsuz).
        /// </summary>
        private static (List<IsGecmisSatiriDto>, int) Gecmis(IEnumerable<Donem> sirali, IsKaynagi tip, int firmaId,
                                                             IEnumerable<IsTamamlama> tamamlamalar, DateTime bugun, int? adet)
        {
            bugun = bugun.Date;
            var tamamlar = tamamlamalar.Where(t => t.Yapildi && t.KaynakTip == tip && t.FirmaId == firmaId)
                                       .GroupBy(t => (t.KaynakId, t.DonemAnahtari))
                                       .ToDictionary(g => g.Key, g => g.First());

            var satirlar = new List<IsGecmisSatiriDto>();
            foreach (var d in sirali)
            {
                tamamlar.TryGetValue((d.KaynakId, d.Anahtar), out var t);
                if (t is null && d.SonGun >= bugun) break;

                satirlar.Add(new IsGecmisSatiriDto
                {
                    DonemAnahtari = d.Anahtar,
                    DonemEtiketi = string.IsNullOrEmpty(d.Etiket) ? null : d.Etiket,
                    SonGun = d.SonGun,
                    Yapildi = t is not null,
                    TamamlanmaZamani = t is null ? null : DateTime.SpecifyKind(t.TamamlanmaZamani!.Value, DateTimeKind.Utc),
                    TamamlayanAdi = t?.KullaniciAdi,
                    Yapilmadi = t is null
                });
            }

            satirlar.Reverse();
            var toplam = satirlar.Count;
            return (adet is { } n ? satirlar.Take(n).ToList() : satirlar, toplam);
        }
    }

    /// <summary>
    /// Kartın sistem bilgisi: ortak liste ve firmanın atamaları. Saf — servis doldurur.
    /// </summary>
    /// <param name="Liste">Bütün sistemler (pasifler dahil; ad çözümü için).</param>
    /// <param name="FirmaSistemIdleri">Firmanın "Kullanılan sistemler" atamaları, sırayla.</param>
    /// <param name="MizanFormati">Firmanın mizan formatı; muhasebe programı atanmamışsa öneri buradan.</param>
    public sealed record SistemBaglami(IReadOnlyList<Sistemler.Domain.Sistem> Liste, IReadOnlyList<int> FirmaSistemIdleri,
                                       string? MizanFormati)
    {
        public static readonly SistemBaglami Bos = new(Array.Empty<Sistemler.Domain.Sistem>(), Array.Empty<int>(), null);

        public IReadOnlyDictionary<int, string> Adlar { get; } = Liste.ToDictionary(s => s.Id, s => s.Ad);

        /// <summary>
        /// Yeni işin önerilen sistemi: firmanın ilk muhasebe programı; yoksa mizan formatının
        /// programıyla aynı adlı aktif muhasebe kaydı ("ORKA — döviz kolonlu" → ORKA).
        /// </summary>
        public int? Varsayilan()
        {
            var muhasebe = FirmaSistemIdleri
                .Select(id => Liste.FirstOrDefault(s => s.Id == id))
                .FirstOrDefault(s => s is { Tur: Sistemler.Domain.SistemTuru.Muhasebe });
            if (muhasebe is not null) return muhasebe.Id;

            var program = Firmalar.Domain.MizanFormatlari.ProgramAdi(MizanFormati);
            if (program is null) return null;

            return Liste.FirstOrDefault(s => s.Aktif && s.Tur == Sistemler.Domain.SistemTuru.Muhasebe
                                             && Sistemler.Domain.SistemAdi.Normalize(s.Ad) == Sistemler.Domain.SistemAdi.Normalize(program))?.Id;
        }

        /// <summary>Aktif sistemler + işlerde hâlâ kullanılan pasifler; firmanınkiler önde, sonra türe ve ada göre.</summary>
        public List<IsSistemSecenegiDto> Secenekler(IEnumerable<int?> kullanilan)
        {
            var kullanilanlar = kullanilan.OfType<int>().ToHashSet();
            var firmanin = FirmaSistemIdleri.ToHashSet();

            return Liste
                .Where(s => s.Aktif || kullanilanlar.Contains(s.Id))
                .OrderByDescending(s => firmanin.Contains(s.Id))
                .ThenBy(s => s.Tur)
                .ThenBy(s => s.Ad, StringComparer.Create(IsTakvimHesabi.Tr, true))
                .Select(s => new IsSistemSecenegiDto
                {
                    Id = s.Id, Ad = s.Ad, Tur = s.Tur, Aktif = s.Aktif, Firmanin = firmanin.Contains(s.Id)
                })
                .ToList();
        }
    }
}
