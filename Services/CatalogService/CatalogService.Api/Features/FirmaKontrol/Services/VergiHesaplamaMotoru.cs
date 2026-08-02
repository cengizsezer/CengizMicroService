using CatalogService.Api.Features.FirmaKontrol.Domain;
using CatalogService.Api.Features.FirmaKontrol.Dtos;

namespace CatalogService.Api.Features.FirmaKontrol.Services
{
    /// <summary>
    /// Kurumlar vergisi beyanname hesabı. Saf fonksiyon: veritabanı bilmez, hiçbir şey
    /// yazmaz; girdi olarak kalem katalogu + girilen tutarlar alır, sonucu döner.
    ///
    /// Beyanname sırası (sıra sonucu doğrudan değiştirir):
    /// <code>
    ///     Ticari bilanço kârı (690)
    ///   + KKEG
    ///   = Kâr ve ilaveler toplamı
    ///   − Zarar olsa dahi indirilecek istisnalar        (matrahı negatife çekebilir)
    ///   = Kâr / Zarar
    ///   − Geçmiş yıl zararları                          (en eskiden başlayarak, 5 hesap dönemi)
    ///   − Kazancın bulunması hâlinde indirilecekler     (matrahı sıfırın altına indiremez)
    ///   = Matrah  ×  oran  =  hesaplanan vergi
    ///   − Mahsuplar
    ///   = Ödenecek / iade edilecek vergi
    /// </code>
    /// </summary>
    public static class VergiHesaplamaMotoru
    {
        /// <summary>
        /// KVK 32/C yurt içi asgari kurumlar vergisi oranı. Kalem tablosunda karşılığı olmayan
        /// (kaleme değil, hesaba ait) tek orandır; mevzuat değişirse burası güncellenmelidir.
        /// </summary>
        public const decimal AsgariKurumlarVergisiOrani = 10m;

        /// <summary>Geçmiş yıl zararlarının mahsup edilebileceği azami hesap dönemi sayısı (KVK 9/1-a).</summary>
        public const int ZararMahsupYilSiniri = 5;

        /// <summary>Girdi paketi. Kalemler katalogdan, tutarlar beyannameden gelir.</summary>
        public sealed class Girdi
        {
            public short DonemYil { get; init; }
            public decimal TicariKar { get; init; }
            public decimal KvOrani { get; init; } = 25m;
            public decimal? IndirimliOran { get; init; }
            public decimal? IndirimliOranMatrahi { get; init; }
            public bool AsgariKvHesapla { get; init; } = true;

            /// <summary>
            /// Hesaba girecek kalem katalogu. Çağıran, aktif kalemlere geçmişte kullanılmış
            /// pasif kalemleri de ekleyerek verir; motor ayrıca süzme yapmaz.
            /// </summary>
            public IReadOnlyList<VergiKalemi> Kalemler { get; init; } = Array.Empty<VergiKalemi>();

            /// <summary>Kalem Id -> girilen tutar/not.</summary>
            public IReadOnlyList<VergiSatirYazDto> Satirlar { get; init; } = Array.Empty<VergiSatirYazDto>();

            public IReadOnlyList<GecmisYilZarariYazDto> GecmisYilZararlari { get; init; } = Array.Empty<GecmisYilZarariYazDto>();
        }

