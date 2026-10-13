using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;
using CatalogService.Api.Features.Yapilacaklar.Services;

namespace CatalogService.UnitTests.Yapilacaklar
{
    /// <summary>
    /// Firma Panosu → Yapılacaklar (Aşama 1): <see cref="ChecklistKartKurucu"/>. Saf; veritabanı yok, gün sabit
    /// (11.10.2026). Satırlar her testte <see cref="YapilacaklarKurucu.Satirlar"/>'dan gelir — kartlar kuyruğun
    /// kendisinden kurulur, ayrı bir dönem kuralı yok.
    ///
    /// Senaryo:
    /// <list type="bullet">
    /// <item>A (cengiz): KDV Ağustos yapıldı, Eylül açık (son 28.10); 3. çeyrek geçici vergi açık (son 17.11);
    /// "Bankalar" her ay ayın 1'i, Eylül'den beri — Eylül ve Ekim gecikti. Yasal KDV'nin prosedür satırı da var.</item>
    /// <item>B (aybüke): KDV Ağustos ve Eylül yapıldı, Ekim açık (son 28.11).</item>
    /// <item>C (sorumlusu yok): tek seferlik iş 05.11.2026.</item>
    /// </list>
    /// </summary>
    public class ChecklistKartTests
    {
        private static readonly DateTime Bugun = new(2026, 10, 11);

        private const int A = 1, B = 2, C = 3;
        private const int Cengiz = 16, Aybuke = 17;

        private const int KdvAgu = 101, KdvEyl = 102, KdvEki = 103, GeciciQ3 = 201;
        private const int Bankalar = 10, Prosedur = 11, TekSefer = 12;

        private static List<YapilacaklarKurucu.FirmaGirdisi> Firmalar() => new()
        {
            new(A, "ALFA", Cengiz, "cengiz", new[] { "0015", "0033" }),
            new(B, "BETA", Aybuke, "aybüke", new[] { "0015" }),
            new(C, "CEZA", null, null, Array.Empty<string>())
        };

        private static VergiTakvimi Kdv(int id, int ay, DateTime sonGun) => new()
        {
            Id = id, MukellefiyetKodu = "0015", Ad = "KDV beyannamesi", Tekrar = IsTekrari.Aylik, Yil = 2026, DonemNo = ay,
            DonemBas = new DateTime(2026, ay, 1), DonemBit = new DateTime(2026, ay, 1).AddMonths(1).AddDays(-1),
            SonGun = sonGun, Aktif = true
        };

        private static List<VergiTakvimi> Takvim() => new()
        {
            Kdv(KdvAgu, 8, new DateTime(2026, 9, 28)),
            Kdv(KdvEyl, 9, new DateTime(2026, 10, 28)),
            Kdv(KdvEki, 10, new DateTime(2026, 11, 28)),
            new()
            {
                Id = GeciciQ3, MukellefiyetKodu = "0033", Ad = "Geçici vergi", Tekrar = IsTekrari.UcAylik, Yil = 2026, DonemNo = 3,
                DonemBas = new DateTime(2026, 7, 1), DonemBit = new DateTime(2026, 9, 30), SonGun = new DateTime(2026, 11, 17),
                Aktif = true
            }
        };

        private static List<FirmaIsi> Ozel() => new()
        {
            new()
            {
                Id = Bankalar, FirmaId = A, Baslik = "Bankalar", Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.AyinGunu,
                AyinGunu = 1, IlkDonem = "2026-09", OlusturmaZamani = new DateTime(2026, 8, 15, 9, 0, 0, DateTimeKind.Utc)
            },
            // Yasal KDV'nin prosedür satırı: özel iş DEĞİL, karta madde olarak düşmemeli.
            new()
            {
                Id = Prosedur, FirmaId = A, Baslik = "KDV prosedürü", Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.AyinGunu,
                AyinGunu = 1, YasalMukellefiyetKodu = "0015", OlusturmaZamani = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                Id = TekSefer, FirmaId = C, Baslik = "Ortaklar kurulu", Tekrar = IsTekrari.TekSefer,
                TekSeferTarih = new DateTime(2026, 11, 5), OlusturmaZamani = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc)
            }
        };

