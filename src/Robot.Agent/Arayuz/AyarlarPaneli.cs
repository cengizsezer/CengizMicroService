using System.Runtime.Versioning;
using PkfRobot.Ayarlar;

namespace PkfRobot.Arayuz;

/// <summary>
/// Ayarlar sekmesi. Alanlar <see cref="AyarTanimlari"/> listesinden
/// <b>uretiliyor</b>: yeni bir ayar eklemek icin buraya kutu koymak gerekmiyor.
///
/// Sifreler bu formda ama <b>ayni dosyada degil</b> -- yildizli gosteriliyor,
/// DPAPI ile ayri bir dosyaya yaziliyor ve yedege girmiyor
/// (bkz. <see cref="SifreDeposu"/>).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AyarlarPaneli : UserControl
{
    private readonly ArayuzBaglami _baglam;
    private readonly Dictionary<string, TextBox> _kutular = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Label> _uyarilar = new(StringComparer.OrdinalIgnoreCase);

    private readonly TextBox _orkaSifresi = SifreKutusu();
    private readonly TextBox _firmaSifresi = SifreKutusu();

    private readonly CheckBox _acilistaBaglan = new()
    {
        Text = "Uygulama acilinca ajan baglantisini baslat",
        AutoSize = true
    };

    private readonly CheckBox _kapatincaTepsiye = new()
    {
        Text = "Kapatma dugmesi uygulamayi kapatmasin, tepsiye indirsin",
        AutoSize = true
    };

    private readonly FlowLayoutPanel _icerik = new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
        Padding = new Padding(8)
    };

    public AyarlarPaneli(ArayuzBaglami baglam)
    {
        _baglam = baglam;
        Dock = DockStyle.Fill;

        Controls.Add(_icerik);
        Controls.Add(AltCubuk());

        _icerik.Controls.Add(Baslik("Yollar"));
        foreach (var tanim in AyarTanimlari.Yollar) _icerik.Controls.Add(Satir(tanim));

        _icerik.Controls.Add(Baslik("ORKA giris bilgileri"));
        foreach (var tanim in AyarTanimlari.OrkaGirisi) _icerik.Controls.Add(Satir(tanim));

        _icerik.Controls.Add(SifreSatiri("ORKA sifresi",
            "Giris ekraninda girilen sifre. Diskte DPAPI ile sifreli durur, loglara yazilmaz.",
            _orkaSifresi));

        _icerik.Controls.Add(SifreSatiri("Firma sifresi",
            "ORKA firma acarken ikinci kez sordugu sifre.", _firmaSifresi));

        _icerik.Controls.Add(Baslik("Uygulama"));
        _icerik.Controls.Add(OnayKutulari());

        _baglam.AyarlarDegisti += Yukle;
        Yukle();
    }

    /// <summary>Ayarlari forma basar. Yedek geri yuklendiginde de cagriliyor.</summary>
    public void Yukle()
    {
        foreach (var tanim in AyarTanimlari.Tumu)
        {
            if (!_kutular.TryGetValue(tanim.Anahtar, out var kutu)) continue;
            kutu.Text = tanim.Oku(_baglam.Ayarlar);
            UyariyiTazele(tanim);
        }

        _orkaSifresi.Text = _baglam.Sifreler.OrkaSifresi;
        _firmaSifresi.Text = _baglam.Sifreler.FirmaSifresi;

        _acilistaBaglan.Checked = _baglam.Ayarlar.AcilistaBaglan;
        _kapatincaTepsiye.Checked = _baglam.Ayarlar.KapatinceTepsiyeIn;
    }

    private void Kaydet()
    {
        foreach (var tanim in AyarTanimlari.Tumu)
        {
            if (!_kutular.TryGetValue(tanim.Anahtar, out var kutu)) continue;
            tanim.Yaz(_baglam.Ayarlar, kutu.Text.Trim());
        }

        _baglam.Ayarlar.AcilistaBaglan = _acilistaBaglan.Checked;
        _baglam.Ayarlar.KapatinceTepsiyeIn = _kapatincaTepsiye.Checked;

        _baglam.AyarlariKaydet();
        _baglam.SifreleriKaydet(new Sifreler
        {
            OrkaSifresi = _orkaSifresi.Text,
            FirmaSifresi = _firmaSifresi.Text
        });

        foreach (var tanim in AyarTanimlari.Tumu) UyariyiTazele(tanim);

        MessageBox.Show(this,
            $"Ayarlar kaydedildi.\n\n{_baglam.AyarDeposu.Dosya}\n" +
            $"{_baglam.SifreDeposu.Dosya} (sifreli)",
            "PkfRobot", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void UyariyiTazele(AyarTanimi tanim)
    {
        if (!_uyarilar.TryGetValue(tanim.Anahtar, out var etiket)) return;
        if (!_kutular.TryGetValue(tanim.Anahtar, out var kutu)) return;

        var sorun = YolDogrulama.Sorun(tanim, kutu.Text.Trim());

        etiket.Text = sorun ?? string.Empty;
        etiket.Visible = sorun is not null;
        etiket.ForeColor = YolDogrulama.Engelleyici(tanim, kutu.Text.Trim())
            ? Color.FromArgb(170, 30, 30)
            : Color.FromArgb(170, 110, 20);
    }

    // ---- duzen ----

    private Control Satir(AyarTanimi tanim)
    {
        var kutu = new TextBox { Width = 300 };
        var uyari = new Label
        {
            AutoSize = true,
            Visible = false,
            Font = new Font("Segoe UI", 8F),
            Margin = new Padding(3, 0, 3, 4)
        };

        _kutular[tanim.Anahtar] = kutu;
        _uyarilar[tanim.Anahtar] = uyari;

        kutu.TextChanged += (_, _) => UyariyiTazele(tanim);

        var satir = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0)
        };

        satir.Controls.Add(kutu);

        if (tanim.YolMu)
        {
            var gozat = new Button { Text = "Gozat...", Width = 80 };
            gozat.Click += (_, _) => Gozat(tanim, kutu);
            satir.Controls.Add(gozat);
        }

        var kap = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 8)
        };

        kap.Controls.Add(new Label { Text = tanim.Etiket, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
        kap.Controls.Add(satir);
        kap.Controls.Add(new Label
        {
            Text = tanim.Aciklama,
            AutoSize = true,
            MaximumSize = new Size(390, 0),
            ForeColor = Color.DimGray,
            Font = new Font("Segoe UI", 8F)
        });
        kap.Controls.Add(uyari);

        return kap;
    }

    private void Gozat(AyarTanimi tanim, TextBox kutu)
    {
        if (tanim.Tip == AyarTipi.Dosya)
        {
            using var secici = new OpenFileDialog
            {
                Title = tanim.Etiket,
                Filter = "Program (*.exe)|*.exe|Tum dosyalar (*.*)|*.*",
                CheckFileExists = false
            };

            if (Directory.Exists(Path.GetDirectoryName(kutu.Text)))
                secici.InitialDirectory = Path.GetDirectoryName(kutu.Text);

            if (secici.ShowDialog(this) == DialogResult.OK) kutu.Text = secici.FileName;
            return;
        }

        using var klasor = new FolderBrowserDialog { Description = tanim.Etiket, UseDescriptionForTitle = true };
        if (Directory.Exists(kutu.Text)) klasor.SelectedPath = kutu.Text;
        if (klasor.ShowDialog(this) == DialogResult.OK) kutu.Text = klasor.SelectedPath;
    }

    private static Control SifreSatiri(string etiket, string aciklama, TextBox kutu)
    {
        var kap = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 8)
        };

        var goster = new CheckBox { Text = "Goster", AutoSize = true, Margin = new Padding(6, 4, 0, 0) };
        goster.CheckedChanged += (_, _) => kutu.UseSystemPasswordChar = !goster.Checked;

        var satir = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0)
        };
        satir.Controls.Add(kutu);
        satir.Controls.Add(goster);

        kap.Controls.Add(new Label { Text = etiket, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
        kap.Controls.Add(satir);
        kap.Controls.Add(new Label
        {
            Text = aciklama,
            AutoSize = true,
            MaximumSize = new Size(390, 0),
            ForeColor = Color.DimGray,
            Font = new Font("Segoe UI", 8F)
        });

        return kap;
    }

    private Control OnayKutulari()
    {
        var kap = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 8)
        };

        kap.Controls.Add(_acilistaBaglan);
        kap.Controls.Add(_kapatincaTepsiye);
        return kap;
    }

    private Control AltCubuk()
    {
        var cubuk = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(8, 6, 8, 6) };

        var kaydet = new Button { Text = "Kaydet", Width = 90 };
        kaydet.Click += (_, _) => Kaydet();

        var yedekle = new Button { Text = "Yedekle...", Width = 90 };
        yedekle.Click += (_, _) => Yedekle();

        var geriYukle = new Button { Text = "Geri yukle...", Width = 100 };
        geriYukle.Click += (_, _) => GeriYukle();

        cubuk.Controls.Add(kaydet);
        cubuk.Controls.Add(yedekle);
        cubuk.Controls.Add(geriYukle);
        return cubuk;
    }

    private void Yedekle()
    {
        using var secici = new SaveFileDialog
        {
            Title = "Ayar yedegi",
            Filter = "PkfRobot yedegi (*.json)|*.json",
            FileName = $"pkfrobot-ayarlar-{DateTime.Now:yyyyMMdd}.json"
        };

        if (secici.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            // Once formdaki degerler kaydediliyor: kullanicinin ekranda gordugu
            // ama henuz kaydetmedigi degerlerin yedege girmemesi sasirtici olurdu.
            foreach (var tanim in AyarTanimlari.Tumu)
                if (_kutular.TryGetValue(tanim.Anahtar, out var kutu))
                    tanim.Yaz(_baglam.Ayarlar, kutu.Text.Trim());

            _baglam.AyarlariKaydet();
            _baglam.AyarDeposu.Yedekle(secici.FileName);

            MessageBox.Show(this,
                "Ayarlar ve kalibrasyon yedeklendi.\n\n" +
                "Yedekte SIFRE YOK: sifreler makineye baglidir ve yeni makinede " +
                "yeniden girilmelidir.",
                "PkfRobot", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Yedek alinamadi: {ex.Message}", "PkfRobot",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void GeriYukle()
    {
        using var secici = new OpenFileDialog
        {
            Title = "Ayar yedegi",
            Filter = "PkfRobot yedegi (*.json)|*.json|Tum dosyalar (*.*)|*.*"
        };

        if (secici.ShowDialog(this) != DialogResult.OK) return;

        if (MessageBox.Show(this,
                "Kayitli ayarlar ve kalibrasyon yedektekilerle degistirilecek. Devam edilsin mi?",
                "PkfRobot", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        try
        {
            _baglam.YedektenYukle(secici.FileName);
            var rapor = _baglam.KalibrasyonuUygula();

            MessageBox.Show(this,
                $"Yedek geri yuklendi. {rapor.Uygulanan} koordinat gorev dosyalarina yazildi.\n" +
                "Sifreler yedege girmediginden yeniden girilmeli.",
                "PkfRobot", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Yedek geri yuklenemedi: {ex.Message}", "PkfRobot",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static Label Baslik(string metin) => new()
    {
        Text = metin,
        AutoSize = true,
        Font = new Font("Segoe UI", 10F, FontStyle.Bold),
        Margin = new Padding(0, 6, 0, 8)
    };

    private static TextBox SifreKutusu() => new() { Width = 220, UseSystemPasswordChar = true };
}
