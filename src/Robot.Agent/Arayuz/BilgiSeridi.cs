using System.Runtime.Versioning;

namespace PkfRobot.Arayuz;

/// <summary>
/// Ekranin ustunde duran ince bilgi seridi: "Hedefe tiklayin · Iptal icin Esc".
///
/// <b>Neden odak almiyor:</b> serit acilirken ORKA on planda olmali. Odagi
/// alsaydi ORKA arkaya duser, kullanici once ORKA'ya tiklayip onu one getirmek
/// zorunda kalir ve o ilk tiklama olcum olarak yakalanirdi.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class BilgiSeridi : Form
{
    private readonly Label _metin;

    public BilgiSeridi(string mesaj)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(28, 32, 38);

        _metin = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Text = mesaj
        };

        Controls.Add(_metin);
        Yerlestir();
    }

    /// <summary>Tiklama yakalayici calisirken serit odak almamali.</summary>
    protected override bool ShowWithoutActivation => true;

    /// <summary>
    /// Serit <b>tiklama gecirgen</b>: <c>WS_EX_TRANSPARENT</c>.
    ///
    /// Neden sart: serit ekranin en ustunde, tam genislikte duruyor -- ORKA tam
    /// ekranken menu ve arac cubugunun tam uzerinde. Gecirgen olmasaydi oraya
    /// yapilan olcum tiklamasinda <c>WindowFromPoint</c> ORKA'yi degil bu seridi
    /// dondururdu; secim "tiklanan pencere ORKA degil" diye reddedilir ve
    /// kullanici PkfRobot'un kendi seridine takildigini goremezdi.
    /// </summary>
    protected override CreateParams CreateParams
    {
        get
        {
            var p = base.CreateParams;
            p.ExStyle |= Win32.WS_EX_TRANSPARENT | Win32.WS_EX_NOACTIVATE;
            return p;
        }
    }

    public string Mesaj
    {
        get => _metin.Text;
        set => _metin.Text = value;
    }

    public void Uyar()
    {
        BackColor = Color.FromArgb(120, 40, 40);
        _metin.BackColor = BackColor;
    }

    /// <summary>
    /// Serit ORKA'nin bulundugu MONITORUN ustune konumlanir.
    ///
    /// Neden: kullanici olcum yaparken ORKA'ya bakiyor ve ORKA ikinci monitorde
    /// calisiyor. Serit birincil monitore aciliyordu; "Hedefe tiklayin · Iptal
    /// icin Esc" yazisi kullanicinin bakmadigi ekranda kaliyordu.
    ///
    /// ORKA bulunamazsa birincil monitor kullaniliyor -- serit icin dogru
    /// varsayilan bu; ekran goruntusu icin DEGIL (bkz. <see cref="YakalamaAlani"/>).
    /// </summary>
    private void Yerlestir()
    {
        var tutamac = YakalamaAlani.OrkaTutamaci();
        var ekran = tutamac != IntPtr.Zero ? Screen.FromHandle(tutamac) : Screen.PrimaryScreen;

        var alan = (ekran ?? Screen.PrimaryScreen)?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        Width = alan.Width;
        Height = 34;
        Location = new Point(alan.Left, alan.Top);
    }
}