        public static VergiSonucDto Hesapla(Girdi girdi)
        {
            var sonuc = new VergiSonucDto { TicariKar = Yuvarla(girdi.TicariKar) };

            // Katalogu çağıran belirler; motor Aktif'e göre süzmez. Pasife alınmış bir kalem
            // yeni girişte seçilemez ama geçmiş beyannamede tutarı durur — burada süzülseydi
            // kayıtlı beyanname kalem pasife alınınca sessizce değişirdi.
            var kalemler = girdi.Kalemler.ToList();
            var kalemIndeks = kalemler.ToDictionary(k => k.Id);

            // Kalem başına girilen tutar (aynı kalem birden fazla gelirse toplanır).
            var tutarlar = new Dictionary<int, decimal>();
            var notlar = new Dictionary<int, string?>();
            foreach (var s in girdi.Satirlar)
            {
                if (!kalemIndeks.ContainsKey(s.VergiKalemiId)) continue;
                tutarlar[s.VergiKalemiId] = tutarlar.GetValueOrDefault(s.VergiKalemiId) + Yuvarla(s.Tutar);
                if (!string.IsNullOrWhiteSpace(s.Aciklama)) notlar[s.VergiKalemiId] = s.Aciklama;
            }

            decimal Tutar(int kalemId) => tutarlar.GetValueOrDefault(kalemId);

            // ─────────── 1) İlaveler (KKEG) ───────────
            //
            // İstisnaya ilişkin KKEG (tür b) ticari kâra eklenir AMA aynı tutar bağlı istisnayı
            // büyütür; matraha net etkisi sıfırdır. Bağlı istisnası olmayan (b) kalemi
            // büyütecek bir istisna bulamadığı için matrahı artıran KKEG gibi davranır.
            var iliskiliKkegToplami = new Dictionary<int, decimal>();   // istisna kalemi Id -> eklenecek tutar

            foreach (var k in kalemler.Where(k => k.Grup == VergiKalemGrubu.Kkeg).OrderBy(k => k.SiraNo).ThenBy(k => k.Kod))
            {
                var tutar = Tutar(k.Id);
                var bagliVar = k.IstisnayaIliskinMi && k.BagliIstisnaKalemiId is int bagli && kalemIndeks.ContainsKey(bagli);
                var matrahiArtirir = !bagliVar;

                if (bagliVar)
                {
                    var bagliId = k.BagliIstisnaKalemiId!.Value;
                    iliskiliKkegToplami[bagliId] = iliskiliKkegToplami.GetValueOrDefault(bagliId) + tutar;
                }

                sonuc.Ilaveler.Add(new VergiSonucSatirDto
                {
                    VergiKalemiId = k.Id,
                    Kod = k.Kod,
                    Ad = k.Ad,
                    AltGrup = k.AltGrup,
                    KanunMaddesi = k.KanunMaddesi,
                    Hatirlatma = k.Hatirlatma,
                    Grup = k.Grup,
                    SiraNo = k.SiraNo,
                    GirilenTutar = tutar,
                    EfektifTutar = tutar,
                    MatrahiArtirir = matrahiArtirir,
                    Aciklama = notlar.GetValueOrDefault(k.Id)
                });

                if (k.IstisnayaIliskinMi && !bagliVar && tutar > 0)
                    sonuc.Uyarilar.Add(new VergiUyariDto
                    {
                        Seviye = VergiUyariSeviyesi.Uyari,
                        KalemKodu = k.Kod,
                        Mesaj = $"'{k.Ad}' istisnaya ilişkin KKEG olarak tanımlı ama bağlı istisna kalemi seçilmemiş. " +
                                "Tutar matrahı artıran KKEG gibi işlendi; kalem yönetiminden bağlı istisnayı seçin."
                    });
            }

            sonuc.IlaveHamToplam = Yuvarla(sonuc.Ilaveler.Sum(x => x.GirilenTutar));
            sonuc.IlaveMatrahaEtkiEden = Yuvarla(sonuc.Ilaveler.Where(x => x.MatrahiArtirir).Sum(x => x.GirilenTutar));

            // Beyannameye ham toplam yazılır; istisnaya ilişkin kısım aşağıda istisna
            // büyütülerek geri çıkacağı için net etki yine matraha etki eden kısımdır.
            sonuc.KarVeIlavelerToplami = Yuvarla(sonuc.TicariKar + sonuc.IlaveHamToplam);

            // ─────────── 2) Zarar olsa dahi indirilecek istisnalar ───────────
            foreach (var k in kalemler.Where(k => k.Grup == VergiKalemGrubu.ZararOlsaDahi).OrderBy(k => k.SiraNo).ThenBy(k => k.Kod))
            {
                var girilen = Tutar(k.Id);
                var iliskili = iliskiliKkegToplami.GetValueOrDefault(k.Id);
                var efektif = Yuvarla(girilen + iliskili);

                sonuc.ZararOlsaDahiIndirimler.Add(new VergiSonucSatirDto
                {
                    VergiKalemiId = k.Id,
                    Kod = k.Kod,
                    Ad = k.Ad,
                    AltGrup = k.AltGrup,
                    KanunMaddesi = k.KanunMaddesi,
                    Hatirlatma = k.Hatirlatma,
                    Grup = k.Grup,
                    SiraNo = k.SiraNo,
                    GirilenTutar = girilen,
                    EfektifTutar = efektif,
                    IliskiliKkeg = iliskili,
                    Aciklama = notlar.GetValueOrDefault(k.Id)
                });
            }

            sonuc.ZararOlsaDahiToplam = Yuvarla(sonuc.ZararOlsaDahiIndirimler.Sum(x => x.EfektifTutar));

            // Grup 2 matrahı negatife çekebilir; taban sıfırlanmaz.
            sonuc.KarZarar = Yuvarla(sonuc.KarVeIlavelerToplami - sonuc.ZararOlsaDahiToplam);

            // ─────────── 3) Geçmiş yıl zararları ───────────
            var kalanKazanc = Math.Max(0m, sonuc.KarZarar);

            foreach (var z in girdi.GecmisYilZararlari.OrderBy(z => z.ZararYili))
            {
                var zararTutari = Yuvarla(Math.Abs(z.ZararTutari));
                var yilFarki = girdi.DonemYil - z.ZararYili;
                var uygun = yilFarki >= 1 && yilFarki <= ZararMahsupYilSiniri;

                var satir = new ZararMahsupSatirDto
                {
                    ZararYili = z.ZararYili,
                    ZararTutari = zararTutari,
                    MahsupEdilebilir = uygun
                };

                if (!uygun)
                {
                    satir.DevredenTutar = 0m;
                    satir.Uyari = yilFarki > ZararMahsupYilSiniri
                        ? $"{z.ZararYili} yılı zararı {ZararMahsupYilSiniri} hesap döneminden eski; mahsup edilemez ve mahsup hakkı yanmıştır."
                        : $"{z.ZararYili} yılı içinde bulunulan dönemden eski değil; geçmiş yıl zararı olarak mahsup edilemez.";

                    sonuc.Uyarilar.Add(new VergiUyariDto
                    {
                        Seviye = VergiUyariSeviyesi.Uyari,
                        Mesaj = satir.Uyari
                    });
                }
                else
                {
                    // En eski yıldan başlayarak kalan kazanç kadar mahsup.
                    var mahsup = Math.Min(kalanKazanc, zararTutari);
                    satir.MahsupEdilen = mahsup;
                    satir.DevredenTutar = Yuvarla(zararTutari - mahsup);
                    kalanKazanc = Yuvarla(kalanKazanc - mahsup);
                }

                sonuc.ZararMahsuplari.Add(satir);
            }

            sonuc.ZararMahsupToplami = Yuvarla(sonuc.ZararMahsuplari.Sum(x => x.MahsupEdilen));
            sonuc.MahsupSonrasiKazanc = Yuvarla(sonuc.KarZarar - sonuc.ZararMahsupToplami);

            // ─────────── 4) Kazancın bulunması hâlinde indirilecekler ───────────
            //
            // KVK 10 anlamında kurum kazancı (üst sınır tabanı):
            // ticari bilanço kârı − (iştirak kazançları istisnası + geçmiş yıl zararları).
            // Bu tanım bağış/sponsorluk gibi yüzdesel tavanların matrahıdır; KKEG içermez.
            var istirakKazanciIstisnasi = sonuc.ZararOlsaDahiIndirimler
                .Where(x => string.Equals(x.Kod, "IST-01", StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.EfektifTutar);

            sonuc.KurumKazanci = Math.Max(0m, Yuvarla(sonuc.TicariKar - istirakKazanciIstisnasi - sonuc.ZararMahsupToplami));

            // Grup 3 matrahı sıfırın altına indiremez: kalan kazanç kadar uygulanır.
            var grup3Kalan = Math.Max(0m, sonuc.MahsupSonrasiKazanc);

            foreach (var k in kalemler.Where(k => k.Grup == VergiKalemGrubu.KazancVarsa).OrderBy(k => k.SiraNo).ThenBy(k => k.Kod))
            {
                var girilen = Tutar(k.Id);

                var satir = new VergiSonucSatirDto
                {
                    VergiKalemiId = k.Id,
                    Kod = k.Kod,
                    Ad = k.Ad,
                    AltGrup = k.AltGrup,
                    KanunMaddesi = k.KanunMaddesi,
                    Hatirlatma = k.Hatirlatma,
                    Grup = k.Grup,
                    SiraNo = k.SiraNo,
                    GirilenTutar = girilen,
                    Aciklama = notlar.GetValueOrDefault(k.Id)
                };

                // a) Üst sınır kontrolü
                var sinirliTutar = girilen;
                var ustSinir = UstSinirTutariHesapla(k, sonuc.KurumKazanci);
                satir.UstSinirTutari = ustSinir;

                if (ustSinir is decimal sinir && girilen > sinir)
                {
                    satir.SinirAsimi = Yuvarla(girilen - sinir);
                    sinirliTutar = sinir;

                    sonuc.Uyarilar.Add(new VergiUyariDto
                    {
                        Seviye = VergiUyariSeviyesi.Uyari,
                        KalemKodu = k.Kod,
                        Mesaj = $"'{k.Ad}' için üst sınır {Bicim(sinir)} TL; girilen tutar sınırı {Bicim(satir.SinirAsimi)} TL aşıyor. " +
                                "Aşan kısım indirilemez, KKEG-17'ye taşınmalıdır."
                    });
                }

                // b) Kalan kazanç kontrolü
                var uygulanan = Math.Min(sinirliTutar, grup3Kalan);
                satir.EfektifTutar = uygulanan;
                grup3Kalan = Yuvarla(grup3Kalan - uygulanan);

                var kullanilamayan = Yuvarla(sinirliTutar - uygulanan);
                satir.KullanilamayanTutar = kullanilamayan;

                if (kullanilamayan > 0)
                {
                    if (k.DevredebilirMi)
                    {
                        satir.DevredenTutar = kullanilamayan;
                        sonuc.Uyarilar.Add(new VergiUyariDto
                        {
                            Seviye = VergiUyariSeviyesi.Bilgi,
                            KalemKodu = k.Kod,
                            Mesaj = $"'{k.Ad}' kaleminde kazanç yetersizliği nedeniyle {Bicim(kullanilamayan)} TL indirilemedi; " +
                                    "bu tutar sonraki dönemlere devreder."
                        });
                    }
                    else
                    {
                        satir.YananTutar = kullanilamayan;
                        sonuc.Uyarilar.Add(new VergiUyariDto
                        {
                            Seviye = VergiUyariSeviyesi.Uyari,
                            KalemKodu = k.Kod,
                            Mesaj = $"'{k.Ad}' kaleminde kazanç yetersizliği nedeniyle {Bicim(kullanilamayan)} TL indirilemedi; " +
                                    "bu kalem devretmediği için indirim hakkı yanar."
                        });
                    }
                }

                sonuc.KazancVarsaIndirimler.Add(satir);
            }

            sonuc.KazancVarsaToplam = Yuvarla(sonuc.KazancVarsaIndirimler.Sum(x => x.EfektifTutar));

            // ─────────── 5) Matrah ───────────
            // Grup 3 sıfırın altına indiremediği için matrah negatifse bu Grup 2'den gelir.
            sonuc.Matrah = Yuvarla(sonuc.MahsupSonrasiKazanc - sonuc.KazancVarsaToplam);

            // ─────────── 6) Vergi ───────────
            var vergiyeTabiMatrah = Math.Max(0m, sonuc.Matrah);

            var indirimliMatrah = 0m;
            if (girdi.IndirimliOran is decimal indirimliOran && girdi.IndirimliOranMatrahi is decimal indirimliMatrahGirdi)
                indirimliMatrah = Math.Min(Math.Max(0m, Yuvarla(indirimliMatrahGirdi)), vergiyeTabiMatrah);
            else
                indirimliOran = girdi.KvOrani;

            var genelMatrah = Yuvarla(vergiyeTabiMatrah - indirimliMatrah);

            sonuc.IndirimliOranMatrahi = indirimliMatrah;
            sonuc.GenelOranMatrahi = genelMatrah;
            sonuc.NormalVergi = Yuvarla(
                genelMatrah * girdi.KvOrani / 100m +
                indirimliMatrah * (girdi.IndirimliOran ?? girdi.KvOrani) / 100m);

            // ─────────── 7) Yurt içi asgari kurumlar vergisi (KVK 32/C) ───────────
            sonuc.AsgariKvHesaplandi = girdi.AsgariKvHesapla;

            if (girdi.AsgariKvHesapla)
            {
                // Asgari matrah = ticari kâr + KKEG − (asgari matrahtan düşebilen istisnalar)
                var asgariDusenler = sonuc.ZararOlsaDahiIndirimler
                        .Where(x => kalemIndeks[x.VergiKalemiId].AsgariMatrahtanDuser)
                        .Sum(x => x.EfektifTutar)
                    + sonuc.KazancVarsaIndirimler
                        .Where(x => kalemIndeks[x.VergiKalemiId].AsgariMatrahtanDuser)
                        .Sum(x => x.EfektifTutar);

                sonuc.AsgariMatrah = Math.Max(0m, Yuvarla(sonuc.TicariKar + sonuc.IlaveHamToplam - asgariDusenler));
                sonuc.AsgariVergi = Yuvarla(sonuc.AsgariMatrah * AsgariKurumlarVergisiOrani / 100m);

                sonuc.AsgariUygulandi = sonuc.AsgariVergi > sonuc.NormalVergi;
                sonuc.HesaplananVergi = Math.Max(sonuc.NormalVergi, sonuc.AsgariVergi);

                if (sonuc.AsgariUygulandi)
                    sonuc.Uyarilar.Add(new VergiUyariDto
                    {
                        Seviye = VergiUyariSeviyesi.Bilgi,
                        Mesaj = $"Yurt içi asgari kurumlar vergisi ({Bicim(sonuc.AsgariVergi)} TL) normal hesaplanan vergiden " +
                                $"({Bicim(sonuc.NormalVergi)} TL) yüksek olduğu için asgari vergi uygulandı."
                    });
            }
            else
            {
                sonuc.HesaplananVergi = sonuc.NormalVergi;
            }

            // ─────────── 8) Mahsuplar ───────────
            foreach (var k in kalemler.Where(k => k.Grup == VergiKalemGrubu.Mahsup).OrderBy(k => k.SiraNo).ThenBy(k => k.Kod))
            {
                var tutar = Tutar(k.Id);
                sonuc.Mahsuplar.Add(new VergiSonucSatirDto
                {
                    VergiKalemiId = k.Id,
                    Kod = k.Kod,
                    Ad = k.Ad,
                    AltGrup = k.AltGrup,
                    KanunMaddesi = k.KanunMaddesi,
                    Hatirlatma = k.Hatirlatma,
                    Grup = k.Grup,
                    SiraNo = k.SiraNo,
                    GirilenTutar = tutar,
                    EfektifTutar = tutar,
                    Aciklama = notlar.GetValueOrDefault(k.Id)
                });
            }

            sonuc.MahsupToplami = Yuvarla(sonuc.Mahsuplar.Sum(x => x.EfektifTutar));
            sonuc.OdenecekVergi = Yuvarla(sonuc.HesaplananVergi - sonuc.MahsupToplami);

            if (sonuc.OdenecekVergi < 0)
                sonuc.Uyarilar.Add(new VergiUyariDto
                {
                    Seviye = VergiUyariSeviyesi.Bilgi,
                    Mesaj = $"Mahsuplar hesaplanan vergiyi aşıyor; {Bicim(Math.Abs(sonuc.OdenecekVergi))} TL iade veya mahsup konusu."
                });

            return sonuc;
        }

        /// <summary>Kalemin tutarsal üst sınırı. Yüzdesel sınırlarda taban kurum kazancıdır.</summary>
        private static decimal? UstSinirTutariHesapla(VergiKalemi kalem, decimal kurumKazanci)
            => (kalem.UstSinirTuru, kalem.UstSinirDeger) switch
            {
                (Domain.UstSinirTuru.KurumKazanciYuzdesi, decimal yuzde) => Yuvarla(kurumKazanci * yuzde / 100m),
                (Domain.UstSinirTuru.SabitTutar, decimal tutar) => Yuvarla(tutar),
                _ => null
            };

        private static decimal Yuvarla(decimal deger) => Math.Round(deger, 2, MidpointRounding.AwayFromZero);

        private static string Bicim(decimal deger) =>
            deger.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
    }
}
