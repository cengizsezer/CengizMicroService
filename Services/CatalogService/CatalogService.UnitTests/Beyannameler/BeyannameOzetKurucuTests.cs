using CatalogService.Api.Features.Declarations.Dtos;
using CatalogService.Api.Features.Declarations.Entities;
using CatalogService.Api.Features.Declarations.Services;

namespace CatalogService.UnitTests.Beyannameler
{
    /// <summary>
    /// Firma × beyanname türü matrisi. Kurucu saf fonksiyon olduğu için kurallar
    /// veritabanı kurmadan sınanıyor: durum sırası, satır/sütun toplamları, eşleşmeyen
    /// tür raporu ve aynı hücrede birden fazla kayıt.
    ///
    /// Örnek veri kullanıcının Excel'de elle tuttuğu tablodan alındı (ALPHA AHŞAP tek
    /// beyanname, CİTADEL dört beyanname).
    /// </summary>
    public class BeyannameOzetKurucuTests
    {
        private const int Yil = 2026;
        private const int Ay = 8;

        private static List<BeyannameTuru> Turler() => new()
        {
            new BeyannameTuru { Id = 1, Deger = "0015 KDV-1", Kod = "0015", Ad = "KDV (1 No.lu)", Sira = 10, Aktif = true },
            new BeyannameTuru { Id = 2, Deger = "4017 KDV-2", Kod = "4017", Ad = "KDV Tevkifat (2 No.lu)", Sira = 20, Aktif = true },
            new BeyannameTuru { Id = 3, Deger = "0003 STOPAJ MUHTASAR", Kod = "0003", Ad = "Gelir Vergisi Stopajı", Sira = 30, Aktif = true },
            new BeyannameTuru { Id = 4, Deger = "SGK", Kod = "4101", Ad = "SGK Primi", Sira = 40, Aktif = true }
        };

        private static List<CustomerCompany> Firmalar() => new()
        {
            new CustomerCompany { Id = 1, CompanyName = "ALPHA AHŞAP", TaxNumber = "7721471008", IsActive = true },
            new CustomerCompany { Id = 3, CompanyName = "CİTADEL GAYRİMENKUL", TaxNumber = "7280624888", IsActive = true }
        };

        private static Declaration Beyanname(int id, int firmaId, string tur, decimal tutar,
                                             DeclarationStatus durum = DeclarationStatus.Draft,
                                             PaymentStatus odeme = PaymentStatus.Pending) => new()
        {
            Id = id,
            CustomerCompanyId = firmaId,
            DeclarationType = tur,
            Year = Yil,
            Month = Ay,
            Amount = tutar,
            DueDate = new DateTime(Yil, Ay, 26),
            DeclarationStatus = durum,
            PaymentStatus = odeme
        };

        private static BeyannameOzetDto Kur(IEnumerable<Declaration>? beyannameler = null,
                                            IEnumerable<BeyannameEk>? ekler = null)
            => BeyannameOzetKurucu.Kur(Yil, Ay, Turler(), Firmalar(),
                                       (beyannameler ?? Enumerable.Empty<Declaration>()).ToList(),
                                       (ekler ?? Enumerable.Empty<BeyannameEk>()).ToList());

        // ---- İskelet ----

        [Fact]
        public void Kolonlar_tanim_tablosundan_ve_sirasiyla_gelir()
        {
            var ozet = Kur();

            Assert.Equal(4, ozet.Turler.Count);
            Assert.Equal(new[] { "KDV (1 No.lu)", "KDV Tevkifat (2 No.lu)", "Gelir Vergisi Stopajı", "SGK Primi" },
                         ozet.Turler.Select(t => t.Ad));
            // Kolon başlığının altında vergi kodu gösteriliyor.
            Assert.Equal("4101", ozet.Turler[3].Kod);
        }

        [Fact]
        public void Her_firma_her_tur_icin_bir_hucre_alir()
        {
            var ozet = Kur();

            Assert.Equal(2, ozet.Satirlar.Count);
            Assert.All(ozet.Satirlar, s => Assert.Equal(4, s.Hucreler.Count));

            // Sıra numarası ve künye Excel'deki gibi satırda duruyor.
            Assert.Equal(1, ozet.Satirlar[0].Sira);
            Assert.Equal("ALPHA AHŞAP", ozet.Satirlar[0].FirmaAdi);
            Assert.Equal("7721471008", ozet.Satirlar[0].VergiKimlikNo);
        }

        [Fact]
        public void Beyannamesi_olmayan_hucre_bos_gorunur()
        {
            var ozet = Kur();

            var hucre = ozet.Satirlar[0].Hucreler[0];

            Assert.Equal(BeyannameHucreDurum.Yok, hucre.Durum);
            Assert.Null(hucre.DeclarationId);
            Assert.Equal(0m, hucre.Tutar);
        }

        // ---- Durum ----

