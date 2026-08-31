using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using PkfRobot.Ajan;

namespace PkfRobot.Arayuz;

/// <summary>
/// Ana ekran: baglanti, ORKA, calisan is, son isler ve log.
///
/// Hepsi tek ekranda: ofiste "robot calisiyor mu" sorusunun cevabini bulmak icin
/// sekme degistirmek gerekmemeli.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DurumPaneli : UserControl
{
    private readonly ArayuzBaglami _baglam;

    private readonly Label _baglanti = Deger();
    private readonly Label _kalpAtisi = Deger();
    private readonly Label _orka = Deger();

    private readonly GroupBox _isKutusu = new() { Text = "Calisan is", Dock = DockStyle.Top, Height = 96 };
    private readonly Label _isBasligi = new() { Dock = DockStyle.Top, Height = 20, Padding = new Padding(6, 2, 6, 0) };
    private readonly ProgressBar _ilerleme = new() { Dock = DockStyle.Top, Height = 16, Margin = new Padding(6) };
    private readonly Label _isMesaji = new() { Dock = DockStyle.Fill, Padding = new Padding(6, 2, 6, 0), ForeColor = Color.DimGray };

    private readonly ListView _gecmis = new()
    {
        Dock = DockStyle.Top,
        Height = 116,
        View = View.Details,
        FullRowSelect = true,
        HeaderStyle = ColumnHeaderStyle.Nonclickable
    };

    private readonly TextBox _log = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        BackColor = Color.FromArgb(250, 250, 250),
        Font = new Font("Consolas", 8.25F)
    };

    private readonly Button _baglantiDugmesi = new() { Text = "Baglan", Width = 110, Dock = DockStyle.Left };

    public DurumPaneli(ArayuzBaglami baglam)
    {
        _baglam = baglam;
        Dock = DockStyle.Fill;
        Padding = new Padding(8);

        Controls.Add(LogBolumu());
        Controls.Add(_gecmis);
        Controls.Add(IsBolumu());
        Controls.Add(DurumBolumu());

        _gecmis.Columns.Add("Zaman", 70);
        _gecmis.Columns.Add("Is", 130);
        _gecmis.Columns.Add("Sonuc", 80);
        _gecmis.Columns.Add("Sure", 70);

        _baglantiDugmesi.Click += (_, _) => BaglantiyiCevir();

        // Log ve is olaylari ajanin is parcacigindan geliyor; ekrana dokunmadan
        // once UI is parcacigina gecmek zorunlu.
        _baglam.Log.SatirGeldi += satir => GuvenliCagir(() => LogEkle(satir));
        _baglam.Izleyici.Degisti += () => GuvenliCagir(Tazele);
        _baglam.Kopru.Degisti += () => GuvenliCagir(Tazele);

        LogYukle();
        Tazele();
    }

    /// <summary>Zamanlayicidan saniyede bir; "son kalp atisi" gecen sureyi gosteriyor.</summary>
    public void Tazele()
    {
        var durum = _baglam.Kopru.Durum;

        _baglanti.Text = durum switch
        {
            BaglantiDurumu.Bagli => "bagli",
            BaglantiDurumu.Baglaniyor => "baglaniyor...",
            BaglantiDurumu.Kopuk => "kopuk, yeniden deneniyor",
            _ => "kapali"
        };
        _baglanti.ForeColor = Renk(durum);

        _kalpAtisi.Text = _baglam.Kopru.SonKalpAtisi is { } atis
            ? $"{atis:HH:mm:ss} ({(int)(DateTime.Now - atis).TotalSeconds} sn once)"
            : "-";

        var orkaAcik = new OrkaSureci(_baglam.Config.Ajan.OrkaSurecAdi).CalisiyorMu();
        _orka.Text = orkaAcik ? "acik" : "kapali";
        _orka.ForeColor = orkaAcik ? Color.FromArgb(20, 120, 60) : Color.DimGray;

        _baglantiDugmesi.Text = _baglam.Kopru.Calisiyor ? "Durdur" : "Baglan";

        IsiTazele();
        GecmisiTazele();
    }

    private void IsiTazele()
    {
        var calisan = _baglam.Izleyici.Calisan;

        if (calisan is null)
        {
            _isBasligi.Text = "Calisan is yok.";
            _isBasligi.ForeColor = Color.DimGray;
            _ilerleme.Value = 0;
            _isMesaji.Text = string.Empty;
            return;
        }

        _isBasligi.Text = $"{calisan.IsTipi} · {calisan.Sure:mm\\:ss}";
        _isBasligi.ForeColor = Color.Black;
        _ilerleme.Value = Math.Clamp(_baglam.Izleyici.Yuzde, 0, 100);
        _isMesaji.Text = $"%{_ilerleme.Value} · {_baglam.Izleyici.IlerlemeMesaji}";
    }

    private void GecmisiTazele()
    {
        var isler = _baglam.Izleyici.SonIsler;

        // Satir sayisi degismediyse listeyi bastan kurmak, kullanicinin sectigi
        // satiri her saniye kaybettirirdi.
        if (_gecmis.Items.Count == isler.Count && isler.Count > 0 &&
            _gecmis.Items[0].SubItems[0].Text == Zaman(isler[0]))
            return;

        _gecmis.BeginUpdate();
        _gecmis.Items.Clear();

        foreach (var kayit in isler)
        {
            var satir = new ListViewItem(new[]
            {
                Zaman(kayit),
                kayit.IsTipi,
                kayit.SonucMetni,
                $"{kayit.Sure.TotalSeconds:0} sn"
            });

            if (kayit.Basarili == false)
            {
                satir.ForeColor = Color.FromArgb(160, 30, 30);
                if (!string.IsNullOrWhiteSpace(kayit.Mesaj)) satir.ToolTipText = kayit.Mesaj;
            }

            _gecmis.Items.Add(satir);
        }

        _gecmis.EndUpdate();
    }

    private static string Zaman(IsGecmisiKaydi kayit) => kayit.Basladi.ToString("dd.MM HH:mm");

    private void BaglantiyiCevir()
    {
        if (_baglam.Kopru.Calisiyor)
        {
            _baglantiDugmesi.Enabled = false;
            try { _baglam.Kopru.Durdur(); }
            finally { _baglantiDugmesi.Enabled = true; }
        }
        else
        {
            _baglam.CalisirYollariHazirla();
            _baglam.Kopru.Baslat();
        }

        Tazele();
    }

    private void LogYukle()
    {
        var metin = new StringBuilder();
        foreach (var satir in _baglam.Log.Satirlar) metin.AppendLine(satir);
        _log.Text = metin.ToString();
        SonaKaydir();
    }

    private void LogEkle(string satir)
    {
        _log.AppendText(satir + Environment.NewLine);
        SonaKaydir();
    }

    private void SonaKaydir()
    {
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    private void GuvenliCagir(Action eylem)
    {
        if (IsDisposed || !IsHandleCreated) return;

        try
        {
            if (InvokeRequired) BeginInvoke(eylem);
            else eylem();
        }
        catch (ObjectDisposedException)
        {
            // Pencere kapanirken gelen son bildirim; onemsiz.
        }
    }

    // ---- duzen ----

    private Control DurumBolumu()
    {
        var kutu = new GroupBox { Text = "Durum", Dock = DockStyle.Top, Height = 104 };

        var izgara = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(6, 2, 6, 2)
        };
        izgara.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        izgara.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        izgara.Controls.Add(Etiket("Hub baglantisi"), 0, 0);
        izgara.Controls.Add(_baglanti, 1, 0);
        izgara.Controls.Add(Etiket("Son kalp atisi"), 0, 1);
        izgara.Controls.Add(_kalpAtisi, 1, 1);
        izgara.Controls.Add(Etiket("ORKA"), 0, 2);
        izgara.Controls.Add(_orka, 1, 2);

        var dugmeler = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 28, Margin = new Padding(0, 2, 0, 0) };
        dugmeler.Controls.Add(_baglantiDugmesi);
        izgara.Controls.Add(dugmeler, 1, 3);

        kutu.Controls.Add(izgara);
        return kutu;
    }

    private Control IsBolumu()
    {
        _isKutusu.Controls.Add(_isMesaji);
        _isKutusu.Controls.Add(_ilerleme);
        _isKutusu.Controls.Add(_isBasligi);
        return _isKutusu;
    }

    private Control LogBolumu()
    {
        var kutu = new GroupBox { Text = "Log", Dock = DockStyle.Fill };

        var alt = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 30 };
        var ac = new Button { Text = "Log klasorunu ac", Width = 130 };
        ac.Click += (_, _) => KlasorAc(Path.Combine(AjanKimlikDeposu.VarsayilanKlasor, "logs"));

        var gorevLoglari = new Button { Text = "Gorev loglari", Width = 110 };
        gorevLoglari.Click += (_, _) => KlasorAc(_baglam.Ayarlar.LogKlasoru);

        alt.Controls.Add(ac);
        alt.Controls.Add(gorevLoglari);

        kutu.Controls.Add(_log);
        kutu.Controls.Add(alt);
        return kutu;
    }

    private void KlasorAc(string? klasor)
    {
        if (string.IsNullOrWhiteSpace(klasor)) return;

        try
        {
            Directory.CreateDirectory(klasor);
            Process.Start(new ProcessStartInfo(klasor) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Klasor acilamadi: {ex.Message}", "PkfRobot",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static Color Renk(BaglantiDurumu durum) => durum switch
    {
        BaglantiDurumu.Bagli => Color.FromArgb(20, 120, 60),
        BaglantiDurumu.Baglaniyor => Color.FromArgb(180, 120, 20),
        BaglantiDurumu.Kopuk => Color.FromArgb(170, 40, 40),
        _ => Color.DimGray
    };

    private static Label Etiket(string metin) => new()
    {
        Text = metin,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.DimGray
    };

    private static Label Deger() => new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        Font = new Font("Segoe UI", 9F, FontStyle.Bold)
    };
}
