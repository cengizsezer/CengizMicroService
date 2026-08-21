using CatalogService.Api.Features.BankaEkstre.Services;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Ölçülen desenlerin her biri için en az bir örnek. Desenler sırayla denenir,
    /// ilk yakalayan kazanır; bu yüzden örnekler yalnız hedef deseni tetikleyecek şekilde seçildi.
    ///
    /// İkinci blok hesap sahibinin kendi unvanının elenmesini doğrular: gerçek dosyada
    /// 287 satırın 268'inde firmanın kendi adı geçiyordu ve karşı taraf sanılıyordu.
    /// </summary>
    public class UnvanCikariciTests
    {
        private readonly UnvanCikarici _cikarici = new();
        private static readonly List<Api.Features.BankaEkstre.Domain.UnvanDeseni> Desenler =
            BankaEkstreTestOrtami.Desenler();

        /// <summary>Ölçülen dosyadaki hesap sahibi unvanı.</summary>
        private const string HesapSahibi = "PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ";

        [Fact]
        public void Desen1_sorgu_numarali_tarafindan()
        {
            var unvan = _cikarici.Cikar(
                "0000123 sorgu numaralı DAGI GIYIM SANAYI VE TICARET A.S. tarafından gönderilmiştir",
                Desenler).Unvan;

            Assert.Equal("DAGI GIYIM SANAYI VE TICARET A.S.", unvan);
        }

        [Fact]
        public void Desen2_nolu_hesab()
        {
            var unvan = _cikarici.Cikar(
                "TR330006200012300006673953 nolu KEMAL TEKSTIL LIMITED hesabına havale",
                Desenler).Unvan;

            Assert.Equal("KEMAL TEKSTIL LIMITED", unvan);
        }

        [Theory]
        // Banka adı değişken; ortak çıpa "A.Ş." kuyruğu. Her biri gerçek dosyadan.
        [InlineData("Türkiye Garanti Bankası A.Ş. İlyas Ömeroğlu", "İlyas Ömeroğlu")]
        [InlineData("Akbank T.A.Ş. ARAS KARGO YURTİÇİ YURTDIŞI TAŞIMACILIK A.Ş", "ARAS KARGO YURTİÇİ YURTDIŞI TAŞIMACILIK A.Ş")]
        [InlineData("Türk Ekonomi Bankası A.Ş. PARK PLAZA YÖNETİMİ", "PARK PLAZA YÖNETİMİ")]
        [InlineData("Türkiye İş Bankası A.Ş. Kübranur Karasürmeli", "Kübranur Karasürmeli")]
        [InlineData("Denizbank A.Ş. YURTİÇİ KARGO A.Ş.", "YURTİÇİ KARGO A.Ş.")]
        [InlineData("Albaraka Türk Katılım Bankası A.Ş. AVANSAS OFİS MALZEMELERİ TİC.A.Ş", "AVANSAS OFİS MALZEMELERİ TİC.A.Ş")]
        public void Giden_fast_karsi_tarafi_banka_adindan_sonra_okunur(string bankaVeUnvan, string beklenen)
        {
            // Karşı tarafın kendi "A.Ş." eki unvanın içinde kalmalı: tembel eşleşme ilk
            // (gönderen bankanın) "A.Ş."sini tutar.
            var unvan = _cikarici.Cikar(
                $"masraf ödemesi (23/07/2026 tarihli 2801304349 sorgu no'lu {HesapSahibi} " +
                $"hesabından {bankaVeUnvan} hesabına giden FAST ödemesi)",
                Desenler,
                HesapSahibi).Unvan;

            Assert.Equal(beklenen, unvan);
        }

        [Fact]
        public void Gelen_fast_satirinda_giden_deseni_tutmaz()
        {
            // Gelen satırda "hesabından" karşı tarafın adından SONRA geliyor; desen tutsaydı
            // hesap sahibinin kendi adını karşı taraf sanardı. Desen doğrudan sınanır:
            // bu satırların kalan davranışı (Desen 3'ün gövdeyi yakalaması) ayrı bir defekt.
            var eslesme = System.Text.RegularExpressions.Regex.Match(
                "MARBAŞ ÖDEME (24.07.2026 tarihli 8107395 sorgu no'lu Akbank T.A.Ş. " +
                "MARBAŞ MENKUL DEĞERLER ANONİM ŞTİ. hesabından PKF ADAY BAĞIMSIZ DENETİM A.Ş " +
                "hesabına gelen FAST ödemesi)",
                BankaEkstreTestOrtami.GidenKarsiTarafDeseni);

            Assert.False(eslesme.Success);
        }

        [Fact]
        public void Iban_kunyesiyle_baslayan_yakalama_gecersiz_sayilir()
        {
            // Giden EFT gövdesinde banka adından sonra karşı taraf değil, IBAN künyesi
            // geliyor. Künye elenmezse her satır farklı bir "unvan" üretir; elenince
            // sıradaki desen (NO'LU … HESABINA) gerçek karşı tarafı verir.
            var sonuc = _cikarici.Cikar(
                "DENİZBANK HESABINA (PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ VADESİZ HESABINDAN " +
                "DENİZBANK A.Ş. - IBAN MERKEZ SUBE ŞUBESİ NEZDİNDEKİ TR450013400001964210100008 " +
                "NO'LU ZAFER GENÇ HESABINA YAPILAN 1414049 SORGU NO'LU EFT)",
                Desenler,
                HesapSahibi);

            Assert.Equal("ZAFER GENÇ", sonuc.Unvan);
        }

        [Fact]
        public void Iban_kunyesi_elenince_hesap_sahibi_elemesi_yine_calisir()
        {
            // Karşı taraf hesap sahibinin kendisi: künye elendikten sonra NO'LU deseni
            // firmanın kendi adını verir, o da elenir. Satır kendi hesapları arasıdır.
            var sonuc = _cikarici.Cikar(
                "DENİZBANK HESABINA (PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ VADESİZ HESABINDAN " +
                "DENİZBANK A.Ş. - IBAN MERKEZ SUBE ŞUBESİ NEZDİNDEKİ TR450013400001964210100008 " +
                "NO'LU PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ HESABINA YAPILAN 1414049 SORGU NO'LU EFT)",
                Desenler,
                HesapSahibi);

            Assert.True(sonuc.HesapSahibiElendi);
            Assert.NotEqual("IBAN MERKEZ SUBE ŞUBESİ NEZDİNDEKİ TR450013400001964210100008 NO'LU PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ", sonuc.Unvan);
        }

        [Fact]
        public void Desen3_sorgu_nolu_kalan_metin()
        {
            var unvan = _cikarici.Cikar(
                "20260115 sorgu no'lu 5511223 PARK PLAZA YONETIMI",
                Desenler).Unvan;

            Assert.Equal("PARK PLAZA YONETIMI", unvan);
        }

        [Fact]
        public void Desen4_nolu_buyuk_harfli_unvan()
        {
            // "hesab" geçmediği için desen 2 tutmaz; desen 4 devreye girer.
            var unvan = _cikarici.Cikar(
                "123456 nolu PKF ISTANBUL YEMINLI MALI MUSAVIRLIK",
                Desenler).Unvan;

            Assert.Equal("PKF ISTANBUL YEMINLI MALI MUSAVIRLIK", unvan);
        }

        [Fact]
        public void Desen5_egik_cizgi_oncesi_unvan()
        {
            var unvan = _cikarici.Cikar(
                "MERT INSAAT SANAYI / ISTANBUL SUBESI",
                Desenler).Unvan;

            Assert.Equal("MERT INSAAT SANAYI", unvan);
        }

        [Fact]
        public void Desen6_parantez_oncesi_metin()
        {
            var unvan = _cikarici.Cikar(
                "Beta Yazılım Hizmetleri (ödeme referansı 99123)",
                Desenler).Unvan;

            Assert.Equal("Beta Yazılım Hizmetleri", unvan);
        }

        [Fact]
        public void Hicbir_desen_tutmazsa_null_doner()
        {
            var sonuc = _cikarici.Cikar("kredi karti borc odemesi", Desenler);

            Assert.Null(sonuc.Unvan);
            Assert.False(sonuc.HesapSahibiElendi);
        }

        [Fact]
        public void Bos_aciklama_null_doner()
        {
            Assert.Null(_cikarici.Cikar(null, Desenler).Unvan);
            Assert.Null(_cikarici.Cikar("   ", Desenler).Unvan);
        }

        [Fact]
        public void Bozuk_desen_ayristirmayi_dusurmez()
        {
            var desenler = new List<Api.Features.BankaEkstre.Domain.UnvanDeseni>
            {
                new() { Desen = "([", Sira = 10, Aktif = true, GrupNo = 1 },
                new() { Desen = @"^(.+?)\s*\(", Sira = 20, Aktif = true, GrupNo = 1 }
            };

            Assert.Equal("Alfa Ticaret", _cikarici.Cikar("Alfa Ticaret (referans)", desenler).Unvan);
        }

        // ---- Hesap sahibinin kendi unvanı ----

        [Fact]
        public void Hesap_sahibinin_kendi_unvani_karsi_taraf_sayilmaz()
        {
            var sonuc = _cikarici.Cikar(
                "0000123 sorgu numaralı PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ tarafından gönderilmiştir",
                Desenler,
                HesapSahibi);

            Assert.Null(sonuc.Unvan);
            Assert.True(sonuc.HesapSahibiElendi);
        }

        [Fact]
        public void Hesap_sahibi_atilinca_sonraki_desen_denenir()
        {
            // Birinci desen hesap sahibini, ikincisi gerçek karşı tarafı yakalıyor.
            // Atılan yakalama döngüyü bitirmemeli.
            var desenler = new List<Api.Features.BankaEkstre.Domain.UnvanDeseni>
            {
                new() { Desen = @"sorgu numaralı (.+?) tarafından", GrupNo = 1, Sira = 10, Aktif = true },
                new() { Desen = @"lehine (.+?) hesabına", GrupNo = 1, Sira = 20, Aktif = true }
            };

            var sonuc = _cikarici.Cikar(
                "sorgu numaralı PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ tarafından " +
                "lehine DAĞI GİYİM SANAYİ VE TİCARET A.Ş. hesabına aktarıldı",
                desenler,
                HesapSahibi);

            Assert.Equal("DAĞI GİYİM SANAYİ VE TİCARET A.Ş.", sonuc.Unvan);
            Assert.True(sonuc.HesapSahibiElendi);
        }

        [Fact]
        public void Eleme_normalize_cekirdek_uzerinden_yapilir()
        {
            // Yazım farkı elemeyi bozmamalı: karşılaştırma gürültü kelimeleri atılmış
            // çekirdek üzerinden yapılır ("A.Ş." ile "ANONİM ŞİRKETİ" aynı çekirdeği verir).
            var sonuc = _cikarici.Cikar(
                "0000123 sorgu numaralı PKF ADAY BAGIMSIZ DENETIM A.S. tarafından gönderilmiştir",
                Desenler,
                HesapSahibi);

            Assert.Null(sonuc.Unvan);
            Assert.True(sonuc.HesapSahibiElendi);
        }

        [Fact]
        public void Benzer_adli_farkli_cari_elenmez()
        {
            // Hatanın sonucu buydu: hesap sahibinin adı "Bağımsız Denetim Derneği" ile
            // eşleşiyordu. Çekirdekler farklı olduğu için bu unvan atılmamalı.
            var sonuc = _cikarici.Cikar(
                "0000123 sorgu numaralı BAĞIMSIZ DENETİM DERNEĞİ tarafından gönderilmiştir",
                Desenler,
                HesapSahibi);

            Assert.Equal("BAĞIMSIZ DENETİM DERNEĞİ", sonuc.Unvan);
            Assert.False(sonuc.HesapSahibiElendi);
        }

        [Fact]
        public void Hesap_sahibi_girilmemisse_eski_davranis_surer()
        {
            var sonuc = _cikarici.Cikar(
                "0000123 sorgu numaralı PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ tarafından gönderilmiştir",
                Desenler);

            Assert.Equal("PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ", sonuc.Unvan);
            Assert.False(sonuc.HesapSahibiElendi);
        }
    }
}
