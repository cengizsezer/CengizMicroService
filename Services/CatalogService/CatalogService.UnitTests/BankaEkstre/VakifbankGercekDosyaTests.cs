using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Madde 2: ayrıştırıcı gerçek Vakıfbank ekstresiyle sınanır — taklit dosyayla değil.
    ///
    /// Dosya yapısı: 1–5. satırlar hesap künyesi (birleştirilmiş hücreler), 6. satır boş,
    /// 7. satır kolon başlıkları, veri 8–294 arası (287 satır).
    ///
    /// Eski hata: başlık satırı bulunamıyordu. Kolon adları <c>OrdinalIgnoreCase</c> ile
    /// aranıyordu ve invariant kültür 'ı' → 'I' / 'i' → 'İ' dönüşümünü yapmadığı için
    /// "AÇIKLAMA" ≠ "Açıklama" ve "İŞLEM TARİHİ" ≠ "İşlem Tarihi" çıkıyordu. Tarih ve
    /// açıklama bulunamayınca satır başlık sayılmıyor, sessizce sabit indekslere düşülüyordu.
    /// </summary>
    public class VakifbankGercekDosyaTests
    {
        private static EkstreParseSonuc Ayristir()
        {
            using var dosya = BankaEkstreTestOrtami.GercekEkstre();
            return new VakifbankVadesizParser().Ayristir(dosya);
        }

        [Fact]
        public void Baslik_satiri_bulunur_uyari_cikmaz()
        {
            var sonuc = Ayristir();

            Assert.Empty(sonuc.Uyarilar);
        }

        [Fact]
        public void Iki_yuz_seksen_yedi_satir_ayrisir()
        {
            var sonuc = Ayristir();

            Assert.Equal(287, sonuc.Satirlar.Count);
            Assert.Equal(0, sonuc.AtlananSatir);
        }

        [Fact]
        public void Aciklama_kolonu_basliktan_bulunur()
        {
            // AÇIKLAMA 17. kolon (Q). Düzeltilmiş ekstre bu kolona yazdığı için kritik.
            var sonuc = Ayristir();

            Assert.Equal(17, sonuc.AciklamaKolonu);
        }

        [Fact]
        public void Veri_sekizinci_satirdan_baslar()
        {
            var sonuc = Ayristir();

            Assert.Equal(8, sonuc.Satirlar[0].KaynakSatirNo);
            Assert.Equal(294, sonuc.Satirlar[^1].KaynakSatirNo);
        }

        [Fact]
        public void Tarih_islem_tarihi_kolonundan_okunur()
        {
            // HAREKET TARIH (3. kolon) saat de içeriyor: "22.07.2026 14:12".
            // İŞLEM TARİHİ (4. kolon) saatsiz; kullanılması gereken bu.
            var sonuc = Ayristir();
            var ilk = sonuc.Satirlar[0];

            Assert.Equal(new DateTime(2026, 7, 22), ilk.Tarih);
            Assert.Equal(ilk.Tarih.Date, ilk.Tarih);
        }

        [Fact]
        public void Donem_dosyanin_tarih_araligiyla_ayni()
        {
            // Künyedeki "TARİH ARALIĞI: 22.07.2026 - 21.08.2026" ile tutmalı.
            var sonuc = Ayristir();

            Assert.Equal(new DateTime(2026, 7, 22), sonuc.DonemBaslangic);
            Assert.Equal(new DateTime(2026, 8, 21), sonuc.DonemBitis);
        }

        [Fact]
        public void Yon_dagilimi_gercek_veriyle_tutar()
        {
            // Dosyada 173 "A" (alacak) ve 114 "B" (borç) satırı var. Bakiye kolonundan
            // doğrulandı: her A satırında bakiye tutar kadar artıyor, her B satırında
            // tutar kadar azalıyor. A = giren, B = çıkan.
            var sonuc = Ayristir();

            Assert.Equal(173, sonuc.Satirlar.Count(s => s.Yon == Yon.Giren));
            Assert.Equal(114, sonuc.Satirlar.Count(s => s.Yon == Yon.Cikan));
        }

        [Fact]
        public void Tutar_her_zaman_pozitif_saklanir()
        {
            // Dosyada B satırlarının tutarı negatif geliyor; işaret Yon alanında durur.
            var sonuc = Ayristir();

            Assert.All(sonuc.Satirlar, s => Assert.True(s.Tutar > 0m));
        }

        [Fact]
        public void Ilk_satirin_tum_alanlari_dogru_okunur()
        {
            var ilk = Ayristir().Satirlar[0];

            Assert.Equal("Alınan Havale", ilk.IslemTipi);
            Assert.Equal(198000m, ilk.Tutar);
            Assert.Equal(Yon.Giren, ilk.Yon);
            Assert.Equal("Birim", ilk.Kanal);
            Assert.Contains("CMS JANT VE MAKİNA SANAYİİ", ilk.HamAciklama);
        }

        [Fact]
        public void Son_satirda_isaretli_tutar_ve_borc_birlikte_dogru_okunur()
        {
            var son = Ayristir().Satirlar[^1];

            Assert.Equal("Kredi Kartı Otomatik Ödeme", son.IslemTipi);
            Assert.Equal(73806.68m, son.Tutar);
            Assert.Equal(Yon.Cikan, son.Yon);
        }

        [Fact]
        public void Karsi_vkn_doldurulmaz()
        {
            // VKN kolonunda her satırda hesap sahibinin kendi VKN'si var (0070511435).
            var sonuc = Ayristir();

            Assert.All(sonuc.Satirlar, s => Assert.Null(s.KarsiVkn));
        }

        // ---- Giden FAST/EFT karşı tarafı ----

        /// <summary>Ölçülen dosyadaki hesap sahibi unvanı.</summary>
        private const string HesapSahibi = "PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ";

        private static List<string?> Unvanlar()
        {
            var cikarici = new UnvanCikarici();
            var desenler = BankaEkstreTestOrtami.Desenler();

            return Ayristir().Satirlar
                .Select(s => cikarici.Cikar(s.HamAciklama, desenler, HesapSahibi).Unvan)
                .ToList();
        }

        [Theory]
        [InlineData("İlyas Ömeroğlu")]
        [InlineData("Mesut Aktaş")]
        [InlineData("DİLARA KAYA")]
        public void Giden_fast_karsi_taraflari_gercek_dosyadan_cikarilir(string beklenen)
        {
            // "… hesabından Türkiye Garanti Bankası A.Ş. İlyas Ömeroğlu hesabına giden FAST
            // ödemesi". Banka adından sonraki desen eklenmeden önce bu satırlarda unvan
            // olarak açıklamanın kalanı ("ADAY BAĞIMSIZ DENETİM … hesabına giden FAST
            // ödemesi") çıkıyordu.
            Assert.Contains(beklenen, Unvanlar());
        }

        /// <summary>Dosyadaki giden FAST/EFT satırlarının çıkarılan unvanları.</summary>
        private static List<string?> GidenUnvanlar()
        {
            var cikarici = new UnvanCikarici();
            var desenler = BankaEkstreTestOrtami.Desenler();

            return Ayristir().Satirlar
                .Where(s => s.HamAciklama.Contains("hesabına giden", StringComparison.Ordinal))
                .Select(s => cikarici.Cikar(s.HamAciklama, desenler, HesapSahibi).Unvan)
                .ToList();
        }

        [Fact]
        public void Giden_fast_satirlarinin_tamami_karsi_taraf_verir()
        {
            // Dosyada "hesabına giden" kalıbında 38 satır var; hepsi karşı taraf vermeli.
            var unvanlar = GidenUnvanlar();

            Assert.Equal(38, unvanlar.Count);
            Assert.All(unvanlar, u => Assert.False(string.IsNullOrWhiteSpace(u)));
        }

        [Fact]
        public void Giden_fast_satirlarinin_hicbirinde_govde_metni_unvan_sayilmaz()
        {
            // Karşı taraf çıkarılamayan satırın imzası: yakalanan metnin içinde gövdenin
            // kendisi ("hesabından") kalıyor.
            var bozuk = GidenUnvanlar()
                .Where(u => u is not null && u.Contains("hesab", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.Empty(bozuk);
        }

        [Fact]
        public void Unvan_yakalamalari_iban_kunyesiyle_baslamaz()
        {
            // "… - IBAN MERKEZ SUBE ŞUBESİ NEZDİNDEKİ TR45… NO'LU …" künyesi unvan değil.
            var kunyeler = Unvanlar()
                .Where(u => u is not null)
                .Where(u => Normalizasyon.MetinNormalize(u) is var sade &&
                            (sade.StartsWith("IBAN", StringComparison.Ordinal) ||
                             sade.StartsWith("MERKEZ SUBE", StringComparison.Ordinal) ||
                             sade.StartsWith("NEZDINDEKI", StringComparison.Ordinal) ||
                             sade.StartsWith("TR", StringComparison.Ordinal) && sade.Length > 4 && char.IsDigit(sade[2])))
                .ToList();

            Assert.Empty(kunyeler);
        }

        [Fact]
        public void Baslik_bulunamazsa_taranan_satirlarin_icerigi_uyariya_yazilir()
        {
            // Başlıksız dosyada uyarı yalnız "bulunamadı" demez; ne görüldüğünü de yazar.
            using var dosya = BankaEkstreTestOrtami.BasliksizEkstre(
                new object[] { "15.01.2026", "Gelen EFT Otomatik Yatan", 100.0, "İnternet", "0070511435", "A", "test" });

            var sonuc = new VakifbankVadesizParser().Ayristir(dosya);

            var uyari = Assert.Single(sonuc.Uyarilar);
            Assert.Contains("Başlık satırı bulunamadı", uyari);
            Assert.Contains("Taranan satırlarda görülen metinler", uyari);
            Assert.Contains("VAKIFBANK", uyari);
        }
    }
}
