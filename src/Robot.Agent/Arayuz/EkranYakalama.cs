using System.Runtime.Versioning;

namespace PkfRobot.Arayuz;

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
    /// <summary>Tum ekranin goruntusu; verilen noktaya nisan cizilir.</summary>
    public static Bitmap Al(int nisanX, int nisanY)
    {
        var alan = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);

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
