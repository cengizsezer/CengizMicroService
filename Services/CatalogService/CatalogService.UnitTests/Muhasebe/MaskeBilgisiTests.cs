using CatalogService.Api.Features.Muhasebe.Domain;
using CatalogService.Api.Features.Muhasebe.Services;

namespace CatalogService.UnitTests.Muhasebe
{
    /// <summary>
    /// Kod maskesi çözümlemesi. Kod üretme mantığının tamamı buradan okuduğu için
    /// maskenin firma bazında değişebilirliği de burada doğrulanır.
    /// </summary>
    public class MaskeBilgisiTests
    {
        [Fact]
        public void VarsayilanMaske_KebirSeviyesiniVeDerinligiDogruCikariyor()
        {
            var maske = MaskeBilgisi.Coz(new KodMaskesi { SegmentUzunluk = "3,2,2,4", Ayrac = "." });

            Assert.Equal(3, maske.KebirSeviyesi);
            Assert.Equal(6, maske.MaksimumSeviye);
            Assert.Equal(1, maske.SegmentUzunlugu(1));
            Assert.Equal(1, maske.SegmentUzunlugu(3));
            Assert.Equal(2, maske.SegmentUzunlugu(4));
            Assert.Equal(2, maske.SegmentUzunlugu(5));
            Assert.Equal(4, maske.SegmentUzunlugu(6));
        }

        [Fact]
        public void MaskeKaydiYoksa_VarsayilanMaskeKullaniliyor()
        {
            var maske = MaskeBilgisi.Coz(null);

            Assert.Equal(".", maske.Ayrac);
            Assert.Equal(3, maske.KebirSeviyesi);
            Assert.Equal(6, maske.MaksimumSeviye);
        }

        [Fact]
        public void FirmaBazliFarkliMaske_SegmentUzunluklariniDegistiriyor()
        {
            var maske = MaskeBilgisi.Coz(new KodMaskesi { SegmentUzunluk = "3,3", Ayrac = "-" });

            Assert.Equal("-", maske.Ayrac);
            Assert.Equal(4, maske.MaksimumSeviye);
            Assert.Equal(3, maske.SegmentUzunlugu(4));
        }

        [Fact]
        public void MaskeninIzinVerdiginden_DahaDerinSeviyeIstenirse_HataDonuyor()
        {
            var maske = MaskeBilgisi.Coz(new KodMaskesi { SegmentUzunluk = "3,2", Ayrac = "." });

            Assert.Throws<MuhasebeKuralException>(() => maske.SegmentUzunlugu(5));
        }

        [Fact]
        public void GecersizMaske_Reddediliyor()
        {
            Assert.Throws<MuhasebeKuralException>(
                () => MaskeBilgisi.Coz(new KodMaskesi { SegmentUzunluk = "abc", Ayrac = "." }));
        }

        [Theory]
        [InlineData(1, HesapTuru.Sinif)]
        [InlineData(2, HesapTuru.Grup)]
        [InlineData(3, HesapTuru.Kebir)]
        [InlineData(4, HesapTuru.Muavin)]
        [InlineData(6, HesapTuru.Muavin)]
        public void SeviyedenHesapTuru_DogruCikariliyor(int seviye, HesapTuru beklenen)
        {
            var maske = MaskeBilgisi.Coz(null);

            Assert.Equal(beklenen, maske.TuruBul((byte)seviye));
        }

        [Theory]
        [InlineData('1', HesapKarakter.Aktif)]
        [InlineData('2', HesapKarakter.Aktif)]
        [InlineData('5', HesapKarakter.Pasif)]
        [InlineData('6', HesapKarakter.Gelir)]
        [InlineData('7', HesapKarakter.Gider)]
        [InlineData('8', HesapKarakter.Maliyet)]
        [InlineData('9', HesapKarakter.Nazim)]
        public void KokSiniftan_KarakterTuretiliyor(char rakam, HesapKarakter beklenen)
            => Assert.Equal(beklenen, MaskeBilgisi.KokKarakter(rakam));
    }
}
