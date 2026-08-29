using CatalogService.Api.Features.Ajanlar.Domain;
using CatalogService.Api.Features.Ajanlar.Services;

namespace CatalogService.UnitTests.Ajanlar
{
    /// <summary>
    /// Bellekteki ajan listesinin kuralları. Buradaki asıl mesele "hayalet kayıt":
    /// ofisteki makine gün içinde birkaç kez yeniden bağlanıyor ve listede iki kez
    /// görünürse hangisine iş gönderileceği belirsizleşiyor.
    /// </summary>
    public class AjanDeposuTests
    {
        private static AjanDeposu Depo(SahteSaat saat, int zamanAsimi = 90)
            => new(new SabitAyar<CatalogService.Api.Features.Ajanlar.AgentHubAyarlari>(
                       AjanTestVerisi.Ayarlar(zamanAsimi: zamanAsimi)), saat);

        private static AjanKaydi Ajan(string makineId, string connectionId, string makineAdi = "BANKA-PC",
                                      Action? kes = null) => new()
        {
            MakineId = makineId,
            ConnectionId = connectionId,
            MakineAdi = makineAdi,
            AjanSurumu = "1.0.0",
            KullaniciId = "kullanici-1",
            BaglantiyiKes = kes
        };

        [Fact]
        public void Kaydedilen_ajan_listede_gorunur()
        {
            var depo = Depo(new SahteSaat());

            depo.Kaydet(Ajan("MAK-1", "c1"));

            var liste = depo.Baglilar();
            Assert.Single(liste);
            Assert.Equal("MAK-1", liste[0].MakineId);
            Assert.Equal("c1", liste[0].ConnectionId);
        }

        [Fact]
        public void Kayit_zamanlari_depoda_atanir()
        {
            var saat = new SahteSaat();
            var depo = Depo(saat);

            var sonuc = depo.Kaydet(Ajan("MAK-1", "c1"));

            Assert.Equal(saat.GetUtcNow(), sonuc.Ajan.BaglantiZamani);
            Assert.Equal(saat.GetUtcNow(), sonuc.Ajan.SonKalpAtisi);
        }

        [Fact]
        public void Ayni_makineyle_ikinci_baglanti_eskisini_dusurur()
        {
            var depo = Depo(new SahteSaat());
            depo.Kaydet(Ajan("MAK-1", "c1"));

            var sonuc = depo.Kaydet(Ajan("MAK-1", "c2"));

            Assert.NotNull(sonuc.Dusurulen);
            Assert.Equal("c1", sonuc.Dusurulen!.ConnectionId);

            var liste = depo.Baglilar();
            Assert.Single(liste);
            Assert.Equal("c2", liste[0].ConnectionId);
        }

        [Fact]
        public void Ayni_baglantinin_ikinci_kaydi_dusurme_sayilmaz()
        {
            var depo = Depo(new SahteSaat());
            depo.Kaydet(Ajan("MAK-1", "c1"));

            var sonuc = depo.Kaydet(Ajan("MAK-1", "c1", makineAdi: "BANKA-PC (yeni ad)"));

            Assert.Null(sonuc.Dusurulen);
            Assert.Equal("BANKA-PC (yeni ad)", Assert.Single(depo.Baglilar()).MakineAdi);
        }

        [Fact]
        public void Farkli_makineler_yan_yana_durur()
        {
            var depo = Depo(new SahteSaat());

            depo.Kaydet(Ajan("MAK-1", "c1", "ALFA"));
            depo.Kaydet(Ajan("MAK-2", "c2", "BETA"));

            Assert.Equal(new[] { "ALFA", "BETA" }, depo.Baglilar().Select(a => a.MakineAdi));
        }

        [Fact]
        public void Baglanti_kopunca_depodan_silinir()
        {
            var depo = Depo(new SahteSaat());
            depo.Kaydet(Ajan("MAK-1", "c1"));

            var cikan = depo.Cikar("c1");

            Assert.NotNull(cikan);
            Assert.Empty(depo.Baglilar());
        }

        [Fact]
        public void Dusurulen_baglantinin_kopusu_yerine_gecen_kaydi_silmez()
        {
            // Sıra gerçekte böyle: yeni bağlantı kaydolur, ardından eski soketin
            // "koptum" bildirimi gelir. ConnectionId eşleşmediği için dokunmamalı —
            // yoksa makine yeniden bağlandığı anda listeden düşerdi.
            var depo = Depo(new SahteSaat());
            depo.Kaydet(Ajan("MAK-1", "c1"));
            depo.Kaydet(Ajan("MAK-1", "c2"));

            var cikan = depo.Cikar("c1");

            Assert.Null(cikan);
            Assert.Equal("c2", Assert.Single(depo.Baglilar()).ConnectionId);
        }

        [Fact]
        public void Tanimadigi_baglanti_kimligi_cikarmaya_calisinca_null_doner()
        {
            var depo = Depo(new SahteSaat());
            depo.Kaydet(Ajan("MAK-1", "c1"));

            Assert.Null(depo.Cikar("bilinmeyen"));
            Assert.Single(depo.Baglilar());
        }

        [Fact]
        public void Kalp_atisi_son_atisi_gunceller()
        {
            var saat = new SahteSaat();
            var depo = Depo(saat);
            depo.Kaydet(Ajan("MAK-1", "c1"));
            var ilk = depo.Baglilar()[0].SonKalpAtisi;

            saat.Ilerle(TimeSpan.FromSeconds(30));
            Assert.True(depo.KalpAtisi("c1"));

            var sonra = depo.Baglilar()[0].SonKalpAtisi;
            Assert.Equal(ilk.AddSeconds(30), sonra);
        }

        [Fact]
        public void Kaydolmamis_baglantidan_gelen_kalp_atisi_yok_sayilir()
        {
            var depo = Depo(new SahteSaat());

            Assert.False(depo.KalpAtisi("c-yok"));
        }

        [Fact]
        public void Kalp_atisi_kesilen_kayit_listeden_duser()
        {
            var saat = new SahteSaat();
            var depo = Depo(saat, zamanAsimi: 90);
            depo.Kaydet(Ajan("MAK-1", "c1"));

            saat.Ilerle(TimeSpan.FromSeconds(91));

            Assert.Empty(depo.Baglilar());
            // Süzme aynı zamanda temizlik: kayıt gerçekten çıkarıldığı için sonradan
            // gelen kopuş bildirimi de onu bulamaz.
            Assert.Null(depo.Cikar("c1"));
        }

        [Fact]
        public void Atisini_surdiren_ajan_esik_asilsa_da_listede_kalir()
        {
            var saat = new SahteSaat();
            var depo = Depo(saat, zamanAsimi: 90);
            depo.Kaydet(Ajan("MAK-1", "c1"));

            for (var i = 0; i < 5; i++)
            {
                saat.Ilerle(TimeSpan.FromSeconds(30));
                depo.KalpAtisi("c1");
            }

            Assert.Single(depo.Baglilar());
        }
    }
}
