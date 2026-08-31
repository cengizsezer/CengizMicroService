using System.Runtime.Versioning;
using PkfRobot.Ayarlar;

namespace PkfRobot.Arayuz;

/// <summary>
/// PkfRobot penceresi: durum, ayarlar, kalibrasyon.
///
/// <b>Kapatma dugmesi uygulamayi kapatmiyor</b>, tepsiye indiriyor. Ajanin isi
/// gun boyu bagli kalmak; pencereyi kapatan birinin robotu da kapatmasi
/// beklenmez ve bunun farkina ancak sunucudan is gonderilemedigi zaman
/// varilirdi. Cikis tepsi menusunden.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AnaForm : Form
{
    private readonly ArayuzBaglami _baglam;
    private readonly DurumPaneli _durum;
    private readonly AyarlarPaneli _ayarlar;
    private readonly KalibrasyonPaneli _kalibrasyon;

    private readonly NotifyIcon _tepsi = new();
    private readonly System.Windows.Forms.Timer _zamanlayici = new() { Interval = 1000 };
    private readonly CheckBox _herZamanUstte = new() { Text = "Her zaman ustte", AutoSize = true };

    private readonly Dictionary<BaglantiDurumu, Icon> _simgeler = new();

    private bool _gercektenKapat;

    public AnaForm(ArayuzBaglami baglam)
    {
        _baglam = baglam;

        Text = "PkfRobot";
        // Hesap makinesi boyu: masaustunun kosesinde durup yol gostersin,
        // ekrani kaplamasin.
        ClientSize = new Size(470, 620);
        MinimumSize = new Size(430, 470);
        StartPosition = FormStartPosition.CenterScreen;

        _durum = new DurumPaneli(_baglam);
        _ayarlar = new AyarlarPaneli(_baglam);
        _kalibrasyon = new KalibrasyonPaneli(_baglam);

        var sekmeler = new TabControl { Dock = DockStyle.Fill };
        sekmeler.TabPages.Add(Sekme("Durum", _durum));
        sekmeler.TabPages.Add(Sekme("Ayarlar", _ayarlar));
        sekmeler.TabPages.Add(Sekme("Kalibrasyon", _kalibrasyon));

        Controls.Add(sekmeler);
        Controls.Add(UstCubuk());

        SimgeleriHazirla();
        TepsiyiHazirla();

        _herZamanUstte.CheckedChanged += (_, _) =>
        {
            TopMost = _herZamanUstte.Checked;
            _baglam.Ayarlar.HerZamanUstte = _herZamanUstte.Checked;
            _baglam.AyarDeposu.Yaz(_baglam.Ayarlar);
        };

        _zamanlayici.Tick += (_, _) =>
        {
            _baglam.Kopru.DurumuTazele();
            _durum.Tazele();
            TepsiyiTazele();
        };

        Load += (_, _) => Acilis();
        FormClosing += Kapanirken;
    }

    private void Acilis()
    {
        _herZamanUstte.Checked = _baglam.Ayarlar.HerZamanUstte;
        TopMost = _baglam.Ayarlar.HerZamanUstte;

        _baglam.CalisirYollariHazirla();

        // Publish gorev dosyalarinin uzerine yaziyor; kayitli kalibrasyon her
        // acilista geri uygulaniyor (bkz. KalibrasyonUygulama).
        var rapor = _baglam.KalibrasyonuUygula();
        if (rapor.Uygulanan > 0)
            _baglam.Log.Bilgi($"Kalibrasyon gorev dosyalarina uygulandi: {rapor.Uygulanan} koordinat.");

        foreach (var sorun in rapor.Sorunlular)
            _baglam.Log.Uyari($"Kalibrasyon: {sorun.Mesaj}");

        _kalibrasyon.Tazele();

        if (_baglam.Ayarlar.AcilistaBaglan) _baglam.Kopru.Baslat();

        _zamanlayici.Start();
        TepsiyiTazele();
    }

    private void Kapanirken(object? gonderen, FormClosingEventArgs e)
    {
        // Windows kapaniyorsa ya da tepsi menusunden cikildiysa gercekten kapan.
        if (_gercektenKapat || e.CloseReason != CloseReason.UserClosing) return;
        if (!_baglam.Ayarlar.KapatinceTepsiyeIn) return;

        e.Cancel = true;
        TepsiyeIn();
    }

    private void TepsiyeIn()
    {
        Hide();
        ShowInTaskbar = false;

        _tepsi.ShowBalloonTip(2500, "PkfRobot",
            "Arka planda calismaya devam ediyor. Cikmak icin tepsi simgesine sag tiklayin.",
            ToolTipIcon.Info);
    }

    private void Goster()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void Cik()
    {
        _gercektenKapat = true;
        _zamanlayici.Stop();

        // Ajan once duzgun kapatiliyor: sureci aniden birakmak sunucuda zaman
        // asimina kadar "calisiyor" gorunecek bir is birakirdi.
        _baglam.Kopru.Durdur();

        _tepsi.Visible = false;
        Close();
    }

    // ---- tepsi ----

    private void TepsiyiHazirla()
    {
        var menu = new ContextMenuStrip();

        var goster = new ToolStripMenuItem("Goster", null, (_, _) => Goster());
        var baglanti = new ToolStripMenuItem("Baglan", null, (_, _) =>
        {
            if (_baglam.Kopru.Calisiyor) _baglam.Kopru.Durdur();
            else { _baglam.CalisirYollariHazirla(); _baglam.Kopru.Baslat(); }
            TepsiyiTazele();
        });

        menu.Opening += (_, _) =>
            baglanti.Text = _baglam.Kopru.Calisiyor ? "Baglantiyi durdur" : "Baglan";

        menu.Items.Add(goster);
        menu.Items.Add(baglanti);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Cikis", null, (_, _) => Cik()));

        _tepsi.ContextMenuStrip = menu;
        _tepsi.Visible = true;
        _tepsi.DoubleClick += (_, _) => Goster();
    }

    private void TepsiyiTazele()
    {
        var durum = _baglam.Kopru.Durum;

        if (_simgeler.TryGetValue(durum, out var simge)) _tepsi.Icon = simge;

        var calisan = _baglam.Izleyici.Calisan;
        var isSatiri = calisan is null ? string.Empty : $"\n{calisan.IsTipi} %{_baglam.Izleyici.Yuzde}";

        // Tepsi ipucu 63 karakterle sinirli; uzun metin sessizce kirpilir.
        var metin = $"PkfRobot - {Aciklama(durum)}{isSatiri}";
        _tepsi.Text = metin.Length > 60 ? metin[..60] : metin;
    }

    private static string Aciklama(BaglantiDurumu durum) => durum switch
    {
        BaglantiDurumu.Bagli => "bagli",
        BaglantiDurumu.Baglaniyor => "baglaniyor",
        BaglantiDurumu.Kopuk => "kopuk",
        _ => "kapali"
    };

    /// <summary>
    /// Tepsi simgeleri kodda ciziliyor: projede .ico dosyasi yok ve tek ihtiyac
    /// duyulan sey durum rengi. Uc simge bir kez uretilip saklaniyor -- her
    /// saniye yeni bir <c>HICON</c> uretmek tutamac sizdirirdi.
    /// </summary>
    private void SimgeleriHazirla()
    {
        _simgeler[BaglantiDurumu.Bagli] = Nokta(Color.FromArgb(30, 160, 70));
        _simgeler[BaglantiDurumu.Baglaniyor] = Nokta(Color.FromArgb(220, 160, 30));
        _simgeler[BaglantiDurumu.Kopuk] = Nokta(Color.FromArgb(200, 50, 50));
        _simgeler[BaglantiDurumu.Kapali] = Nokta(Color.FromArgb(130, 130, 130));

        Icon = _simgeler[BaglantiDurumu.Kapali];
    }

    private static Icon Nokta(Color renk)
    {
        using var resim = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(resim))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var firca = new SolidBrush(renk);
            using var kalem = new Pen(Color.FromArgb(240, 255, 255, 255), 2);
            g.FillEllipse(firca, 4, 4, 24, 24);
            g.DrawEllipse(kalem, 4, 4, 24, 24);
        }

        var tutamac = resim.GetHicon();
        try
        {
            // Icon.FromHandle tutamacin sahibi olmuyor; kopya alip tutamaci
            // hemen birakiyoruz.
            using var gecici = Icon.FromHandle(tutamac);
            return (Icon)gecici.Clone();
        }
        finally
        {
            YokEt(tutamac);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "DestroyIcon")]
    private static extern bool YokEt(IntPtr tutamac);

    // ---- duzen ----

    private Control UstCubuk()
    {
        var cubuk = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 30,
            Padding = new Padding(8, 5, 8, 0)
        };

        cubuk.Controls.Add(_herZamanUstte);
        return cubuk;
    }

    private static TabPage Sekme(string baslik, Control icerik)
    {
        var sayfa = new TabPage(baslik);
        sayfa.Controls.Add(icerik);
        return sayfa;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _zamanlayici.Dispose();
            _tepsi.Dispose();
            foreach (var simge in _simgeler.Values) simge.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// Ajan anahtarini soran kucuk pencere. Konsol modunda ayni sey yildizli olarak
/// terminalden soruluyor; arayuzde terminal olmadigi icin buradan.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AnahtarFormu : Form
{
    private readonly TextBox _kutu = new()
    {
        Width = 340,
        UseSystemPasswordChar = true,
        Location = new Point(16, 96)
    };

    public AnahtarFormu(string ayarKlasoru)
    {
        Text = "PkfRobot - ajan kurulumu";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(380, 176);
        MaximizeBox = false;
        MinimizeBox = false;

        Controls.Add(new Label
        {
            Text = "Bu makinede kayitli ajan anahtari yok.\n\n" +
                   "Anahtari DijitalMasraf > Yonetim > Ajanlar ekranindan alin " +
                   "(pkfr_ ile baslar). Bir kez girilir; " +
                   $"{ayarKlasoru} altinda sifreli durur.",
            Location = new Point(16, 12),
            Size = new Size(348, 76)
        });

        Controls.Add(_kutu);

        var tamam = new Button
        {
            Text = "Kaydet",
            DialogResult = DialogResult.OK,
            Location = new Point(196, 132),
            Width = 80
        };

        var iptal = new Button
        {
            Text = "Iptal",
            DialogResult = DialogResult.Cancel,
            Location = new Point(284, 132),
            Width = 80
        };

        Controls.Add(tamam);
        Controls.Add(iptal);

        AcceptButton = tamam;
        CancelButton = iptal;
    }

    public string? Anahtar => string.IsNullOrWhiteSpace(_kutu.Text) ? null : _kutu.Text.Trim();

    /// <summary>Anahtari sorar; iptal edilirse null doner.</summary>
    public static string? Sor(string ayarKlasoru)
    {
        using var form = new AnahtarFormu(ayarKlasoru);
        return form.ShowDialog() == DialogResult.OK ? form.Anahtar : null;
    }
}
