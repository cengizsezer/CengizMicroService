using System.Runtime.Versioning;
using FlaUI.Core.Input;
using PkfRobot.Ayarlar;

namespace PkfRobot.Arayuz;

/// <summary>
/// Kalibrasyon sekmesi: gorev dosyalarindaki her <c>Tikla</c> adimi icin bir
/// satir; ad, mevcut deger, "Sec" ve "Dene".
///
/// Satirlar <b>gorev JSON'larindan turetiliyor</b> (<see cref="KoordinatKesfi"/>);
/// burada elle yazilmis liste yok. Yeni bir gorev dosyasi eklenince satirlar
/// kendiliginden cikiyor.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class KalibrasyonPaneli : UserControl
{
    private readonly ArayuzBaglami _baglam;

    private readonly FlowLayoutPanel _liste = new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
        Padding = new Padding(8)
    };

    private readonly Label _ozet = new()
    {
        Dock = DockStyle.Top,
        Height = 40,
        Padding = new Padding(8, 6, 8, 0),
        ForeColor = Color.DimGray
    };

    private TiklamaYakalayici? _yakalayici;
    private BilgiSeridi? _serit;

    public KalibrasyonPaneli(ArayuzBaglami baglam)
    {
        _baglam = baglam;
        Dock = DockStyle.Fill;

        Controls.Add(_liste);
        Controls.Add(_ozet);
        Controls.Add(AltCubuk());

        _baglam.AyarlarDegisti += Tazele;
        Tazele();
    }

    /// <summary>Satirlari gorev dosyalarindan yeniden okur.</summary>
    public void Tazele()
    {
        _liste.SuspendLayout();
        _liste.Controls.Clear();

        var kayitlar = KoordinatKesfi.Kesfet(_baglam.GorevlerKlasoru);

        if (kayitlar.Count == 0)
        {
            _liste.Controls.Add(new Label
            {
                Text = $"Gorev dosyalarinda Tikla adimi bulunamadi.\n{_baglam.GorevlerKlasoru}",
                AutoSize = true,
                ForeColor = Color.DimGray
            });
        }

        string? oncekiDosya = null;
        foreach (var kayit in kayitlar)
        {
            if (kayit.DosyaAdi != oncekiDosya)
            {
                _liste.Controls.Add(new Label
                {
                    Text = $"{kayit.DosyaAdi}  —  {kayit.GorevAdi}",
                    AutoSize = true,
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    Margin = new Padding(0, 10, 0, 4)
                });
                oncekiDosya = kayit.DosyaAdi;
            }

            _liste.Controls.Add(Satir(kayit));
        }

        _ozet.Text = $"{kayitlar.Count} koordinat · kayitli olcum: {_baglam.Ayarlar.Koordinatlar.Count}\n" +
                     $"Olculen degerler {_baglam.AyarDeposu.Dosya} icinde saklanir ve gorev dosyalarina yazilir.";

        _liste.ResumeLayout();
    }

    private Control Satir(KoordinatKaydi kayit)
    {
        var kayitli = _baglam.Ayarlar.Koordinat(kayit.Anahtar);

        var deger = new Label
        {
            AutoSize = true,
            Font = new Font("Consolas", 9F),
            Margin = new Padding(0, 6, 8, 0),
            Text = Degeri(kayit, kayitli)
        };

        var sec = new Button { Text = "Sec", Width = 64 };
        var dene = new Button { Text = "Dene", Width = 64 };

        sec.Click += (_, _) => Sec(kayit, deger);
        dene.Click += (_, _) => Dene(kayit);

        var dugmeler = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0)
        };
        dugmeler.Controls.Add(deger);
        dugmeler.Controls.Add(sec);
        dugmeler.Controls.Add(dene);

        var kap = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 10),
            BorderStyle = BorderStyle.None
        };

        kap.Controls.Add(new Label
        {
            Text = kayit.Etiket,
            AutoSize = true,
            MaximumSize = new Size(400, 0),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        });
        kap.Controls.Add(dugmeler);

        if (!string.IsNullOrWhiteSpace(kayit.HedefPencere))
        {
            kap.Controls.Add(new Label
            {
                Text = $"hedef pencere: {kayit.HedefPencere}",
                AutoSize = true,
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8F)
            });
        }

        return kap;
    }

    private string Degeri(KoordinatKaydi kayit, KoordinatAyari? kayitli)
    {
        var dosyada = $"{OranDonusturucu.Yaz(kayit.X)} x {OranDonusturucu.Yaz(kayit.Y)}";

        if (kayitli is null) return $"{dosyada}  (gorev dosyasindan)";

        var olculen = $"{OranDonusturucu.Yaz(kayitli.X)} x {OranDonusturucu.Yaz(kayitli.Y)}";
        return olculen == dosyada
            ? $"{dosyada}  ({kayitli.Olculdu:dd.MM.yyyy} olculdu)"
            : $"{olculen}  (olculdu; dosyada {dosyada})";
    }

    // ---- secme akisi ----

    private void Sec(KoordinatKaydi kayit, Label deger)
    {
        var durum = _baglam.Orka.Durum();
        if (!durum.Bulundu)
        {
            Uyar("ORKA acik degil. Kalibre edilecek ekrana kadar ORKA'da elle gidin, " +
                 "sonra Sec'e basin.");
            return;
        }

        if (!durum.TamEkran)
        {
            var cevap = MessageBox.Show(this,
                "ORKA tam ekran degil. Koordinatlar pencereye oranla olculuyor ve robot " +
                "tiklamadan once pencereyi buyutuyor; simdi olculen deger kayabilir.\n\n" +
                "Yine de devam edilsin mi?",
                "PkfRobot", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (cevap != DialogResult.Yes) return;
        }

        var form = FindForm();
        form?.Hide();

        OrkaPenceresi.OneGetir(durum.Tutamac);

        _serit = new BilgiSeridi($"{kayit.Etiket}  ·  Hedefe tiklayin  ·  Iptal icin Esc");
        _serit.Show();

        _yakalayici = new TiklamaYakalayici();
        _yakalayici.Tiklandi += (x, y) => BeginInvoke(() => Yakalandi(kayit, deger, x, y));
        _yakalayici.Iptal += () => BeginInvoke(() => Bitir(form, null));

        try
        {
            _yakalayici.Basla();
        }
        catch (Exception ex)
        {
            Bitir(form, ex.Message);
        }
    }

    private void Yakalandi(KoordinatKaydi kayit, Label deger, int x, int y)
    {
        var form = FindForm();
        var sonuc = KoordinatSecimi.Degerlendir(_baglam.Orka.Ortam(x, y));

        if (!sonuc.Kabul)
        {
            Bitir(form, sonuc.Mesaj);
            return;
        }

        _baglam.Ayarlar.KoordinatYaz(kayit.Anahtar, kayit.Aciklama, sonuc.OranX, sonuc.OranY);
        _baglam.AyarlariKaydet();

        // Olcum hemen gorev dosyasina da yaziliyor: iki yerde birden durmasi
        // ancak ikisi ayni oldugunda ise yarar.
        var rapor = KalibrasyonUygulama.Uygula(_baglam.GorevlerKlasoru,
                                               new[] { _baglam.Ayarlar.Koordinat(kayit.Anahtar)! });

        deger.Text = $"{OranDonusturucu.Yaz(sonuc.OranX)} x {OranDonusturucu.Yaz(sonuc.OranY)}  (yeni olcum)";

        var mesaj = sonuc.Uyari ? sonuc.Mesaj : null;
        if (rapor.SorunVar)
            mesaj = string.Join(Environment.NewLine, rapor.Sorunlular.Select(s => s.Mesaj));

        Bitir(form, mesaj);
        Tazele();
    }

    private void Bitir(Form? form, string? mesaj)
    {
        _yakalayici?.Dur();
        _yakalayici?.Dispose();
        _yakalayici = null;

        _serit?.Close();
        _serit?.Dispose();
        _serit = null;

        if (form is not null)
        {
            form.Show();
            form.Activate();
        }

        if (!string.IsNullOrWhiteSpace(mesaj)) Uyar(mesaj);
    }

    // ---- deneme ----

    private void Dene(KoordinatKaydi kayit)
    {
        var kayitli = _baglam.Ayarlar.Koordinat(kayit.Anahtar);
        var oranX = kayitli?.X ?? kayit.X;
        var oranY = kayitli?.Y ?? kayit.Y;

        var durum = _baglam.Orka.Durum();
        if (!durum.Bulundu)
        {
            Uyar("ORKA acik degil. Denemek icin ORKA'yi acip kalibre edilen ekrana gidin.");
            return;
        }

        var cevap = MessageBox.Show(this,
            $"\"{kayit.Etiket}\" noktasina GERCEKTEN tiklanacak " +
            $"(oran {OranDonusturucu.Yaz(oranX)} x {OranDonusturucu.Yaz(oranY)}).\n\n" +
            "ORKA'da bir menu acilabilir ya da bir islem baslayabilir. " +
            "Once test firmasinda denemeniz onerilir.\n\nDevam edilsin mi?",
            "PkfRobot", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (cevap != DialogResult.Yes) return;

        var form = FindForm();
        form?.Hide();

        try
        {
            OrkaPenceresi.OneGetir(durum.Tutamac);
            Thread.Sleep(400);

            // Olcu one getirdikten SONRA okunuyor: pencere simge durumundan
            // donduyse eski dikdortgen yanlis noktaya goturur.
            var olcu = OrkaPenceresi.Olcu(durum.Tutamac);
            if (!olcu.Gecerli)
            {
                form?.Show();
                Uyar("ORKA penceresinin olculeri okunamadi.");
                return;
            }

            var (x, y) = OranDonusturucu.Mutlak(oranX, oranY, olcu);

            Mouse.MoveTo(x, y);
            Thread.Sleep(_baglam.Config.Zamanlama.TusBeklemeMs);
            Mouse.Click(MouseButton.Left);
            Thread.Sleep(Math.Max(600, _baglam.Config.Zamanlama.AdimBeklemeMs));

            var goruntu = EkranYakalama.Al(x, y);

            form?.Show();

            using var pencere = new DenemePenceresi(goruntu,
                $"Deneme: {kayit.Etiket} · oran {OranDonusturucu.Yaz(oranX)} x {OranDonusturucu.Yaz(oranY)} " +
                $"· piksel {x}, {y}");
            pencere.ShowDialog(this);
        }
        catch (Exception ex)
        {
            form?.Show();
            Uyar($"Deneme yapilamadi: {ex.Message}");
        }
    }

    // ---- alt cubuk ----

    private Control AltCubuk()
    {
        var cubuk = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(8, 6, 8, 6) };

        var uygula = new Button { Text = "Gorevlere uygula", Width = 130 };
        uygula.Click += (_, _) => Uygula();

        var yenile = new Button { Text = "Yenile", Width = 80 };
        yenile.Click += (_, _) => Tazele();

        var sifirla = new Button { Text = "Olcumu sil", Width = 90 };
        sifirla.Click += (_, _) => OlcumuSil();

        cubuk.Controls.Add(uygula);
        cubuk.Controls.Add(yenile);
        cubuk.Controls.Add(sifirla);
        return cubuk;
    }

    private void Uygula()
    {
        var rapor = _baglam.KalibrasyonuUygula();

        var mesaj = $"{rapor.Uygulanan} koordinat yazildi, {rapor.Ayni} zaten guncel.";
        if (rapor.SorunVar)
            mesaj += Environment.NewLine + Environment.NewLine +
                     string.Join(Environment.NewLine, rapor.Sorunlular.Select(s => s.Mesaj));

        MessageBox.Show(this, mesaj, "PkfRobot", MessageBoxButtons.OK,
                        rapor.SorunVar ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

        Tazele();
    }

    /// <summary>
    /// Kayitli olcumleri siler; gorev dosyalarindaki degerler oldugu gibi kalir.
    /// Yanlis olculmus bir koordinattan sonra "yayindaki degere don" yolu.
    /// </summary>
    private void OlcumuSil()
    {
        if (_baglam.Ayarlar.Koordinatlar.Count == 0)
        {
            Uyar("Kayitli olcum yok.");
            return;
        }

        if (MessageBox.Show(this,
                $"{_baglam.Ayarlar.Koordinatlar.Count} kayitli olcum silinecek. " +
                "Gorev dosyalarindaki degerler degismez.\n\nDevam edilsin mi?",
                "PkfRobot", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _baglam.Ayarlar.Koordinatlar.Clear();
        _baglam.AyarlariKaydet();
        Tazele();
    }

    private void Uyar(string mesaj)
        => MessageBox.Show(this, mesaj, "PkfRobot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
