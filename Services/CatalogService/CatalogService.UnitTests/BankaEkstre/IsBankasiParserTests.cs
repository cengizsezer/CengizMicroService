using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// İş Bankası ayrıştırıcısı. Vakıfbank'tan farklı üç şeyi sınar: dosyanın eski .xls
    /// biçimi, tarih hücresindeki tire ayraçlı saat ve borç/alacak kolonu olmadan yönün
    /// tutarın işaretinden okunması.
    ///
    /// Ham açıklamalar gerçek 7 aylık ekstreden birebir alınmıştır.
    /// </summary>
    public class IsBankasiParserTests
    {
        private readonly IsBankasiVadesizParser _parser = new();

        // ---- Gerçek dosyadan ham açıklamalar ----

        private const string Renkler =
            "RENKLER MAKİNA VE YEDEK PARÇA SANAYİ VE TİCARET A.*0111**8792586";

        private const string GyVarlik =
            "GY VARLIK KİRALAMA ANONİM ŞİRKETİ*0153*FATURA BEDELİ GÖNDEREN: GY VARLIK KİRALAMA A.Ş.";

        private const string MuhammedMuhsin =
            "MUHAMMED MUHSİN*0111*Muhsin Group carisine istinaden*1629974954*FAST";

        private const string Saurer =
            "SAURER TEKSTİL A.Ş*0062*SAURER DOMİCİLATION SERVICE 052026*3667000239*FAST";

        // "Havale" tipinde unvan SONDA: baştaki alan işlemin gövdesi.
        private const string HavaleOpat =
            "2. FATURA BEDELİ ÖDEMESİ*OPAT OTOMOTİV İNŞAAT ELEKTRONİK TURİZM GIDA PAZARLAMA";

        private const string HavaleOsmanli =
            "6481373184-OPH Fon Bağımsız Denetim Hizmeti Bedeli*OSMANLI PORTFÖY YÖNETİMİ A.Ş.";

        // Kendi hesapları arası: baştaki alan hesap sahibinin kendi unvanı.
        private const string BankalarArasi =
            "PKF ADAY BAĞIMSIZ DENETİM A.Ş.*TR400001500158007298490100*VAKIFBANK*0082558";

        private const string KrediTaksiti =
            "KREDİ NO: 10080844268 ANAPARA TAHSİLAT";

        // ---- Dosya biçimi ve kolonlar ----

        [Fact]
        public void Xls_dosyasi_okunur_ve_basliklar_isimle_bulunur()
        {
            using var dosya = UcBankaTestOrtami.IsBankasiEkstresi(
                new object?[] { "26/08/2026-14:58:47", "İşCep", -12500.75m, "EFT", Renkler, "8792586" },
                new object?[] { "25/08/2026-09:12:03", "Sistem", 3400.00m, "FAST", Saurer, "3667000239" });

            var sonuc = _parser.Ayristir(dosya);

            Assert.Empty(sonuc.Uyarilar);
            Assert.Equal(2, sonuc.Satirlar.Count);
            // Açıklama 9. kolon; düzeltilmiş ekstre bu numarayla çalışıyor.
            Assert.Equal(9, sonuc.AciklamaKolonu);
        }

        [Fact]
        public void Tarih_saat_ayracindan_ayrilir()
        {
            using var dosya = UcBankaTestOrtami.IsBankasiEkstresi(
                new object?[] { "26/08/2026-14:58:47", "İşCep", -100m, "EFT", Renkler, "1" });

            var satir = _parser.Ayristir(dosya).Satirlar.Single();

            // Ayraç tire; boşluk bekleyen bir ayrıştırıcı bu hücreyi hiç okuyamazdı.
            Assert.Equal(new DateTime(2026, 8, 26), satir.Tarih);
        }

        [Fact]
        public void Yon_tutarin_isaretinden_okunur_tutar_pozitif_saklanir()
        {
            using var dosya = UcBankaTestOrtami.IsBankasiEkstresi(
                new object?[] { "26/08/2026-14:58:47", "İşCep", -12500.75m, "EFT", Renkler, "1" },
                new object?[] { "26/08/2026-15:02:11", "Sistem", 3400.00m, "EFT", Saurer, "2" });

            var satirlar = _parser.Ayristir(dosya).Satirlar;

            Assert.Equal(Yon.Cikan, satirlar[0].Yon);
            Assert.Equal(12500.75m, satirlar[0].Tutar);

            Assert.Equal(Yon.Giren, satirlar[1].Yon);
            Assert.Equal(3400.00m, satirlar[1].Tutar);
        }

        [Fact]
        public void Islem_tipi_kanal_ve_referans_okunur()
        {
            using var dosya = UcBankaTestOrtami.IsBankasiEkstresi(
                new object?[] { "26/08/2026-14:58:47", "İşCep", -100m, "Havale", HavaleOpat, "6481373184" });

            var satir = _parser.Ayristir(dosya).Satirlar.Single();

            Assert.Equal("Havale", satir.IslemTipi);
            Assert.Equal("İşCep", satir.Kanal);
            // Referans bankanın kendi tekil anahtarı; mükerrer yükleme kontrolü için saklanır.
            Assert.Equal("6481373184", satir.Referans);
            Assert.Equal(17, satir.KaynakSatirNo);
        }

        [Fact]
        public void Aciklamadaki_ibani_cikarir()
        {
            using var dosya = UcBankaTestOrtami.IsBankasiEkstresi(
                new object?[] { "26/08/2026-14:58:47", "Sistem", -100m, "EFT", BankalarArasi, "1" });

            var satir = _parser.Ayristir(dosya).Satirlar.Single();

            Assert.Equal("TR400001500158007298490100", satir.KarsiIban);
            // Açıklamadaki son numara işlemin referansı; karşı tarafın VKN'si değil.
            Assert.Null(satir.KarsiVkn);
        }

        [Fact]
        public void Tarihi_okunamayan_satir_atlanir()
        {
            using var dosya = UcBankaTestOrtami.IsBankasiEkstresi(
                new object?[] { "26/08/2026-14:58:47", "İşCep", -100m, "EFT", Renkler, "1" },
                new object?[] { "TOPLAM", null, -250m, null, null, null });

            var sonuc = _parser.Ayristir(dosya);

            Assert.Single(sonuc.Satirlar);
            Assert.Equal(1, sonuc.AtlananSatir);
        }

        [Fact]
        public void Baslik_bulunamazsa_sabit_indekslere_duser_ve_uyarir()
        {
            using var dosya = UcBankaTestOrtami.IsBankasiBasliksizEkstre(
                new object?[] { "26/08/2026-14:58:47", "İşCep", -100m, "EFT", Renkler, "1" });

            var sonuc = _parser.Ayristir(dosya);

            var uyari = Assert.Single(sonuc.Uyarilar);
            Assert.Contains("Başlık satırı bulunamadı", uyari);
            // Sabit indeksler ölçülen yerleşim: satır yine de okunur.
            Assert.Single(sonuc.Satirlar);
            Assert.Equal("EFT", sonuc.Satirlar[0].IslemTipi);
        }

        // ---- Unvan çıkarma ----

        [Theory]
        [InlineData(Renkler, "RENKLER MAKİNA VE YEDEK PARÇA SANAYİ VE TİCARET A.")]
        [InlineData(GyVarlik, "GY VARLIK KİRALAMA ANONİM ŞİRKETİ")]
        [InlineData(MuhammedMuhsin, "MUHAMMED MUHSİN")]
        [InlineData(Saurer, "SAURER TEKSTİL A.Ş")]
        public void Bastaki_unvan_banka_kodu_cipasiyla_yakalanir(string aciklama, string beklenen)
        {
            var sonuc = Cikar(aciklama);

            Assert.Equal(beklenen, sonuc.Unvan);
        }

        [Theory]
        [InlineData(HavaleOpat, "OPAT OTOMOTİV İNŞAAT ELEKTRONİK TURİZM GIDA PAZARLAMA")]
        [InlineData(HavaleOsmanli, "OSMANLI PORTFÖY YÖNETİMİ A.Ş.")]
        public void Havale_satirinda_unvan_sondan_alinir(string aciklama, string beklenen)
        {
            // "Havale" tipinde unvan başta değil sonda. Ayrım işlem tipiyle değil veriyle
            // yapılıyor: baştaki desen ikinci alanın dört haneli banka kodu olmasını şart
            // koşuyor, havale gövdesinde orada kod değil metin var.
            var sonuc = Cikar(aciklama);

            Assert.Equal(beklenen, sonuc.Unvan);
        }

        [Fact]
        public void Bankalar_arasi_satirda_hesap_sahibi_elenir()
        {
            var sonuc = Cikar(BankalarArasi);

            // Karşı taraf firmanın kendisi: unvan verilmez, bayrak açılır. Banka kayıt
            // defteri katmanı bu bayrakla devreye giriyor.
            Assert.Null(sonuc.Unvan);
            Assert.True(sonuc.HesapSahibiElendi);
        }

        private static UnvanSonuc Cikar(string aciklama)
            => new UnvanCikarici().Cikar(aciklama, UcBankaTestOrtami.IsBankasiDesenleri(),
                                         UcBankaTestOrtami.HesapSahibi);

        // ---- Açıklama üretimi ----

        [Theory]
        [InlineData(Yon.Giren, "Gelen Eft - Renkler Makina Ve Yedek Parça")]
        [InlineData(Yon.Cikan, "Giden Eft - Renkler Makina Ve Yedek Parça")]
        public void Ayni_islem_tipinde_yon_sablondan_gelir(Yon yon, string beklenenOnek)
        {
            // İş Bankası "EFT" tipini hem tahsilatta hem ödemede kullanıyor; şablon
            // tablosunda yön alanı olmadığı için yön {YON} yer tutucusuyla veriden geliyor.
            var baglam = new SatirBaglami
            {
                IslemTipi = "EFT",
                HamAciklama = Renkler,
                Yon = yon,
                Unvan = "RENKLER MAKİNA VE YEDEK PARÇA SANAYİ VE TİCARET A."
            };

            var uretici = new AciklamaUretici();
            baglam.Sablon = uretici.SablonBul(baglam.IslemTipi, UcBankaTestOrtami.IsBankasiSablonlari(),
                                              baglam.HamAciklama);

            Assert.StartsWith(beklenenOnek, uretici.Uret(baglam));
        }

        [Fact]
        public void Kredi_satirinda_aciklama_ve_ogrenme_anahtari_ayni_numarayi_tasir()
        {
            var baglam = new SatirBaglami
            {
                IslemTipi = "Kredi",
                HamAciklama = KrediTaksiti,
                Yon = Yon.Cikan
            };

            var uretici = new AciklamaUretici();
            baglam.Sablon = uretici.SablonBul(baglam.IslemTipi, UcBankaTestOrtami.IsBankasiSablonlari(),
                                              baglam.HamAciklama);

            Assert.Equal("Kredi No: 10080844268", uretici.Uret(baglam));
            // Her kredinin muavini ayrı; anahtar kredi numarasını taşımazsa bütün krediler
            // ilk onaydan sonra aynı hesaba çözülür.
            Assert.Equal("KREDI:10080844268", Normalizasyon.KrediAnahtar(KrediTaksiti));
        }
    }
}
