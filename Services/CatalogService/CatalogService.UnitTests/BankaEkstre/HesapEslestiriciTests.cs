using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Katman sırası, yön → ana grup kuralı ve karar eşikleri. Eşikler gevşetilmez:
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

        // ---- Katman sırası ----

        [Fact]
        public void Katman1_iban_gecmis_onaydan_once_gelir()
        {
            var veri = new EslestirmeVerisi
            {
                OgrenmeKayitlari = new[]
                {
                    new OgrenmeKaydi { AnahtarTipi = AnahtarTipi.Iban, Anahtar = "330006200012300006673953",
                                       Yon = Yon.Giren, HesapKodu = "120 D22", HesapAdi = "Dagi Giyim" },
                    new OgrenmeKaydi { AnahtarTipi = AnahtarTipi.AciklamaHash,
                                       Anahtar = Normalizasyon.AciklamaHash("DAGI GIYIM tarafından"),
                                       Yon = Yon.Giren, HesapKodu = "120 X99", HesapAdi = "Yanlış" }
                }
            };

            var sonuc = _eslestirici.Coz(
                Baglam(hamAciklama: "DAGI GIYIM tarafından", iban: "TR330006200012300006673953"), veri);

            Assert.Equal(KaynakKatman.Iban, sonuc.Katman);
            Assert.Equal("120 D22", sonuc.HesapKodu);
            Assert.Equal(1.0m, sonuc.Guven);
            Assert.Equal(SatirDurum.Otomatik, sonuc.Durum);
        }

        [Fact]
        public void Katman2_gecmis_onay_hash_ile_cozer()
        {
            var aciklama = "0000123 sorgu numaralı KEMAL TEKSTIL tarafından";
            var veri = new EslestirmeVerisi
            {
                OgrenmeKayitlari = new[]
                {
                    new OgrenmeKaydi { AnahtarTipi = AnahtarTipi.AciklamaHash,
                                       Anahtar = Normalizasyon.AciklamaHash(aciklama),
                                       Yon = Yon.Giren, HesapKodu = "120 K08", HesapAdi = "Kemal Tekstil" }
                }
            };

            var sonuc = _eslestirici.Coz(Baglam(hamAciklama: aciklama), veri);

            Assert.Equal(KaynakKatman.GecmisOnay, sonuc.Katman);
            Assert.Equal("120 K08", sonuc.HesapKodu);
        }

        [Fact]
        public void Ogrenme_kaydi_yon_bazlidir()
        {
            var aciklama = "KEMAL TEKSTIL tarafından";
            var veri = new EslestirmeVerisi
            {
                OgrenmeKayitlari = new[]
                {
                    new OgrenmeKaydi { AnahtarTipi = AnahtarTipi.AciklamaHash,
                                       Anahtar = Normalizasyon.AciklamaHash(aciklama),
                                       Yon = Yon.Giren, HesapKodu = "120 K08" }
                }
            };

            // Aynı anahtar, ters yön: öğrenilmiş kayıt kullanılmaz.
            var sonuc = _eslestirici.Coz(Baglam(hamAciklama: aciklama, yon: Yon.Cikan), veri);

            Assert.NotEqual(KaynakKatman.GecmisOnay, sonuc.Katman);
        }

        [Fact]
        public void Katman3_banka_kayit_defteri_metinden_bankayi_bulur()
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
        public void Katman3_islenen_hesabin_kendisini_secmez()
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

        [Fact]
        public void Katman4_sabit_kural_islem_tipinden_cozer()
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

        // ---- Karar eşikleri ----

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
        }

        [Fact]
        public void Yakin_ikinci_aday_varsa_onaya_duser_ve_iki_aday_gosterilir()
        {
            // Ölçümdeki iki hatanın tipi: aynı unvan ailesinden birden fazla cari.
            var veri = new EslestirmeVerisi
            {
                HesapPlani = new[]
                {
                    Plan("120 P17", "PKF İSTANBUL YEMİNLİ MALİ MÜŞAVİRLİK BİR"),
                    Plan("120 P16", "PKF İSTANBUL YEMİNLİ MALİ MÜŞAVİRLİK İKİ")
                }
            };

            var sonuc = _eslestirici.Coz(Baglam(unvan: "PKF İSTANBUL YEMİNLİ MALİ MÜŞAVİRLİK"), veri);

            Assert.Equal(SatirDurum.OnayBekliyor, sonuc.Durum);
            Assert.NotNull(sonuc.IkinciAdayKodu);
            Assert.True(sonuc.Guven >= HesapEslestirici.OtomatikEsik);
            Assert.True(sonuc.Guven - sonuc.IkinciAdaySkoru!.Value < HesapEslestirici.AdayFarki);
        }

        [Fact]
        public void Esik_altindaki_skor_otomatik_gecmez()
        {
            var veri = new EslestirmeVerisi
            {
                HesapPlani = new[] { Plan("120 M01", "MERT İNŞAAT") }
            };

            var sonuc = _eslestirici.Coz(Baglam(unvan: "MELTEM ORGANİZASYON REKLAM"), veri);

            Assert.Equal(SatirDurum.OnayBekliyor, sonuc.Durum);
            Assert.True(sonuc.Guven < HesapEslestirici.OtomatikEsik);
            // Öneri yine de gösterilir; ama otomatik yazılmaz.
            Assert.Equal("120 M01", sonuc.HesapKodu);
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
        public void Ilk_harf_daraltmasi_bos_kalirsa_tum_gruba_genisler()
        {
            // Unvan "Z" ile başlıyor ama planda Z ile başlayan kod yok; arama tüm gruba açılır.
            var veri = new EslestirmeVerisi { HesapPlani = new[] { Plan("120 D22", "ZETA MADENCİLİK") } };

            var sonuc = _eslestirici.Coz(Baglam(unvan: "ZETA MADENCİLİK"), veri);

            Assert.Equal("120 D22", sonuc.HesapKodu);
            Assert.Equal(SatirDurum.Otomatik, sonuc.Durum);
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
