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
            // Iki ayri durum, iki ayri cumle: ORKA hic acik degil mi, yoksa
            // acik ama olculecek ANA pencere mi bulunamiyor? Ikisi "ORKA acik
            // degil" diye ayni cumleye sikistirilirsa kullanici bosuna ORKA'yi
            // yeniden acar.
            var mesaj = durum.Surecler.Count == 0
                ? $"ORKA calismiyor ('{_baglam.Config.Ajan.OrkaSurecAdi}' sureci bulunamadi). " +
                  "Kalibre edilecek ekrana kadar ORKA'da elle gidin, sonra Sec'e basin."
                : $"ORKA calisiyor (pid {string.Join(", ", durum.Surecler)}) ama olculecek ANA " +
                  $"penceresi bulunamadi. Beklenen baslik: '{_baglam.Config.Pencereler.AnaEkran}'. " +
                  "Ana pencere simge durumunda olabilir; gorev cubugundan geri acip yeniden deneyin.";

            _baglam.Log.Uyari($"Koordinat secimi baslatilamadi: {mesaj}");
            Uyar(mesaj + TeshisEki());
            return;
        }

        // Tam ekran degilse UYARILIR, ENGELLENMEZ: oran pencerenin o anki
        // olcusunden hesaplaniyor, maximize olmamasi matematigi bozmuyor. Asil
        // risk pencerenin sonradan yeniden boyutlandirilmasi -- o da uyari
        // konusu. Burada onceden Evet/Hayir soran bir kutu vardi ve secimi
        // fiilen engelliyordu.
        var tamEkranNotu = durum.TamEkran
            ? string.Empty
            : "  ·  ORKA tam ekran degil (olcum yine de alinir)";

        if (!durum.TamEkran)
            _baglam.Log.Uyari(
                $"Koordinat secimi '{kayit.Etiket}': ORKA tam ekran degil " +
                $"(pencere {durum.Olcu.Genislik}x{durum.Olcu.Yukseklik}). Olcum engellenmiyor; " +
                "pencere sonradan yeniden boyutlandirilirsa oran kayar.");

        // Secim baslarken ekrandan ne okundugu log'a giriyor: ret olursa
        // "neye gore olculecekti" sorusunun cevabi burada duruyor.
        _baglam.Log.Bilgi(
            $"Koordinat secimi basladi: '{kayit.Etiket}' · beklenen surec " +
            $"{_baglam.Config.Ajan.OrkaSurecAdi} · ana pencere '{durum.Baslik}' " +
            $"[sol={durum.Olcu.Sol} ust={durum.Olcu.Ust} genislik={durum.Olcu.Genislik} " +
            $"yukseklik={durum.Olcu.Yukseklik}] · ORKA surecleri " +
            $"[{string.Join(", ", durum.Surecler)}]");

        var form = FindForm();
        form?.Hide();

        OrkaPenceresi.OneGetir(durum.Tutamac);

        _serit = new BilgiSeridi(
            $"{kayit.Etiket}  ·  Hedefe tiklayin (ORKA'nin alt pencereleri de olur)" +
            $"  ·  Iptal icin Esc{tamEkranNotu}");
        if (!durum.TamEkran) _serit.Uyar();
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
        var ortam = _baglam.Orka.Ortam(x, y);
        var sonuc = KoordinatSecimi.Degerlendir(ortam);

        // Karar her zaman log'a yaziliyor -- kabul de ret de. "Secici tiklamayi
        // kabul etmiyor" denildiginde bakilacak yer burasi: hangi pencereye
        // tiklandi, hangi denetim tetiklendi, oranin paydasi hangi pencereydi.
        var satir = KoordinatSecimi.Gunluk(ortam, sonuc);
        if (sonuc.Kabul) _baglam.Log.Bilgi(satir);
        else _baglam.Log.Uyari(satir);

        if (!sonuc.Kabul)
        {
            // Pencere bulunamadi ya da olcusu alinamadiysa teshis dokumu de
            // eklenir; diger retlerde (baska uygulama, ana pencere disi) sorun
            // pencere taramasinda degil, dokum kalabalik yapardi.
            var ek = sonuc.Sebep is RedSebebi.OrkaKapali or RedSebebi.PencereOlcusuOkunamadi
                ? TeshisEki()
                : string.Empty;

            Bitir(form, sonuc.Mesaj + ek);
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
            Uyar("ORKA acik degil. Denemek icin ORKA'yi acip kalibre edilen ekrana gidin."
                 + TeshisEki());
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
            var olcu = OrkaPenceresi.OlcuAl(durum.Tutamac);
            if (!olcu.Gecerli)
            {
                form?.Show();
                Uyar("ORKA penceresinin olculeri okunamadi." + TeshisEki());
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

    // ---- GECICI TESHIS ----

    /// <summary>
    /// Olcu alinamadiginda hata kutusuna eklenen pencere dokumu.
    ///
    /// Ayni dokum bir dosyaya da yaziliyor: basliklar uzun, mesaj kutusuna
    /// sigmiyor. Kutuda dosyanin yolu gosteriliyor.
    ///
    /// <b>Gecici:</b> AnaPencereBul'un neden bos dondugu anlasilinca bu metot ve
    /// cagrilari, OrkaPenceresi'ndeki teshis blogu ile birlikte silinecek.
    /// </summary>
    private string TeshisEki()
    {
        try
        {
            var dokum = _baglam.Orka.Teshis();
            var yol = OrkaPenceresi.TeshisKaydet(_baglam.Config.LogKlasoru, dokum);

            // Log paneline de dusuyor: kutu kapatildiktan sonra da elde kalsin.
            _baglam.Log.Uyari(dokum);

            var yolSatiri = yol is null
                ? "Dokum dosyaya YAZILAMADI (log klasorune erisilemedi)."
                : $"Bu dokum su dosyaya da yazildi:{Environment.NewLine}{yol}";

            return Environment.NewLine + Environment.NewLine +
                   dokum + Environment.NewLine + yolSatiri;
        }
        catch (Exception ex)
        {
            return Environment.NewLine + Environment.NewLine +
                   $"(Teshis dokumu alinamadi: {ex.Message})";
        }
    }
}
