using System.Runtime.Versioning;
using PkfRobot.Config;

namespace PkfRobot.Arayuz;

/// <summary>
/// Ekran goruntusu alinacak dikdortgeni bulan TEK nokta.
///
/// <b>Neden birincil monitor degil:</b> ORKA ikinci monitorde calisiyor -- ana
/// pencerenin rect'i Sol=2390'dan basliyor. <c>Screen.PrimaryScreen</c> ve
/// FlaUI'nin <c>Capture.Screen()</c> cagrisi birincil monitoru cekiyor, yani
/// gorev ekran goruntuleri ORKA'nin hic gorunmedigi bir ekrani gosteriyordu:
/// "sablon-secildi.png'yi her calistirmada kontrol et" talimati bos bir
/// goruntuye bakiyordu.
///
/// <b>Neden tum sanal masaustu de degil:</b> ilgilendigimiz sey ORKA'nin
/// penceresi; iki monitoru birden cekmek dosyayi buyutuyor ve grid'i okunmaz
/// hale getiriyor. Sanal masaustu yalnizca ORKA bulunamadiginda YEDEK.
/// Birincil monitore HICBIR durumda dusulmuyor -- hatanin kendisi oydu.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class YakalamaAlani
{
    private static readonly object Kilit = new();
    private static OrkaPenceresi? _orka;

    /// <summary>ORKA ana penceresinin tutamaci; bulunamazsa <see cref="IntPtr.Zero"/>.</summary>
    public static IntPtr OrkaTutamaci()
    {
        try
        {
            return Orka().Durum().Tutamac;
        }
        catch (Exception)
        {
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// Yakalanacak dikdortgen: once ORKA ana penceresi, bulunamazsa butun
    /// monitorleri kapsayan sanal masaustu.
    ///
    /// Olcu <see cref="OrkaPenceresi.OlcuAl"/>'dan geliyor -- tiklama oraninin
    /// paydasi ile ekran goruntusunun cercevesi ayni dikdortgen olsun; ikisi
    /// ayrilirsa goruntuye bakip "robot buraya tikladi" demek imkansizlasir.
    /// </summary>
    public static Rectangle Alan()
    {
        var olcu = OrkaPenceresi.OlcuAl(OrkaTutamaci());
        if (olcu.Gecerli)
            return new Rectangle(olcu.Sol, olcu.Ust, olcu.Genislik, olcu.Yukseklik);

        // Sanal masaustu butun monitorleri kapsar; Sol/Ust NEGATIF olabilir
        // (ikinci monitor solda ya da yukarida ise). Deger kirpilmiyor.
        var sanal = SystemInformation.VirtualScreen;
        return sanal is { Width: > 0, Height: > 0 } ? sanal : new Rectangle(0, 0, 1920, 1080);
    }

    /// <summary>
    /// ORKA'yi bulan yardimci; bir kez kurulup saklaniyor (pencere her cagrida
    /// yeniden araniyor, saklanan yalniz surec adi ile baslik).
    ///
    /// <b>Ayar neden buradan okunuyor:</b> <c>AdimLogger</c> ile
    /// <see cref="EkranYakalama"/>'nin cagri imzalari degismedi ve ikisinin de
    /// elinde config yok. Surec adi ile ana ekran basligi yine tek yerde
    /// (<see cref="RobotConfig"/>) tanimli; burada kopyasi tutulmuyor.
    /// </summary>
    private static OrkaPenceresi Orka()
    {
        lock (Kilit)
        {
            if (_orka is not null) return _orka;

            var cfg = new RobotConfig();
            try
            {
                // --config ile baska bir ayar dosyasi verilmis olabilir;
                // Program.cs hangisini okuduysa burada da o okunmali. Arguman
                // cozumu kopyalanmiyor, ayni cozucu cagriliyor.
                var yol = Parametreler.Coz(Environment.GetCommandLineArgs().Skip(1).ToArray())
                              .ConfigYolu
                          ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");

                cfg = RobotConfig.Yukle(yol);
            }
            catch (Exception)
            {
                // Ayar okunamadi: koddaki varsayilanlarla devam. Surec adi
                // tutmazsa ORKA bulunamaz ve sanal masaustune dusulur -- ikinci
                // monitor yine goruntuye giriyor, eski hata geri gelmiyor.
            }

            _orka = new OrkaPenceresi(cfg.Ajan.OrkaSurecAdi, cfg.Pencereler.AnaEkran);
            return _orka;
        }
    }
}

/// <summary>
/// "Dene" sonrasi ekran goruntusu ve uzerine cizilen nisan.
///
/// <b>Neden goruntu sart:</b> ORKA'nin gridi UI Automation'a kapali (bkz.
/// OKUBENI); robot yazdigi degerin dogru yere gittigini ekrandan goremiyor.
/// Kalibrasyonu dogrulamanin tek yolu, tiklanan noktayi goz ile gormek.
/// </summary>
[SupportedOSPlatform("windows")]
public static class EkranYakalama
{
    /// <summary>
    /// ORKA ana penceresinin goruntusu (bulunamazsa butun monitorler); verilen
    /// noktaya nisan cizilir. Nisan alanin sol-ust kosesine goreli cevriliyor,
    /// bu yuzden dikdortgenin ekranda nerede durdugu onemli degil.
    /// </summary>
    public static Bitmap Al(int nisanX, int nisanY)
    {
        var alan = YakalamaAlani.Alan();

        var goruntu = new Bitmap(alan.Width, alan.Height);
        using (var g = Graphics.FromImage(goruntu))
        {
            g.CopyFromScreen(alan.Left, alan.Top, 0, 0, alan.Size);
            Nisan(g, nisanX - alan.Left, nisanY - alan.Top);
        }

        return goruntu;
    }

    private static void Nisan(Graphics g, int x, int y)
    {
        const int yaricap = 22;

        using var disKalem = new Pen(Color.White, 4);
        using var icKalem = new Pen(Color.FromArgb(230, 40, 40), 2);

        foreach (var kalem in new[] { disKalem, icKalem })
        {
            g.DrawEllipse(kalem, x - yaricap, y - yaricap, yaricap * 2, yaricap * 2);
            g.DrawLine(kalem, x - yaricap - 8, y, x - 6, y);
            g.DrawLine(kalem, x + 6, y, x + yaricap + 8, y);
            g.DrawLine(kalem, x, y - yaricap - 8, x, y - 6);
            g.DrawLine(kalem, x, y + 6, x, y + yaricap + 8);
        }
    }
}

/// <summary>
/// Deneme sonucunu gosteren pencere: ekran goruntusu ve tiklanan nokta.
/// Goruntu buyuk oldugu icin pencereye sigdiriliyor; tiklama ile 1:1 gorunume
/// gecilebiliyor.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DenemePenceresi : Form
{
    private readonly PictureBox _resim;

    public DenemePenceresi(Bitmap goruntu, string baslik)
    {
        Text = baslik;
        StartPosition = FormStartPosition.CenterParent;
        Width = 900;
        Height = 620;
        MinimizeBox = false;

        _resim = new PictureBox
        {
            Dock = DockStyle.Fill,
            Image = goruntu,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(24, 24, 24)
        };

        var alt = new Panel { Dock = DockStyle.Bottom, Height = 40 };

        var aciklama = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Nisan dogru yerde mi? Degilse koordinati yeniden secin.",
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };

        var buyut = new CheckBox
        {
            Text = "1:1 goster",
            Dock = DockStyle.Right,
            Width = 100,
            Appearance = Appearance.Button,
            TextAlign = ContentAlignment.MiddleCenter
        };
        buyut.CheckedChanged += (_, _) =>
        {
            _resim.SizeMode = buyut.Checked ? PictureBoxSizeMode.AutoSize : PictureBoxSizeMode.Zoom;
            AutoScroll = buyut.Checked;
        };

        var kapat = new Button { Text = "Kapat", Dock = DockStyle.Right, Width = 90, DialogResult = DialogResult.OK };

        alt.Controls.Add(aciklama);
        alt.Controls.Add(buyut);
        alt.Controls.Add(kapat);

        Controls.Add(_resim);
        Controls.Add(alt);

        AcceptButton = kapat;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _resim.Image?.Dispose();
        base.Dispose(disposing);
    }
}