        [Theory]
        [InlineData(DeclarationStatus.Draft, PaymentStatus.Pending, BeyannameHucreDurum.Hazirlandi)]
        [InlineData(DeclarationStatus.Ready, PaymentStatus.Pending, BeyannameHucreDurum.Hazirlandi)]
        [InlineData(DeclarationStatus.Approved, PaymentStatus.Pending, BeyannameHucreDurum.Onaylandi)]
        [InlineData(DeclarationStatus.Submitted, PaymentStatus.Planned, BeyannameHucreDurum.Onaylandi)]
        [InlineData(DeclarationStatus.Draft, PaymentStatus.Paid, BeyannameHucreDurum.Odendi)]
        public void Durum_beyanname_ve_odeme_durumundan_turetilir(
            DeclarationStatus beyannameDurum, PaymentStatus odemeDurum, BeyannameHucreDurum beklenen)
        {
            var ozet = Kur(new[] { Beyanname(1, 1, "0015 KDV-1", 1000m, beyannameDurum, odemeDurum) });

            Assert.Equal(beklenen, ozet.Satirlar[0].Hucreler[0].Durum);
        }

        [Fact]
        public void Ayni_hucrede_iki_kayit_varsa_en_geri_durum_gosterilir()
        {
            // Biri ödendi diye hücre yeşile dönerse yanındaki ödenmemiş kayıt görünmez olurdu.
            var ozet = Kur(new[]
            {
                Beyanname(1, 1, "0015 KDV-1", 1000m, DeclarationStatus.Approved, PaymentStatus.Paid),
                Beyanname(2, 1, "0015 KDV-1", 500m, DeclarationStatus.Draft, PaymentStatus.Pending)
            });

            var hucre = ozet.Satirlar[0].Hucreler[0];

            Assert.Equal(BeyannameHucreDurum.Hazirlandi, hucre.Durum);
            Assert.Equal(2, hucre.KayitSayisi);
            Assert.Equal(1500m, hucre.Tutar);
            Assert.Equal(2, hucre.Kayitlar.Count);
        }

        // ---- Toplamlar ----

        [Fact]
        public void Satir_ve_sutun_toplamlari_dolu_hucreleri_sayar()
        {
            // Excel'deki örnek: ALPHA 1 beyanname, CİTADEL 3.
            var ozet = Kur(new[]
            {
                Beyanname(1, 1, "0015 KDV-1", 1000m),
                Beyanname(2, 3, "0015 KDV-1", 2000m),
                Beyanname(3, 3, "4017 KDV-2", 300m),
                Beyanname(4, 3, "SGK", 700m)
            });

            Assert.Equal(1, ozet.Satirlar[0].DoluHucreSayisi);
            Assert.Equal(1000m, ozet.Satirlar[0].ToplamTutar);

            Assert.Equal(3, ozet.Satirlar[1].DoluHucreSayisi);
            Assert.Equal(3000m, ozet.Satirlar[1].ToplamTutar);

            // KDV-1 kolonunda iki firma da var.
            var kdv1 = ozet.KolonToplamlari.Single(k => k.TuruId == 1);
            Assert.Equal(2, kdv1.DoluHucreSayisi);
            Assert.Equal(3000m, kdv1.ToplamTutar);

            // Hiç kaydı olmayan kolon sıfır; tablodan düşmez.
            var stopaj = ozet.KolonToplamlari.Single(k => k.TuruId == 3);
            Assert.Equal(0, stopaj.DoluHucreSayisi);

            Assert.Equal(4, ozet.ToplamBeyanname);
            Assert.Equal(4000m, ozet.ToplamTutar);
        }

        // ---- Eşleşmeyen tür ----

        [Fact]
        public void Taninmayan_tur_sessizce_dusmez_raporlanir()
        {
            var ozet = Kur(new[]
            {
                Beyanname(1, 1, "0015 KDV-1", 1000m),
                Beyanname(2, 1, "9999 TANIMSIZ", 50m)
            });

            Assert.Equal(1, ozet.ToplamBeyanname);
            var eslesmeyen = Assert.Single(ozet.EslesmeyenTurler);
            Assert.Equal("9999 TANIMSIZ", eslesmeyen);
        }

        [Fact]
        public void Ayni_taninmayan_tur_bir_kez_raporlanir()
        {
            var ozet = Kur(new[]
            {
                Beyanname(1, 1, "9999 TANIMSIZ", 50m),
                Beyanname(2, 3, "9999 tanimsiz", 60m)
            });

            Assert.Single(ozet.EslesmeyenTurler);
        }

        // ---- Belgeler ----

        [Fact]
        public void Hucre_bagli_belgelerin_turlerini_tasir()
        {
            var beyanname = Beyanname(7, 3, "0015 KDV-1", 2000m, DeclarationStatus.Approved, PaymentStatus.Paid);

            var ozet = Kur(new[] { beyanname }, new[]
            {
                new BeyannameEk { Id = 1, DeclarationId = 7, Tur = BeyannameEkTuru.Dekont, FileId = 11 },
                new BeyannameEk { Id = 2, DeclarationId = 7, Tur = BeyannameEkTuru.Tahakkuk, FileId = 12 }
            });

            var hucre = ozet.Satirlar[1].Hucreler[0];

            // İkonların sırası tür sırası; tahakkuk önce.
            Assert.Equal(new[] { BeyannameEkTuru.Tahakkuk, BeyannameEkTuru.Dekont },
                         hucre.Ekler.Select(e => e.Tur));
        }
    }
}