        private static IsTamamlama Yapildi(IsKaynagi tip, int kaynak, int firma, string donem) => new()
        {
            KaynakTip = tip, KaynakId = kaynak, FirmaId = firma, DonemAnahtari = donem,
            TamamlanmaZamani = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc)
        };

        private static List<IsTamamlama> Tamamlamalar() => new()
        {
            Yapildi(IsKaynagi.Yasal, KdvAgu, A, "2026-08"),
            Yapildi(IsKaynagi.Yasal, KdvAgu, B, "2026-08"),
            Yapildi(IsKaynagi.Yasal, KdvEyl, B, "2026-09"),
            // Yalnız notu olan kayıt (zaman boş): yapılmış SAYILMAZ.
            new() { KaynakTip = IsKaynagi.Ozel, KaynakId = Bankalar, FirmaId = A, DonemAnahtari = "2026-09", Not = "ekstre gelmedi" }
        };

        // ---- Kategoriler (Aşama 2): seed'in dört kategorisi ----
        private const int BankaCari = 1, KayitKapanis = 2, Bordro = 3, Beyanname = 4;

        private static List<IsKategorisi> Kategoriler() => new()
        {
            new() { Id = BankaCari, Ad = "Banka & Cari", Sira = 1, Aktif = true },
            new() { Id = KayitKapanis, Ad = "Kayıt & Kapanış", Sira = 2, Aktif = true },
            new() { Id = Bordro, Ad = "Bordro", Sira = 3, Aktif = true },
            new() { Id = Beyanname, Ad = "Beyanname", Sira = 4, Aktif = true, VarsayilanYasal = true }
        };

        private static IsKategoriBaglami Baglam(List<IsKategorisi>? kategoriler = null,
                                                Dictionary<string, int>? mukellefiyetler = null,
                                                Dictionary<int, (int?, int?)>? ozelIsler = null)
            => new(kategoriler ?? Kategoriler(), mukellefiyetler ?? new(), ozelIsler ?? new());

        private static FirmaPanosuDto Kur(FirmaPanosuFiltresi? filtre = null, List<IsTamamlama>? tamamlar = null,
                                          List<FirmaIsi>? ozel = null, IsKategoriBaglami? kategoriler = null)
        {
            var firmalar = Firmalar();
            var satirlar = YapilacaklarKurucu.Satirlar(Bugun, firmalar, Takvim(), ozel ?? Ozel(), tamamlar ?? Tamamlamalar());
            return ChecklistKartKurucu.Kur(Bugun, firmalar, satirlar, filtre ?? new FirmaPanosuFiltresi(), kategoriler ?? Baglam());
        }

        private static ChecklistKartiDto Kart(FirmaPanosuDto dto, int firma, string donem)
            => dto.Donemler.SelectMany(d => d.Kartlar).Single(k => k.FirmaId == firma && k.DonemAnahtari == donem);

        private static ChecklistKartiDto? KartVarsa(FirmaPanosuDto dto, int firma, string donem)
            => dto.Donemler.SelectMany(d => d.Kartlar).SingleOrDefault(k => k.FirmaId == firma && k.DonemAnahtari == donem);

        private static IEnumerable<ChecklistMaddesiDto> Maddeler(ChecklistKartiDto k) => k.Bolumler.SelectMany(b => b.Maddeler);

        private static int Sayac(FirmaPanosuDto dto, PanoKovasi kova) => dto.Sayaclar.Single(s => s.Kova == kova).Sayi;

        // ---- Gruplama ----

        [Fact]
        public void Kartlar_firma_ve_donem_basina_birdir_donemler_eskiden_yeniye()
        {
            var dto = Kur();

            Assert.Equal(new[] { "2026-08", "2026-09", "2026-10", "2026-11" }, dto.Donemler.Select(d => d.Anahtar));
            Assert.Equal("Eylül 2026", dto.Donemler[1].Etiket);

            var kartlar = dto.Donemler.SelectMany(d => d.Kartlar).ToList();
            Assert.Equal(kartlar.Count, kartlar.Select(k => (k.FirmaId, k.DonemAnahtari)).Distinct().Count());
            Assert.Equal(kartlar.Count, dto.KartSayisi);
            Assert.Equal(3, dto.FirmaSayisi);

            // Eylül: A ve B; kart sırası firma adı.
            Assert.Equal(new[] { "ALFA", "BETA" }, dto.Donemler[1].Kartlar.Select(k => k.FirmaAdi));
            Assert.Equal(2, dto.Donemler[1].FirmaSayisi);
        }

        [Fact]
        public void Bolumler_kategori_sirasinda_kategorisiz_ozel_is_digerde_maddeler_son_gune_gore()
        {
            var kart = Kart(Kur(), A, "2026-09");

            // Eşlemesiz yasal → Beyanname; kategorisiz özel → Diğer (en sonda). Boş bölüm yok.
            Assert.Equal(new[] { "Beyanname", "Diğer" }, kart.Bolumler.Select(b => b.Ad));
            Assert.Equal(new[] { "k4", "diger" }, kart.Bolumler.Select(b => b.Anahtar));
            Assert.Equal(new[] { "KDV beyannamesi", "Geçici vergi" }, kart.Bolumler[0].Maddeler.Select(m => m.Satir.Baslik));
            Assert.Equal(new[] { "Bankalar" }, kart.Bolumler[1].Maddeler.Select(m => m.Satir.Baslik));
            Assert.Equal(3, kart.Toplam);
            Assert.Equal(1, kart.Bolumler[1].Gecikmis);
            Assert.True(kart.YasalVar);
        }

        // ---- Dönem merkezlilik ----

        [Fact]
        public void Eylul_KDVsi_son_gunu_Ekimde_olsa_da_Eylul_kartinda_durur()
        {
            var dto = Kur();
            var kdv = Maddeler(Kart(dto, A, "2026-09")).Single(m => m.Satir.KaynakId == KdvEyl);

            Assert.Equal(new DateTime(2026, 10, 28), kdv.Satir.SonGun);
            Assert.Equal(PanoKovasi.BuAy, kdv.Kova);
            Assert.DoesNotContain(Maddeler(Kart(dto, A, "2026-10")), m => m.Satir.KaynakId == KdvEyl);
        }

        [Fact]
        public void Ceyrek_isi_ceyregin_son_ayinin_kartinda()
        {
            var gecici = Maddeler(Kart(Kur(), A, "2026-09")).Single(m => m.Satir.KaynakId == GeciciQ3);

            Assert.Equal("2026-Q3", gecici.Satir.DonemAnahtari);
            Assert.Equal(PanoKovasi.Sonra, gecici.Kova);   // son gün 17.11
        }

        [Fact]
        public void Tek_seferlik_is_tarihinin_ayinin_kartinda()
        {
            var kart = Kart(Kur(), C, "2026-11");

            Assert.Equal("Ortaklar kurulu", Assert.Single(Maddeler(kart)).Satir.Baslik);
            Assert.Equal(PanoKovasi.Sonra, kart.Durum);
        }

        [Theory]
        [InlineData(IsTekrari.Aylik, "2026-09", "2026-09")]
        [InlineData(IsTekrari.UcAylik, "2026-Q1", "2026-03")]
        [InlineData(IsTekrari.UcAylik, "2026-Q4", "2026-12")]
        [InlineData(IsTekrari.Yillik, "2025", "2025-12")]
        [InlineData(IsTekrari.TekSefer, "tek", "2026-11")]
        public void Kart_donemi_donemin_kapandigi_ay(IsTekrari tekrar, string anahtar, string beklenen)
        {
            var satir = new IsSatiriDto { Tekrar = tekrar, DonemAnahtari = anahtar, SonGun = new DateTime(2026, 11, 20) };

            Assert.Equal(beklenen, ChecklistKartKurucu.KartDonemi(satir).Anahtar);
        }

        // ---- Sayaçlar ve kova ----

        [Fact]
        public void Sayaclar_madde_sayar_vadeye_gore()
        {
            var dto = Kur();

            // A: Bankalar Eylül + Ekim (gecikti), KDV Eylül (bu ay), geçici vergi (sonra), KDV Ağustos (tamam).
            // B: KDV Eylül (tamam), KDV Ekim (sonra). C: tek seferlik (sonra).
            Assert.Equal(new[] { PanoKovasi.Gecikti, PanoKovasi.BuAy, PanoKovasi.Sonra, PanoKovasi.Tamam },
                         dto.Sayaclar.Select(s => s.Kova));
            Assert.Equal(2, Sayac(dto, PanoKovasi.Gecikti));
            Assert.Equal(1, Sayac(dto, PanoKovasi.BuAy));
            Assert.Equal(3, Sayac(dto, PanoKovasi.Sonra));
            Assert.Equal(2, Sayac(dto, PanoKovasi.Tamam));

            // Sayaçların toplamı kartlardaki madde sayısıyla aynı.
            Assert.Equal(dto.Sayaclar.Sum(s => s.Sayi), dto.Donemler.Sum(d => d.Toplam));

            Assert.Equal("en eskisi 40 gün önce", dto.Sayaclar[0].Aciklama);   // 01.09 → 11.10
            Assert.Equal("ilk son gün 28.10", dto.Sayaclar[1].Aciklama);
        }

        [Theory]
        [InlineData(2026, 10, 10, false, PanoKovasi.Gecikti)]
        [InlineData(2026, 10, 11, false, PanoKovasi.BuAy)]       // son gün bugün: gecikmedi
        [InlineData(2026, 10, 31, false, PanoKovasi.BuAy)]
        [InlineData(2026, 11, 3, false, PanoKovasi.Sonra)]       // Yapılacaklar'da "Bu hafta" olurdu; burada ay farklı
        [InlineData(2026, 9, 1, true, PanoKovasi.Tamam)]
        public void Kova_vadeye_gore(int y, int a, int g, bool tamam, PanoKovasi beklenen)
            => Assert.Equal(beklenen, ChecklistKartKurucu.Kova(new DateTime(y, a, g), Bugun, tamam));

        // ---- Kart durumu ----

        [Fact]
        public void Kart_durumu_acik_maddelerin_en_erken_son_gunune_gore()
        {
            var dto = Kur();

            var eylul = Kart(dto, A, "2026-09");
            Assert.Equal(PanoKovasi.Gecikti, eylul.Durum);           // Bankalar 01.09 gecikti; KDV 28.10 bu ay
            Assert.Equal("Bankalar", eylul.SiradakiBaslik);
            Assert.Equal(new DateTime(2026, 9, 1), eylul.SiradakiSonGun);
            Assert.Equal(1, eylul.Gecikmis);

            Assert.Equal(PanoKovasi.Sonra, Kart(dto, B, "2026-10").Durum);

            var agustos = Kart(dto, A, "2026-08");
            Assert.Equal(PanoKovasi.Tamam, agustos.Durum);
            Assert.Null(agustos.SiradakiBaslik);
            Assert.Equal(agustos.Toplam, agustos.Tamam);
        }

        // ---- Prosedür satırı ve "yalnız notu var" kaydı ----

        [Fact]
        public void Prosedur_satiri_madde_degildir()
        {
            var tumu = Kur().Donemler.SelectMany(d => d.Kartlar).SelectMany(Maddeler).ToList();

            Assert.DoesNotContain(tumu, m => m.Satir.KaynakTip == IsKaynagi.Ozel && m.Satir.KaynakId == Prosedur);
            Assert.DoesNotContain(tumu, m => m.Satir.Baslik == "KDV prosedürü");
        }

        [Fact]
        public void Zamani_bos_kayit_tamam_sayilmaz()
        {
            var bankalar = Maddeler(Kart(Kur(), A, "2026-09")).Single(m => m.Satir.KaynakId == Bankalar);

            Assert.False(bankalar.Satir.Tamamlandi);
            Assert.Equal(PanoKovasi.Gecikti, bankalar.Kova);
        }

        // ---- Kuyruk sınır durumları ----

        [Fact]
        public void Kuyruk_yalniz_son_yapilmis_donemi_gosterir_daha_eski_yapilmis_kart_cikmaz()
        {
            var dto = Kur();

            // B'nin Ağustos KDV'si yapıldı ama önünde Eylül de yapıldı: Ağustos kartı yok.
            Assert.Null(KartVarsa(dto, B, "2026-08"));
            Assert.Equal(PanoKovasi.Tamam, Kart(dto, B, "2026-09").Durum);
        }

        [Fact]
        public void Gecikmis_butun_donemler_ayri_kartta_ilk_donemden_once_kart_yok()
        {
            var dto = Kur();

            Assert.Equal(PanoKovasi.Gecikti, Maddeler(Kart(dto, A, "2026-10")).Single(m => m.Satir.KaynakId == Bankalar).Kova);
            Assert.DoesNotContain(Maddeler(Kart(dto, A, "2026-08")), m => m.Satir.KaynakId == Bankalar);   // IlkDonem Eylül
            Assert.Null(KartVarsa(dto, A, "2026-11"));                                                        // sıradaki açık dönem Ekim'de
        }

        [Fact]
        public void Gecikmis_donem_dururken_sonraki_donem_isaretlenirse_kuyruktan_duser()
        {
            // Mevcut kuyruk kuralı: ilk açık dönemin ARKASINDAKİ yapılmış dönem gösterilmez. Detay penceresi
            // bu yüzden işaret sonrası maddenin kaybolmasına hazırlıklı olmalı.
            var tamamlar = Tamamlamalar();
            tamamlar.Add(Yapildi(IsKaynagi.Ozel, Bankalar, A, "2026-10"));

            var dto = Kur(tamamlar: tamamlar);

            Assert.DoesNotContain(Maddeler(Kart(dto, A, "2026-09")), m => m.Satir.KaynakId == Bankalar && m.Satir.Tamamlandi);
            var ekim = KartVarsa(dto, A, "2026-10");
            Assert.True(ekim is null || Maddeler(ekim).All(m => m.Satir.KaynakId != Bankalar));
        }

        // ---- Çok firmalı iş ----

        [Fact]
        public void Cok_firmali_is_her_firmanin_kartina_ayri_duser_ortak_sayisi_tasir()
        {
            var dto = Kur();

            var a = Maddeler(Kart(dto, A, "2026-09")).Single(m => m.Satir.KaynakId == KdvEyl);
            var b = Maddeler(Kart(dto, B, "2026-09")).Single(m => m.Satir.KaynakId == KdvEyl);

            Assert.Equal(A, a.Satir.FirmaId);
            Assert.Equal(B, b.Satir.FirmaId);
            Assert.False(a.Satir.Tamamlandi);
            Assert.True(b.Satir.Tamamlandi);
            Assert.Equal(2, a.OrtakFirmaSayisi);
            Assert.Equal(2, b.OrtakFirmaSayisi);

            // Liste görünümü Yapılacaklar'ın gruplamasını korur: aynı iş tek satır, "2 firma".
            var grup = dto.Liste.Single(l => l.Grup.Baslik == "KDV beyannamesi" && l.Grup.SonGun == new DateTime(2026, 10, 28));
            Assert.Equal(2, grup.Grup.FirmaSayisi);
            Assert.Equal(PanoKovasi.BuAy, grup.Kova);   // bir firması açık
        }

        // ---- Filtreler ----

        [Fact]
        public void Tur_filtresi_saymadan_once_uygulanir()
        {
            var dto = Kur(new FirmaPanosuFiltresi { Turler = { IsKaynagi.Yasal } });

            Assert.Equal(0, Sayac(dto, PanoKovasi.Gecikti));
            Assert.Equal(1, Sayac(dto, PanoKovasi.BuAy));
            Assert.Equal(2, Sayac(dto, PanoKovasi.Sonra));
            Assert.Equal(2, Sayac(dto, PanoKovasi.Tamam));
            Assert.All(dto.Donemler.SelectMany(d => d.Kartlar).SelectMany(Maddeler), m => Assert.Equal(IsKaynagi.Yasal, m.Satir.KaynakTip));
            Assert.Null(KartVarsa(dto, C, "2026-11"));   // C'nin yalnız özel işi var: kart hiç çıkmaz
            Assert.Equal(dto.Sayaclar.Sum(s => s.Sayi), dto.Donemler.Sum(d => d.Toplam));
        }

        [Fact]
        public void Sorumlu_filtresi_saymadan_once_secenekler_sorumludan_once()
        {
            var dto = Kur(new FirmaPanosuFiltresi { Sorumlular = { Aybuke } });

            Assert.Equal(new[] { B }, dto.Donemler.SelectMany(d => d.Kartlar).Select(k => k.FirmaId).Distinct());
            Assert.Equal(1, Sayac(dto, PanoKovasi.Sonra));
            Assert.Equal(1, Sayac(dto, PanoKovasi.Tamam));

            // Seçenekler sorumlu filtresinden etkilenmez; "Sorumlu yok" sonda.
            Assert.Equal(new[] { "aybüke", "cengiz", "Sorumlu yok" }, dto.Sorumlular.Select(s => s.Ad));
            Assert.Equal(new[] { 2, 5, 1 }, dto.Sorumlular.Select(s => s.Sayi));
            Assert.Equal(ChecklistKartKurucu.SorumluYok, dto.Sorumlular[^1].Id);
        }

        [Fact]
        public void Sorumlu_yok_secenegi_sorumlusuz_maddeleri_getirir()
        {
            var dto = Kur(new FirmaPanosuFiltresi { Sorumlular = { ChecklistKartKurucu.SorumluYok } });

            Assert.Equal(new[] { C }, dto.Donemler.SelectMany(d => d.Kartlar).Select(k => k.FirmaId).Distinct());
        }

        [Fact]
        public void Isin_kendi_sorumlusu_firmanin_sorumlusunu_ezer()
        {
            var ozel = Ozel();
            ozel.Single(i => i.Id == Bankalar).SorumluKullaniciId = Aybuke;
            ozel.Single(i => i.Id == Bankalar).SorumluKullaniciAdi = "aybüke";

            var dto = Kur(new FirmaPanosuFiltresi { Sorumlular = { Aybuke } }, ozel: ozel);

            Assert.Contains(Maddeler(Kart(dto, A, "2026-09")), m => m.Satir.KaynakId == Bankalar);
            Assert.DoesNotContain(Maddeler(Kart(dto, A, "2026-09")), m => m.Satir.KaynakId == KdvEyl);
        }

        // ---- İş Takip Panosu ile tutarlılık ----

        [Fact]
        public void Kart_maddeleri_ayni_ayin_Is_Takip_Panosunda_ayni_durumla_vardir()
        {
            var firmalar = Firmalar();
            var tamamlar = Tamamlamalar();
            var dto = Kur(tamamlar: tamamlar);

            foreach (var donem in dto.Donemler)
            {
                var ay = IsDonemi.Coz(IsTekrari.Aylik, donem.Anahtar)!.Value;
                var pano = DonemPanosuKurucu.Kur(ay.Yil, ay.No, firmalar, Takvim(), Ozel(), tamamlar);

                foreach (var m in donem.Kartlar.SelectMany(Maddeler))
                {
                    var satir = Assert.Single(pano.Isler, i => i.FirmaId == m.Satir.FirmaId && i.KaynakTip == m.Satir.KaynakTip
                                                               && i.KaynakId == m.Satir.KaynakId
                                                               && i.DonemAnahtari == m.Satir.DonemAnahtari);
                    Assert.Equal(m.Satir.Tamamlandi, satir.Durum == DonemHucreDurumu.Yapildi);
                }
            }
        }
            // ---- Kategoriler (Aşama 2) ----

        private static string Cozulen(IsKaynagi tip, int kaynakId, string? kod, IsKategoriBaglami b)
            => IsKategoriCozucu.Coz(tip, kaynakId, kod, b).Ad;

        [Fact]
        public void Ozel_is_cozum_sirasi_kendi_sonra_tarif_sonra_diger()
        {
            Assert.Equal("Banka & Cari", Cozulen(IsKaynagi.Ozel, Bankalar, null, Baglam(ozelIsler: new() { [Bankalar] = (BankaCari, Bordro) })));
            Assert.Equal("Bordro", Cozulen(IsKaynagi.Ozel, Bankalar, null, Baglam(ozelIsler: new() { [Bankalar] = (null, Bordro) })));
            Assert.Equal("Diğer", Cozulen(IsKaynagi.Ozel, Bankalar, null, Baglam(ozelIsler: new() { [Bankalar] = (null, null) })));
            // Bağlamda hiç yoksa (ör. iş yeni eklendi) de Diğer.
            Assert.Equal("Diğer", Cozulen(IsKaynagi.Ozel, Bankalar, null, Baglam()));
            Assert.Null(IsKategoriCozucu.Coz(IsKaynagi.Ozel, Bankalar, null, Baglam()).KategoriId);
        }

        [Fact]
        public void Yasal_is_eslemeden_yoksa_beyanname()
        {
            var b = Baglam(mukellefiyetler: new() { ["0033"] = KayitKapanis });
            Assert.Equal("Kayıt & Kapanış", Cozulen(IsKaynagi.Yasal, GeciciQ3, "0033", b));
            Assert.Equal("Beyanname", Cozulen(IsKaynagi.Yasal, KdvEyl, "0015", b));

            // Yasal iş özel işin kategori alanlarına bakmaz (aynı Id'li özel iş olsa bile).
            var cakisan = Baglam(ozelIsler: new() { [KdvEyl] = (BankaCari, null) });
            Assert.Equal("Beyanname", Cozulen(IsKaynagi.Yasal, KdvEyl, "0015", cakisan));
        }

        [Fact]
        public void Yasal_varsayilan_adla_degil_kolonla_bulunur()
        {
            // Ad değişse de yasal iş yerinde kalır…
            var kategoriler = Kategoriler();
            kategoriler.Single(k => k.Id == Beyanname).Ad = "Vergi beyanları";
            Assert.Equal("Vergi beyanları", Cozulen(IsKaynagi.Yasal, KdvEyl, "0015", Baglam(kategoriler)));

            // …ve "Beyanname" adlı ama varsayılan olmayan kategori yasal işi çekmez.
            var tasinmis = Kategoriler();
            tasinmis.Single(k => k.Id == Beyanname).VarsayilanYasal = false;
            tasinmis.Single(k => k.Id == Bordro).VarsayilanYasal = true;
            Assert.Equal("Bordro", Cozulen(IsKaynagi.Yasal, KdvEyl, "0015", Baglam(tasinmis)));
        }

        [Fact]
        public void Pasif_kategori_atlanir_zincirde_sonrakine_gecilir()
        {
            var kategoriler = Kategoriler();
            kategoriler.Single(k => k.Id == BankaCari).Aktif = false;
            var b = Baglam(kategoriler, new() { ["0033"] = BankaCari }, new() { [Bankalar] = (BankaCari, Bordro), [TekSefer] = (BankaCari, null) });

            Assert.Equal("Bordro", Cozulen(IsKaynagi.Ozel, Bankalar, null, b));      // kendi pasif → tarif
            Assert.Equal("Diğer", Cozulen(IsKaynagi.Ozel, TekSefer, null, b));       // kendi pasif, tarif yok → Diğer
            Assert.Equal("Beyanname", Cozulen(IsKaynagi.Yasal, GeciciQ3, "0033", b)); // eşleme pasif → varsayılan

            // Beyanname de pasifse yasal iş Diğer'e düşer.
            kategoriler.Single(k => k.Id == Beyanname).Aktif = false;
            Assert.Equal("Diğer", Cozulen(IsKaynagi.Yasal, KdvEyl, "0015", Baglam(kategoriler)));
        }

        [Fact]
        public void Kart_bolumleri_siraya_gore_bos_bolum_yok_diger_en_sonda()
        {
            var b = Baglam(mukellefiyetler: new() { ["0033"] = Bordro }, ozelIsler: new() { [Bankalar] = (BankaCari, null) });
            var kart = Kart(Kur(kategoriler: b), A, "2026-09");

            // Kayıt & Kapanış'ta madde yok → bölüm yok.
            Assert.Equal(new[] { "Banka & Cari", "Bordro", "Beyanname" }, kart.Bolumler.Select(x => x.Ad));
            Assert.Equal(new[] { "Bankalar" }, kart.Bolumler[0].Maddeler.Select(m => m.Satir.Baslik));
            Assert.Equal(new[] { "Geçici vergi" }, kart.Bolumler[1].Maddeler.Select(m => m.Satir.Baslik));
            Assert.Equal(1, kart.Bolumler[0].Gecikmis);
            Assert.Equal(kart.Toplam, kart.Bolumler.Sum(x => x.Toplam));

            // Sıra değişince bölüm sırası da değişir; Diğer her zaman en sonda.
            var kategoriler = Kategoriler();
            kategoriler.Single(k => k.Id == Beyanname).Sira = 0;
            var kart2 = Kart(Kur(kategoriler: Baglam(kategoriler)), A, "2026-09");
            Assert.Equal(new[] { "Beyanname", "Diğer" }, kart2.Bolumler.Select(x => x.Ad));
        }
    }
}
