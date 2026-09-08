using PkfRobot.Arayuz;

namespace PkfRobot.UnitTests.Arayuz;

/// <summary>
/// Modul ekraninda "SAG x3" istenirken ilk SAG yutuluyordu. Sebep pencere bulma
/// degil, ONE GETIRMENIN DOGRULANMAMASIYDI: eski surum FlaUI'nin
/// <c>Focus()/SetForeground()</c> ciftini kullaniyordu ve FlaUI her cagrinin
/// ardindan <c>Wait.UntilResponsive</c> ile pencerenin mesaj kuyrugunu bekliyordu.
/// Ciplak <c>SetForegroundWindow</c>'a gecerken o bariyer dustu; cagri asenkron
/// oldugu icin ilk tus pencere one gelmeden gidiyordu.
///
/// Ekran test edilemez ama <b>dogrulama dongusu</b> edilebilir: butun Windows
/// cagrilari <see cref="OrkaPenceresi.OneGetirCekirdek"/>'e disaridan veriliyor.
/// </summary>
public class OneGetirmeTests
{
    private static readonly IntPtr Hedef = new(0x1234);
    private static readonly IntPtr Baskasi = new(0x9999);

    /// <summary>Sahte masaustu: on plandaki pencere, saat ve cagri sayaclari.</summary>
    private sealed class Masaustu
    {
        public IntPtr OnPlandaki = Baskasi;
        public long Saat;
        public bool CagriSonucu = true;

        /// <summary>Cagridan kac ms sonra pencere gercekten one gelsin? -1 = hic.</summary>
        public int OneGelmeGecikmesiMs = 0;

        public int CagriSayisi;
        public int UykuSayisi;
        public int YanitBeklemeSayisi;
        public readonly List<string> Olaylar = new();

        private long _cagriZamani = -1;

        public OneGetirmeRaporu Calistir(IntPtr hedef, int zamanAsimiMs = 1500, int aralikMs = 25)
            => OrkaPenceresi.OneGetirCekirdek(
                hedef,
                oneGetir: () =>
                {
                    CagriSayisi++;
                    _cagriZamani = Saat;
                    Olaylar.Add("cagri");
                    return CagriSonucu;
                },
                onPencere: () =>
                {
                    if (_cagriZamani >= 0 && OneGelmeGecikmesiMs >= 0 &&
                        Saat - _cagriZamani >= OneGelmeGecikmesiMs)
                        OnPlandaki = hedef;

                    return OnPlandaki;
                },
                yanitBekle: _ =>
                {
                    YanitBeklemeSayisi++;
                    Olaylar.Add("yanit-bekle");
                },
                uyu: ms =>
                {
                    UykuSayisi++;
                    Saat += ms;
                },
                saatMs: () => Saat,
                zamanAsimiMs: zamanAsimiMs,
                aralikMs: aralikMs);
    }

    [Fact]
    public void Pencere_one_gelene_kadar_yokluyor()
    {
        // 3 yoklama sonra one geliyor (25 ms araliklarla): dongunun asil isi bu.
        var masa = new Masaustu { OneGelmeGecikmesiMs = 75 };

        var rapor = masa.Calistir(Hedef);

        Assert.True(rapor.OndeMi);
        Assert.True(rapor.CagriKabulEdildi);
        Assert.False(rapor.ZatenOndeydi);
        Assert.Equal(3, masa.UykuSayisi);
        Assert.Equal(75, rapor.GecenMs);
    }

    [Fact]
    public void One_geldikten_SONRA_yanit_bariyeri_calisiyor()
    {
        // Sira onemli: bariyer pencere one gelmeden calissaydi eski durumdan
        // farkimiz kalmazdi -- beklenen sey aktivasyonun ISLENMESI.
        var masa = new Masaustu { OneGelmeGecikmesiMs = 50 };

        masa.Calistir(Hedef);

        Assert.Equal(new[] { "cagri", "yanit-bekle" }, masa.Olaylar);
        Assert.Equal(1, masa.YanitBeklemeSayisi);
    }

    [Fact]
    public void Zaten_ondeyse_one_getirme_cagrisi_yapilmiyor()
    {
        // Gereksiz aktivasyon ekrani titretiyor ve baska bir pencerenin modalini
        // one cekebiliyor. Bariyer yine de calismali: "onde" olmak "mesajlarini
        // isledi" demek degil.
        var masa = new Masaustu { OnPlandaki = Hedef };

        var rapor = masa.Calistir(Hedef);

        Assert.True(rapor.OndeMi);
        Assert.True(rapor.ZatenOndeydi);
        Assert.Equal(0, masa.CagriSayisi);
        Assert.Equal(1, masa.YanitBeklemeSayisi);
    }

    [Fact]
    public void Pencere_hic_one_gelmezse_sure_dolunca_pes_ediyor()
    {
        // Durdurmuyor: rapor donuyor, karari ve uyariyi cagiran veriyor.
        var masa = new Masaustu { OneGelmeGecikmesiMs = -1 };

        var rapor = masa.Calistir(Hedef, zamanAsimiMs: 200, aralikMs: 25);

        Assert.False(rapor.OndeMi);
        Assert.True(rapor.GecenMs >= 200);
        Assert.Equal(0, masa.YanitBeklemeSayisi);
        Assert.Equal(8, masa.UykuSayisi);
    }

    [Fact]
    public void Cagri_reddedilse_de_pencere_one_gelirse_ikisi_de_raporlaniyor()
    {
        // SetForegroundWindow foreground lock yuzunden false donebilir ama pencere
        // yine de one gelebilir. Tek bool'a sikistirilsaydi bu ayrim log'da
        // gorunmezdi.
        var masa = new Masaustu { CagriSonucu = false, OneGelmeGecikmesiMs = 25 };

        var rapor = masa.Calistir(Hedef);

        Assert.False(rapor.CagriKabulEdildi);
        Assert.True(rapor.OndeMi);
    }

    [Fact]
    public void Cagri_kabul_edilse_de_pencere_gelmeyebilir()
    {
        var masa = new Masaustu { CagriSonucu = true, OneGelmeGecikmesiMs = -1 };

        var rapor = masa.Calistir(Hedef, zamanAsimiMs: 100);

        Assert.True(rapor.CagriKabulEdildi);
        Assert.False(rapor.OndeMi);
    }

    [Fact]
    public void Tutamac_yoksa_hicbir_windows_cagrisi_yapilmiyor()
    {
        var masa = new Masaustu();

        var rapor = masa.Calistir(IntPtr.Zero);

        Assert.Equal(OneGetirmeRaporu.Gecersiz, rapor);
        Assert.Equal(0, masa.CagriSayisi);
        Assert.Equal(0, masa.UykuSayisi);
        Assert.Equal(0, masa.YanitBeklemeSayisi);
    }
}
