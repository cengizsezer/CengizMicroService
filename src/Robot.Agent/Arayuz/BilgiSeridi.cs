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

    private void Yerlestir()
    {
        var ekran = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        Width = ekran.Width;
        Height = 34;
        Location = new Point(ekran.Left, ekran.Top);
    }
}
