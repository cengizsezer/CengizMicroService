using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Katman sırası, çoklu token çıpası, aile ayrımı ve karar eşikleri. Eşikler gevşetilmez:
    /// düşük skorda "en yakın kod" otomatik yazılmaz, yakın ikinci aday varsa onaya düşer.
    /// </summary>
    public class HesapEslestiriciTests
    {
        private readonly HesapEslestirici _eslestirici = new();

        private static HesapPlaniKaydi Plan(string kod, string ad) => new()
        {
            Kod = kod,
            Ad = ad,
            NormalizeAd = Normalizasyon.UnvanNormalize(ad),
            AnaGrup = Normalizasyon.AnaGrup(kod),
            BaslangicHarfi = Normalizasyon.BaslangicHarfi(kod),
            Aktif = true
        };

        private static SatirBaglami Baglam(
            string islemTipi = "Gelen EFT Otomatik Yatan",
            string hamAciklama = "",
            string? unvan = null,
            Yon yon = Yon.Giren,
            string? iban = null,
            string? vkn = null,
            AciklamaSablonu? sablon = null) => new()
            {
                IslemTipi = islemTipi,
                HamAciklama = hamAciklama,
                Unvan = unvan,
                Yon = yon,
                KarsiIban = iban,
                KarsiVkn = vkn,
                Sablon = sablon
            };

        private static HesapEslesmesi Eslesme(string cekirdek, string kod, string? ad = null,
                                              Yon yon = Yon.Giren, string? ek = null,
                                              AnahtarTipi tip = AnahtarTipi.UnvanCekirdek) => new()
        {
            AnahtarTipi = tip,
            AnahtarCekirdek = cekirdek,
            AyirtEdiciEk = ek,
            Yon = yon,
            HesapKodu = kod,
            HesapAdi = ad
        };

        // ---- Kapalı katmanlar ----

        [Fact]
        public void Vkn_katmani_varsayilan_kapali()
        {
            // Vakıfbank'ta VKN kolonu hesap sahibinin VKN'si; açık kalsaydı ilk onaydan
            // sonra tüm satırlar güven 1.0 ile aynı hesaba eşleşir, onaya bile düşmezdi.
            var veri = new EslestirmeVerisi
            {
                Eslesmeler = new[] { Eslesme("0070511435", "120 D22", tip: AnahtarTipi.Vkn) }
            };

            var sonuc = _eslestirici.Coz(Baglam(vkn: "0070511435"), veri);

            Assert.NotEqual(KaynakKatman.Vkn, sonuc.Katman);
            Assert.Null(sonuc.HesapKodu);
        }

        [Fact]
        public void Vkn_katmani_bayrak_acilinca_calisir()
        {
            var veri = new EslestirmeVerisi
            {
                Eslesmeler = new[] { Eslesme("0070511435", "120 D22", tip: AnahtarTipi.Vkn) },
                VknKatmaniAktif = true
            };

            var sonuc = _eslestirici.Coz(Baglam(vkn: "0070511435"), veri);

            Assert.Equal(KaynakKatman.Vkn, sonuc.Katman);
            Assert.Equal("120 D22", sonuc.HesapKodu);
        }

        [Fact]
        public void Iban_katmani_varsayilan_kapali()
        {
            var veri = new EslestirmeVerisi
            {
                Eslesmeler = new[]
                {
                    Eslesme("330006200012300006673953", "120 X99", tip: AnahtarTipi.Iban),
                    Eslesme("DAGI GIYIM", "120 D22", "Dagi Giyim")
                }
            };

            var sonuc = _eslestirici.Coz(
                Baglam(unvan: "DAĞI GİYİM", iban: "TR330006200012300006673953"), veri);

            // IBAN kapalı olduğu için geçmiş onay (unvan çekirdeği) kazanır.
            Assert.Equal(KaynakKatman.GecmisOnay, sonuc.Katman);
            Assert.Equal("120 D22", sonuc.HesapKodu);
        }

        // ---- Katman 1: geçmiş onay ----

        [Fact]
        public void Gecmis_onay_unvan_cekirdeginden_cozer()
        {
            var veri = new EslestirmeVerisi
            {
                Eslesmeler = new[] { Eslesme("KEMAL TEKSTIL", "120 K08", "Kemal Tekstil") }
            };

            // Farklı sorgu numarası, aynı cari: çekirdek aynı kaldığı için eşleşir.
            var sonuc = _eslestirici.Coz(
                Baglam(hamAciklama: "0000999 sorgu numaralı", unvan: "KEMAL TEKSTİL SAN. VE TİC. A.Ş."), veri);

            Assert.Equal(KaynakKatman.GecmisOnay, sonuc.Katman);
            Assert.Equal("120 K08", sonuc.HesapKodu);
            Assert.Equal(1.0m, sonuc.Guven);
        }

        [Fact]
        public void Ogrenme_kaydi_yon_bazlidir()
        {
            var veri = new EslestirmeVerisi
            {
                Eslesmeler = new[] { Eslesme("KEMAL TEKSTIL", "120 K08") }
            };

            var sonuc = _eslestirici.Coz(Baglam(unvan: "KEMAL TEKSTİL", yon: Yon.Cikan), veri);

            Assert.NotEqual(KaynakKatman.GecmisOnay, sonuc.Katman);
        }

        [Fact]
        public void Genisletilmis_anahtar_sade_cekirdekten_once_denenir()
        {
            var veri = new EslestirmeVerisi
            {
                Eslesmeler = new[]
                {
                    Eslesme("PARK PLAZA", "329 P99", "Park Plaza Genel", Yon.Cikan),
                    Eslesme("PARK PLAZA", "329 P04", "Park Plaza Aidat", Yon.Cikan, ek: "AIDAT"),
                    Eslesme("PARK PLAZA", "329 P05", "Park Plaza Elektrik", Yon.Cikan, ek: "ELEKTRIK")
                }
            };

            var sonuc = _eslestirici.Coz(
                Baglam(hamAciklama: "PARK PLAZA AİDAT ÖDEMESİ", unvan: "PARK PLAZA", yon: Yon.Cikan), veri);

            Assert.Equal("329 P04", sonuc.HesapKodu);
            Assert.Equal("AIDAT", sonuc.AyirtEdiciEk);
        }

        [Fact]
        public void Ogrenilmis_ailenin_iki_uyesi_metinde_geciyorsa_onaya_duser()
        {
            var veri = new EslestirmeVerisi
            {
                Eslesmeler = new[]
                {
                    Eslesme("PARK PLAZA", "329 P04", "Park Plaza Aidat", Yon.Cikan, ek: "AIDAT"),
                    Eslesme("PARK PLAZA", "329 P05", "Park Plaza Elektrik", Yon.Cikan, ek: "ELEKTRIK")
                }
            };

            var sonuc = _eslestirici.Coz(
                Baglam(hamAciklama: "PARK PLAZA AİDAT VE ELEKTRİK", unvan: "PARK PLAZA", yon: Yon.Cikan), veri);

            Assert.Equal(SatirDurum.OnayBekliyor, sonuc.Durum);
            Assert.Equal(2, sonuc.Adaylar.Count);
        }

        [Fact]
        public void Unvansiz_satir_islem_tipi_anahtariyla_cozulur()
        {
            var veri = new EslestirmeVerisi
            {
                Eslesmeler = new[] { Eslesme("ISLEM:MKK MASRAFI", "770 01", "Banka Gideri", Yon.Cikan) }
            };

            var sonuc = _eslestirici.Coz(Baglam("MKK Masrafı", hamAciklama: "MKK ücreti", yon: Yon.Cikan), veri);

            Assert.Equal(KaynakKatman.GecmisOnay, sonuc.Katman);
            Assert.Equal("770 01", sonuc.HesapKodu);
        }

        // ---- Katman 2: banka kayıt defteri ----

        [Fact]
        public void Banka_kayit_defteri_metinden_bankayi_bulur()
        {
            var sablon = new AciklamaSablonu { BankalarArasi = true, Sablon = "Hesaplararası Virman - {HESAP}" };
            var veri = new EslestirmeVerisi
            {
                BankaHesaplari = new[]
                {
                    new BankaHesabi { Id = 1, BankaAdi = "Vakıfbank", OrkaHesapKodu = "102 1 1 01", Aktif = true },
                    new BankaHesabi { Id = 2, BankaAdi = "Akbank", OrkaHesapKodu = "102 2 1 01", Aktif = true }
                },
                IslenenBankaHesabiId = 1
            };

            var sonuc = _eslestirici.Coz(
                Baglam("Virman", hamAciklama: "AKBANK hesabına virman", sablon: sablon), veri);

            Assert.Equal(KaynakKatman.BankaKayitDefteri, sonuc.Katman);
            Assert.Equal("102 2 1 01", sonuc.HesapKodu);
            Assert.Equal(0.95m, sonuc.Guven);
        }

        [Fact]
        public void Banka_kayit_defteri_islenen_hesabin_kendisini_secmez()
        {
            var sablon = new AciklamaSablonu { BankalarArasi = true };
            var veri = new EslestirmeVerisi
            {
                BankaHesaplari = new[]
                {
                    new BankaHesabi { Id = 1, BankaAdi = "Vakıfbank", OrkaHesapKodu = "102 1 1 01", Aktif = true }
                },
                IslenenBankaHesabiId = 1
            };

            var sonuc = _eslestirici.Coz(
                Baglam("Virman", hamAciklama: "VAKIFBANK içi virman", sablon: sablon), veri);

            Assert.NotEqual(KaynakKatman.BankaKayitDefteri, sonuc.Katman);
        }

        // ---- Katman 2: eşleştirme anahtarları ----

        /// <summary>Aynı bankanın iki hesabı; yalnız süpürme hesabında anahtar var.</summary>
        private static EslestirmeVerisi VakifbankIkiHesap(string? supurmeAnahtari = "Otomatik Süpürme, Süpürme",
                                                          string? vadesizAnahtari = null) => new()
        {
            BankaHesaplari = new[]
            {
                new BankaHesabi
                {
                    Id = 1, BankaAdi = "Vakıfbank", HesapAdi = "Vakıfbank, Vadesiz Tl",
                    OrkaHesapKodu = "102 1 1 01", EslestirmeAnahtarlari = vadesizAnahtari, Aktif = true
                },
                new BankaHesabi
                {
                    Id = 2, BankaAdi = "Vakıfbank", HesapAdi = "Vakıfbank, Vadeli Tl - Otomatik Süpürme Hesabı",
                    OrkaHesapKodu = "102 1 1 04", EslestirmeAnahtarlari = supurmeAnahtari, Aktif = true
                }
            },
            IslenenBankaHesabiId = 9
        };

        [Fact]
        public void Anahtar_ayni_bankanin_dogru_hesabini_secer()
        {
            // Açıklamada banka adı hiç geçmiyor; ayırt eden tek şey anahtar.
            var sablon = new AciklamaSablonu { BankalarArasi = true, Sablon = "Otomatik Süpürme Pkf Aday" };

            var sonuc = _eslestirici.Coz(
                Baglam("Otomatik Süpürme İşlemleri Virman", hamAciklama: "Otomatik Süpürme Pkf Aday", sablon: sablon),
                VakifbankIkiHesap());

            Assert.Equal(KaynakKatman.BankaKayitDefteri, sonuc.Katman);
            Assert.Equal("102 1 1 04", sonuc.HesapKodu);
            Assert.Equal(SatirDurum.Otomatik, sonuc.Durum);
        }

        [Fact]
        public void Anahtar_tutmayinca_ayni_bankada_iki_hesap_varsa_onaya_duser()
        {
            var sablon = new AciklamaSablonu { BankalarArasi = true, Sablon = "Hesaplararası Virman - {HESAP}" };

            var sonuc = _eslestirici.Coz(
                Baglam("Virman", hamAciklama: "Hesaplar Arası Eft - Vakıfbank", sablon: sablon),
                VakifbankIkiHesap());

            Assert.Equal(SatirDurum.OnayBekliyor, sonuc.Durum);
            Assert.Equal(KaynakKatman.BankaKayitDefteri, sonuc.Katman);

            // Kod önerilmez: "ilk bulunanı" seçmek yanlış banka hesabına kayıt atmak olurdu.
            Assert.Null(sonuc.HesapKodu);
            Assert.Equal(2, sonuc.Adaylar.Count);
            Assert.Contains(sonuc.Adaylar, a => a.Kod == "102 1 1 01");
            Assert.Contains(sonuc.Adaylar, a => a.Kod == "102 1 1 04");
        }

        [Fact]
        public void Tek_hesapli_bankada_anahtar_olmadan_banka_adindan_cozulur()
        {
            var sablon = new AciklamaSablonu { BankalarArasi = true, Sablon = "Hesaplararası Virman - {HESAP}" };
            var veri = new EslestirmeVerisi
            {
                BankaHesaplari = new[]
                {
                    new BankaHesabi { Id = 1, BankaAdi = "Fibabanka", OrkaHesapKodu = "102 1 9 01", Aktif = true },
                    new BankaHesabi { Id = 2, BankaAdi = "Vakıfbank", OrkaHesapKodu = "102 1 1 01", Aktif = true }
                },
                IslenenBankaHesabiId = 9
            };

            var sonuc = _eslestirici.Coz(
                Baglam("Virman", hamAciklama: "Hesaplararası Virman - Fibabanka", sablon: sablon), veri);

            Assert.Equal(KaynakKatman.BankaKayitDefteri, sonuc.Katman);
            Assert.Equal("102 1 9 01", sonuc.HesapKodu);
            Assert.Equal(SatirDurum.Otomatik, sonuc.Durum);
        }

        [Fact]
        public void En_uzun_anahtar_kazanir()
        {
            // Açıklamada hem "Vakıfbank" hem "Otomatik Süpürme" geçiyor; uzun olan seçilir.
            var sablon = new AciklamaSablonu { BankalarArasi = true };

            var sonuc = _eslestirici.Coz(
                Baglam("Virman", hamAciklama: "Vakıfbank Otomatik Süpürme", sablon: sablon),
                VakifbankIkiHesap(vadesizAnahtari: "Vakıfbank"));

            Assert.Equal("102 1 1 04", sonuc.HesapKodu);
            Assert.Equal(SatirDurum.Otomatik, sonuc.Durum);
        }

        [Fact]
        public void Anahtar_eslesmesi_turkce_karakter_ve_buyuk_kucuk_harf_duyarsiz()
        {
            var sablon = new AciklamaSablonu { BankalarArasi = true };

            var sonuc = _eslestirici.Coz(
                Baglam("Virman", hamAciklama: "otomatık supurme pkf aday", sablon: sablon),
                VakifbankIkiHesap());

            Assert.Equal("102 1 1 04", sonuc.HesapKodu);
        }

        [Fact]
        public void Anahtar_kelime_ortasinda_eslesmez()
        {
            // "TEB" anahtarı "OTEBANK" içinde geçse de eşleşmemeli.
            var sablon = new AciklamaSablonu { BankalarArasi = true };
            var veri = new EslestirmeVerisi
            {
                BankaHesaplari = new[]
                {
                    new BankaHesabi { Id = 1, BankaAdi = "TEB", OrkaHesapKodu = "102 1 32 87", Aktif = true }
                },
                IslenenBankaHesabiId = 9
            };

            var sonuc = _eslestirici.Coz(
                Baglam("Virman", hamAciklama: "OTEBANK hesabına virman", sablon: sablon), veri);

            Assert.NotEqual(KaynakKatman.BankaKayitDefteri, sonuc.Katman);
        }

        [Fact]
        public void Kodu_olmayan_aday_elenince_belirsizlik_kalmaz()
        {
            var sablon = new AciklamaSablonu { BankalarArasi = true };
            var veri = new EslestirmeVerisi
            {
                BankaHesaplari = new[]
                {
                    new BankaHesabi { Id = 1, BankaAdi = "Vakıfbank", OrkaHesapKodu = "102 1 1 01", Aktif = true },
                    new BankaHesabi { Id = 2, BankaAdi = "Vakıfbank", OrkaHesapKodu = string.Empty, Aktif = true }
                },
                IslenenBankaHesabiId = 9
            };

            var sonuc = _eslestirici.Coz(
                Baglam("Virman", hamAciklama: "Vakıfbank hesabına virman", sablon: sablon), veri);

            Assert.Equal("102 1 1 01", sonuc.HesapKodu);
            Assert.Equal(SatirDurum.Otomatik, sonuc.Durum);
        }

        [Fact]
        public void Belirsiz_bankada_aciklama_yine_banka_adini_kullanir()
        {
            // Onaya düşen satırda bile açıklama üretilebilmeli: iki adayın da bankası aynı.
            var sablon = new AciklamaSablonu { BankalarArasi = true };

            var banka = _eslestirici.BankaBul(
                Baglam("Virman", hamAciklama: "Hesaplar Arası Eft - Vakıfbank", sablon: sablon),
                VakifbankIkiHesap());

            Assert.Equal("Vakıfbank", banka?.BankaAdi);
        }

        // ---- Katman 3: sabit kural ----

        [Fact]
        public void Sabit_kural_islem_tipinden_cozer()
        {
            var veri = new EslestirmeVerisi
            {
                SabitKurallar = new[]
                {
                    new SabitKural { IslemTipiDeseni = "MKK Masrafı", EslesmeTuru = EslesmeTuru.Tam,
                                     HesapKodu = "770", HesapAdi = "Genel Yönetim Giderleri", Guven = 0.95m, Aktif = true }
                }
            };

            var sonuc = _eslestirici.Coz(Baglam("MKK Masrafı", yon: Yon.Cikan), veri);

            Assert.Equal(KaynakKatman.SabitKural, sonuc.Katman);
            Assert.Equal("770", sonuc.HesapKodu);
            Assert.Equal(SatirDurum.Otomatik, sonuc.Durum);
        }

        // ---- Yön → ana grup ----

        [Fact]
        public void Yon_ana_grubu_belirler()
        {
            var veri = new EslestirmeVerisi
            {
                HesapPlani = new[] { Plan("120 D22", "DAĞI GİYİM"), Plan("329 D22", "DAĞI GİYİM") }
            };

            var giren = _eslestirici.Coz(Baglam(unvan: "DAĞI GİYİM", yon: Yon.Giren), veri);
            var cikan = _eslestirici.Coz(Baglam(unvan: "DAĞI GİYİM", yon: Yon.Cikan), veri);

            Assert.Equal("120 D22", giren.HesapKodu);
            Assert.Equal("329 D22", cikan.HesapKodu);
        }

        // ---- Katman 4: çoklu token çıpası ----

        [Fact]
        public void Coklu_token_cipasi_banka_ic_kodunu_atlar()
        {
            // Banka unvanın önüne kendi iç kodunu ekleyebiliyor; ilk kelime çıpa olarak
            // hiç aday getirmiyor, ikinci token doğru cariyi buluyor.
            var veri = new EslestirmeVerisi
            {
                HesapPlani = new[]
                {
                    Plan("120 N15", "NAOS İSTANBUL KOZMETİK"),
                    Plan("120 N16", "NAOS PAZARLAMA DANIŞMANLIK")
                }
            };

            var sonuc = _eslestirici.Coz(
                Baglam(unvan: "NAOSKZ NAOS İSTANBUL KOZMETİK SANAYİ VE TİCARET A.Ş."), veri);

            Assert.Equal("120 N15", sonuc.HesapKodu);
            Assert.Equal(SatirDurum.Otomatik, sonuc.Durum);
            Assert.Equal(KaynakKatman.UnvanBenzerligi, sonuc.Katman);
        }

        [Fact]
        public void Kalabalik_cipa_elenmez_pkf_ailesi_dogru_cariye_gider()
        {
            // Gerçek hesap planında "PKF" çıpası 89 hesap getiriyor (grup şirketleri).
            // Aday sayısına eşik konduğunda bu satırın skoru 0.95'ten 0.48'e düşüp
            // alakasız bir ana hesaba (373) eşleşiyordu.
            var veri = new EslestirmeVerisi { HesapPlani = KalabalikPlan() };

            var sonuc = _eslestirici.Coz(Baglam(unvan: "PKF İSTANBUL YEMİNLİ MALİ MÜŞAVİRLİK A.Ş."), veri);

            Assert.Equal("120 P44", sonuc.HesapKodu);
            Assert.True(sonuc.Guven > 0.90m, $"Skor 0.90 üzerinde olmalı, ölçülen: {sonuc.Guven}");
            Assert.Equal(KaynakKatman.UnvanBenzerligi, sonuc.Katman);
        }

        [Fact]
        public void Kalabalik_cipa_elenmez_istanbul_portfoy_dogru_cariye_gider()
        {
            // "ISTANBUL" çıpası 126 hesap getiriyor. Eşik uygulandığında skor 1.00'dan
            // 0.61'e düşüp alakasız hesaplara (110, 121 1) eşleşiyordu.
            var veri = new EslestirmeVerisi { HesapPlani = KalabalikPlan() };

            var sonuc = _eslestirici.Coz(Baglam(unvan: "İSTANBUL PORTFÖY YÖNETİMİ A.Ş."), veri);

            Assert.Equal("120 I61", sonuc.HesapKodu);
            Assert.True(sonuc.Guven > 0.90m, $"Skor 0.90 üzerinde olmalı, ölçülen: {sonuc.Guven}");
            Assert.Equal(KaynakKatman.UnvanBenzerligi, sonuc.Katman);
        }

        /// <summary>
        /// Ölçülen hesap planının kalabalık çıpalarını taklit eder: PKF 89 grup şirketi,
        /// PARDUS 101 portföy fonu, İSTANBUL 126 hesap. Kalabalık çıpalar gürültü değil,
        /// meşru cari aileleri — hedef cariler bu kalabalığın içinde duruyor.
        /// </summary>
        private static List<HesapPlaniKaydi> KalabalikPlan()
        {
            var plan = new List<HesapPlaniKaydi>
            {
                Plan("120 P44", "PKF İSTANBUL YEMİNLİ MALİ MÜŞAVİRLİK A.Ş."),
                Plan("120 I61", "İSTANBUL PORTFÖY YÖNETİMİ A.Ş."),
                // Eşik uygulandığında satırların kaçtığı alakasız hesaplar.
                Plan("120 X01", "MÜŞAVİRLİK DANIŞMANLIK"),
                Plan("120 X02", "YÖNETİM ORGANİZASYON")
            };

            for (var i = 1; i <= 88; i++)
                plan.Add(Plan($"120 P{i:00}A", $"PKF GRUP ORTAKLIĞI {i:000}"));

            for (var i = 1; i <= 100; i++)
                plan.Add(Plan($"120 D{i:000}", $"PARDUS SERBEST FON {i:000}"));

            for (var i = 1; i <= 125; i++)
                plan.Add(Plan($"120 I{i:000}A", $"İSTANBUL GAYRİMENKUL YATIRIM {i:000}"));

            return plan;
        }

        [Fact]
        public void Hicbir_cipa_tutmazsa_kod_onerilmez()
        {
            // Eskiden tüm grup taranıp "en yakın" kod öneriliyordu; alakasız hesap
            // önermektense satır onay kuyruğuna düşsün.
            var veri = new EslestirmeVerisi { HesapPlani = new[] { Plan("120 M01", "MERT İNŞAAT") } };

            var sonuc = _eslestirici.Coz(Baglam(unvan: "MELTEM ORGANİZASYON REKLAM"), veri);

            Assert.Equal(SatirDurum.Cozulemedi, sonuc.Durum);
            Assert.Null(sonuc.HesapKodu);
        }

        // ---- Karar eşikleri ve aile ----

        [Fact]
        public void Tek_yuksek_aday_otomatik_gecer()
        {
            var veri = new EslestirmeVerisi
            {
                HesapPlani = new[] { Plan("120 D22", "DAĞI GİYİM SANAYİ"), Plan("120 Z01", "ZETA MADENCİLİK") }
            };

            var sonuc = _eslestirici.Coz(Baglam(unvan: "DAĞI GİYİM SANAYİ"), veri);

            Assert.Equal(SatirDurum.Otomatik, sonuc.Durum);
            Assert.Equal("120 D22", sonuc.HesapKodu);
            Assert.Equal(KaynakKatman.UnvanBenzerligi, sonuc.Katman);
            Assert.Null(sonuc.IkinciAdayKodu);
            // Tek aday: anahtar sade çekirdek kalır, gereksiz kelime eklenmez.
            Assert.Null(sonuc.AyirtEdiciEk);
        }

        [Fact]
        public void Park_plaza_ailesi_onaya_duser_ve_tum_uyeler_listelenir()
        {
            var veri = new EslestirmeVerisi
            {
                HesapPlani = new[]
                {
                    Plan("329 P04", "PARK PLAZA YÖNETİMİ AİDAT"),
                    Plan("329 P05", "PARK PLAZA YÖNETİMİ ELEKTRİK"),
                    Plan("329 P27", "PARK PLAZA YÖNETİMİ 19 KAT")
                }
            };

            var sonuc = _eslestirici.Coz(
                Baglam(hamAciklama: "PARK PLAZA ödemesi", unvan: "PARK PLAZA YÖNETİMİ", yon: Yon.Cikan), veri);

            Assert.Equal(SatirDurum.OnayBekliyor, sonuc.Durum);
            Assert.Equal(3, sonuc.Adaylar.Count);
            Assert.Contains(sonuc.Adaylar, a => a.Kod == "329 P04");
            Assert.Contains(sonuc.Adaylar, a => a.Kod == "329 P05");
            Assert.Contains(sonuc.Adaylar, a => a.Kod == "329 P27");
            Assert.NotNull(sonuc.IkinciAdayKodu);
        }

        [Fact]
        public void Aile_ayirt_edici_kelime_metinde_geciyorsa_cozulur()
        {
            var veri = new EslestirmeVerisi
            {
                HesapPlani = new[]
                {
                    Plan("329 P04", "PARK PLAZA YÖNETİMİ AİDAT"),
                    Plan("329 P05", "PARK PLAZA YÖNETİMİ ELEKTRİK")
                }
            };

            var sonuc = _eslestirici.Coz(
                Baglam(hamAciklama: "PARK PLAZA ELEKTRİK FATURASI", unvan: "PARK PLAZA YÖNETİMİ", yon: Yon.Cikan), veri);

            Assert.Equal("329 P05", sonuc.HesapKodu);
            Assert.Equal("ELEKTRIK", sonuc.AyirtEdiciEk);
        }

        [Fact]
        public void Unvan_yoksa_cozulemedi_olur()
        {
            var veri = new EslestirmeVerisi { HesapPlani = new[] { Plan("120 D22", "DAĞI GİYİM") } };

            var sonuc = _eslestirici.Coz(Baglam(unvan: null), veri);

            Assert.Equal(SatirDurum.Cozulemedi, sonuc.Durum);
            Assert.Equal(KaynakKatman.Yok, sonuc.Katman);
            Assert.Null(sonuc.HesapKodu);
        }

        [Fact]
        public void Hesap_plani_bossa_cozulemedi_olur()
        {
            var sonuc = _eslestirici.Coz(Baglam(unvan: "DAĞI GİYİM"), new EslestirmeVerisi());

            Assert.Equal(SatirDurum.Cozulemedi, sonuc.Durum);
        }

        [Fact]
        public void Onek_kurali_kisaltilmis_muavin_adini_yakalar()
        {
            // ORKA muavin adları kesik yazılabiliyor; ilk 14 karakter tutuyorsa skor 0.95.
            var oran = Benzerlik.Oran("ULUSLARARASI TASIMACILIK LOJISTIK", "ULUSLARARASI TASIMACILIK");

            Assert.True(oran >= Benzerlik.OnekSkoru);
        }
    }
}
