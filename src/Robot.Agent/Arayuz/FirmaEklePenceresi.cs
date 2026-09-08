using System.Runtime.Versioning;

namespace PkfRobot.Arayuz;

/// <summary>
/// "Firma ekle" penceresi: kod + aciklama + iki sifre.
///
/// <b>Neden ayri bir sekme degil, kucuk bir pencere:</b> firma ekleme yilda birkac
/// kez yapilan bir is; Calistir sekmesinin yaninda duran bir dugme yeterli.
/// Sekme acmak, her gun kullanilan ekrani bir kere bile kullanilmayacak alanlarla
/// buyutmek olurdu.
///
/// <b>Sifreler burada da diske duz metin gitmiyor.</b> Pencere yalnizca degerleri
/// tasiyor; kaydetme <see cref="CalistirPaneli"/> uzerinden DPAPI'li depoya
/// (<c>sifreler.dat</c>) yapiliyor ve hicbiri sunucuya gonderilmiyor.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FirmaEklePenceresi : Form
{
    private readonly TextBox _kod = new() { Width = 220 };
    private readonly TextBox _ad = new() { Width = 220 };
    private readonly TextBox _orkaSifre = new() { Width = 220, UseSystemPasswordChar = true };
    private readonly TextBox _firmaSifre = new() { Width = 220, UseSystemPasswordChar = true };

    public FirmaEklePenceresi()
    {
        Text = "Firma ekle";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(400, 230);

        var duzen = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(10),
            AutoSize = true
        };

        duzen.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        duzen.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        Satir(duzen, "Firma kodu:", _kod);
        Satir(duzen, "Aciklama:", _ad);
        Satir(duzen, "ORKA sifresi:", _orkaSifre);
        Satir(duzen, "Firma sifresi:", _firmaSifre);

        duzen.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = Color.DimGray,
            MaximumSize = new Size(360, 0),
            Margin = new Padding(0, 6, 0, 6),
            Text = "Firma kodu, ORKA'da F7'den sonra yazilan kod (ornek: 0001). " +
                   "Sifreler bos birakilabilir, sonradan Calistir sekmesinden de girilebilir."
        }, 0, 4);
        duzen.SetColumnSpan(duzen.GetControlFromPosition(0, 4)!, 2);

        var tamam = new Button { Text = "Ekle", DialogResult = DialogResult.OK, Width = 80 };
        var vazgec = new Button { Text = "Vazgec", DialogResult = DialogResult.Cancel, Width = 80 };

        var dugmeler = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 40,
            Padding = new Padding(0, 4, 10, 0)
        };

        dugmeler.Controls.Add(tamam);
        dugmeler.Controls.Add(vazgec);

        Controls.Add(duzen);
        Controls.Add(dugmeler);

        AcceptButton = tamam;
        CancelButton = vazgec;
    }

    public string Kod => _kod.Text.Trim();
    public string Ad => _ad.Text.Trim();
    public string OrkaSifresi => _orkaSifre.Text;
    public string FirmaSifresi => _firmaSifre.Text;

    private static void Satir(TableLayoutPanel duzen, string etiket, Control kutu)
    {
        var satir = duzen.RowCount++;

        duzen.Controls.Add(new Label
        {
            Text = etiket,
            AutoSize = true,
            Margin = new Padding(0, 6, 8, 0)
        }, 0, satir);

        duzen.Controls.Add(kutu, 1, satir);
    }
}
