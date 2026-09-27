using System.Runtime.Versioning;
using PkfRobot.Ayarlar;
using PkfRobot.Config;
using PkfRobot.Core;

namespace PkfRobot.Arayuz;

/// <summary>
/// Calistir sekmesi: firma sec, sifreleri gir, banka satirlarini isaretle, START.
///
/// <b>Bir satir = bir banka.</b> Her satir kendi hesap kodunu ve kendi ekstre
/// dosyasini tasiyor; dosya adindan banka tahmin edilmiyor ve klasor taranmiyor.
/// Tahmin, yanlis bankanin ekstresini dogru gorunen bir kodla aktarma riski
/// demek olurdu (bkz. <see cref="BankaSatiri"/>).
///
/// <b>Hesap kodlari firma bazli:</b> tabloda gorunen kod, secili firmanin kodu.
/// Firma degisince tablo o firmanin setini gosterir; 0336 icin kodlar bos gelir
/// ve kullanici doldurur.
///
/// Tablodaki secimler, hesap kodlari ve zamanlama <c>%AppData%\PkfRobot\ayarlar.json</c>
/// icinde kaliyor: publish klasoru her yayinda uzerine yaziliyor, kullanicinin
/// girdigi degerler orada durmamali.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CalistirPaneli : UserControl
{
    private const string SeciliSutun = "secili";
    private const string BankaSutun = "banka";
    private const string KodSutun = "kod";
    private const string DosyaSutun = "dosya";
    private const string GozatSutun = "gozat";
    private const string DurumSutun = "durum";
    private const string KarsiSutun = "karsi";

    private readonly ArayuzBaglami _baglam;
    private readonly CalismaDenetimi _denetim = new();

    private readonly ComboBox _firma = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly TextBox _orkaSifre = new() { UseSystemPasswordChar = true, Width = 120 };
    private readonly TextBox _firmaSifre = new() { UseSystemPasswordChar = true, Width = 120 };
    private readonly Button _sifreKaydet = new() { Text = "Sifreleri kaydet", Width = 120 };
    private readonly Button _firmaEkle = new() { Text = "Firma ekle", Width = 90 };
    private readonly Button _firmaCikar = new() { Text = "Firma cikar", Width = 90 };

    private readonly NumericUpDown _adimBekleme = SayiKutusu(0, 60000, 50);
    private readonly NumericUpDown _tusBekleme = SayiKutusu(0, 10000, 10);

    /// <summary>
    /// GridDoldur'un tuslar arasi beklemesi. Ayri kutu, cunku grid'de satir
    /// basina 5 tus var ve 43 satirlik bir ekstrede ortak deger (150 ms, carpanla
    /// 225 ms) yalnizca bekleme olarak 48 saniye ediyordu.
    /// </summary>
    private readonly NumericUpDown _gridTusBekleme = SayiKutusu(0, 10000, 10);

    private readonly NumericUpDown _pencereTimeout = SayiKutusu(1, 600, 5);
    private readonly NumericUpDown _acilisTimeout = SayiKutusu(1, 900, 5);

    private readonly NumericUpDown _hizCarpani = new()
    {
        DecimalPlaces = 2,
        Increment = 0.1m,
        Minimum = (decimal)Hizlandirici.EnAz,
        Maximum = (decimal)Hizlandirici.EnCok,
        Width = 70
    };

    private readonly ComboBox _acilisGorevi = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly ComboBox _bankaGorevi = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };

    private readonly DataGridView _tablo = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        EditMode = DataGridViewEditMode.EditOnEnter,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
    };

    /// <summary>
    /// Secili bankanin ELLE girilen karsi hesap kodlari (virgul ya da satir sonu).
    ///
    /// <b>Neden tabloda degil, altta ayri bir kutu:</b> 10 kod bir grid hucresine
    /// sigmiyor ve cok satirli giris gerekiyor. Tabloda yalnizca "kac kod var"
    /// ozeti duruyor -- hangi satirda elle liste oldugu bir bakista gorunsun.
    /// </summary>
    private readonly TextBox _karsiHesaplar = new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        WordWrap = true
    };

    private readonly Label _karsiBaslik = new()
    {
        Dock = DockStyle.Top,
        Height = 34,
        Padding = new Padding(4, 2, 0, 0),
        Text = "Karsi hesap kodlari (elle test) - satir sec, virgul ya da alt satirla ayir. " +
               "Bos ise sunucudan gelen liste kullanilir."
    };

    private readonly Button _yukari = new() { Text = "Yukari", Width = 70 };
    private readonly Button _asagi = new() { Text = "Asagi", Width = 70 };
    private readonly Button _bankaEkle = new() { Text = "Banka ekle", Width = 90 };
    private readonly Button _satirCikar = new() { Text = "Cikar", Width = 70 };

    private readonly Button _start = new() { Text = "START", Width = 90, Height = 30 };
    private readonly Button _durdur = new() { Text = "DURDUR", Width = 90, Height = 30, Enabled = false };
    private readonly Button _devam = new() { Text = "DEVAM", Width = 90, Height = 30, Enabled = false };

    private readonly ProgressBar _ilerleme = new() { Dock = DockStyle.Top, Height = 16, Maximum = 100 };
    private readonly Label _ilerlemeMetni = new() { Dock = DockStyle.Top, Height = 18, ForeColor = Color.DimGray };

    private readonly TextBox _log = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Font = new Font("Consolas", 8.5F),
        BackColor = Color.White
    };

    /// <summary>Tablo koddan doldurulurken hucre olaylari modeli bozmasin.</summary>
    private bool _dolduruluyor;

    private Task? _calisan;

    // ---- canli ilerleme ----
    // Metni CALISAN IPLIK yazmiyor: her adim icin bir Invoke, uzun bir adim
    // boyunca da hicbir tazeleme demekti -- "adim 12/38" satiri 45 saniye
    // kipirdamadan duruyordu ve robotun asili kalip kalmadigi anlasilmiyordu.
    // Iplik yalnizca "hangi adim, ne zaman basladi" bilgisini birakiyor, sayaci
    // bu zamanlayici ekranda ilerletiyor.
    private readonly System.Windows.Forms.Timer _sureSaati = new() { Interval = 200 };

    /// <summary>Calisan adimin baslama ani (UTC tick); 0 = adim yok.</summary>
    private long _adimBaslangici;

    /// <summary>Kuyrugun baslama ani (UTC tick); 0 = calismiyor.</summary>
    private long _kosuBaslangici;

    private string _adimMetni = string.Empty;
    private int _bitenBanka;
    private int _toplamBanka;

    /// <summary>
    /// Duraklama/onay gibi durumlarda ilerleme satirinin yerine gecen metin.
    /// Bos degilse zamanlayici satiri EZMIYOR: "KAYDET bekleniyor" uyarisi,
    /// sayacin bir sonraki tikinda kaybolurdu.
    /// </summary>
    private string _ozelDurum = string.Empty;

    public CalistirPaneli(ArayuzBaglami baglam)
    {
        _baglam = baglam;
        Dock = DockStyle.Fill;

        SutunlariKur();
        Controls.Add(Govde());

        _denetim.Degisti += () => UiIsle(DugmeDurumu);

        _sureSaati.Tick += (_, _) => IlerlemeyiTazele();

        Yukle();
    }

    private CalistirmaAyari Ayar => _baglam.Ayarlar.Calistirma;

    /// <summary>
    /// Secili firma. Liste artik SABIT DEGIL, kullanicinin ayarlarindan geliyor;
    /// hicbiri secili degilse kayitli koda, o da yoksa ilk firmaya duser.
    /// </summary>
    private OrkaFirmasi SeciliFirma
        => _firma.SelectedItem as OrkaFirmasi
           ?? Firmalar.Bul(Ayar.Firmalar, Ayar.FirmaKodu)
           ?? Firmalar.Hepsi[0];

    // ================== KURULUM ==================

    private static NumericUpDown SayiKutusu(int enAz, int enCok, int adim) => new()
    {
        Minimum = enAz,
        Maximum = enCok,
        Increment = adim,
        Width = 70
    };

    private Control Govde()
    {
        var kok = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(8)
        };

        kok.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        kok.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        kok.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        kok.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        kok.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        kok.Controls.Add(FirmaKutusu(), 0, 0);
        kok.Controls.Add(ZamanlamaKutusu(), 0, 1);
        kok.Controls.Add(GorevKutusu(), 0, 2);
        kok.Controls.Add(BankaKutusu(), 0, 3);
        kok.Controls.Add(AltKisim(), 0, 4);

        return kok;
    }

    private Control FirmaKutusu()
    {
        var kutu = new GroupBox { Text = "Firma ve sifreler", Dock = DockStyle.Top, Height = 104 };

        var satir = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(6, 4, 0, 0)
        };

        _firma.SelectedIndexChanged += (_, _) => FirmaDegisti();
        _orkaSifre.TextChanged += (_, _) => _sifreKaydet.Enabled = true;
        _firmaSifre.TextChanged += (_, _) => _sifreKaydet.Enabled = true;
        _sifreKaydet.Click += (_, _) => SifreleriKaydet();
        _firmaEkle.Click += (_, _) => FirmaEkle();
        _firmaCikar.Click += (_, _) => FirmaCikar();

        satir.Controls.Add(Etiket("Firma:"));
        satir.Controls.Add(_firma);
        satir.Controls.Add(_firmaEkle);
        satir.Controls.Add(_firmaCikar);
        satir.Controls.Add(Etiket("ORKA sifresi:"));
        satir.Controls.Add(_orkaSifre);
        satir.Controls.Add(Etiket("Firma sifresi:"));
        satir.Controls.Add(_firmaSifre);
        satir.Controls.Add(_sifreKaydet);

        // Iki sifre de FIRMA BASINA saklaniyor; ustteki kutular hep SECILI
        // firmanin sifreleri. Ayni kutulari tek firma icin doldurup baska
        // firmayla calistirmak eskiden mumkundu ve yanlis sifre ORKA'yi kilitler.
        satir.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(4, 6, 0, 0),
            Text = "Her iki sifre de secili firmaya ait; DPAPI ile sifreli saklanir " +
                   "ve sunucuya GONDERILMEZ."
        });

        kutu.Controls.Add(satir);
        return kutu;
    }

    private Control ZamanlamaKutusu()
    {
        var kutu = new GroupBox { Text = "Zamanlama", Dock = DockStyle.Top, Height = 78 };

        var satir = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(6, 4, 0, 0)
        };

        satir.Controls.Add(Etiket("Adim (ms):"));
        satir.Controls.Add(_adimBekleme);
        satir.Controls.Add(Etiket("Tus (ms):"));
        satir.Controls.Add(_tusBekleme);
        satir.Controls.Add(Etiket("Grid tus (ms):"));
        satir.Controls.Add(_gridTusBekleme);
        satir.Controls.Add(Etiket("Pencere (sn):"));
        satir.Controls.Add(_pencereTimeout);
        satir.Controls.Add(Etiket("ORKA acilis (sn):"));
        satir.Controls.Add(_acilisTimeout);
        satir.Controls.Add(Etiket("Hiz carpani:"));
        satir.Controls.Add(_hizCarpani);

        var aciklama = new Label
        {
            Text = "Hiz carpani gorevdeki Bekle surelerini ve tus beklemelerini olcekler " +
                   "(2 = iki kati bekleme). JSON dosyalari degismez. Grid tus, yalnizca " +
                   "GridDoldur adiminda gecerli; bu degerler ajan (sunucudan gelen is) " +
                   "yolunda da kullaniliyor.",
            AutoSize = false,
            Width = 520,
            Height = 16,
            ForeColor = Color.DimGray
        };
        satir.Controls.Add(aciklama);

        foreach (var kutucuk in new NumericUpDown[]
                 { _adimBekleme, _tusBekleme, _gridTusBekleme, _pencereTimeout,
                   _acilisTimeout, _hizCarpani })
            kutucuk.ValueChanged += (_, _) => AyarlariTopla();

        kutu.Controls.Add(satir);
        return kutu;
    }

    private Control GorevKutusu()
    {
        var kutu = new GroupBox { Text = "Gorev dosyalari", Dock = DockStyle.Top, Height = 76 };

        var satir = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(6, 4, 0, 0)
        };

        satir.Controls.Add(Etiket("Acilis (bir kez):"));
        satir.Controls.Add(_acilisGorevi);
        satir.Controls.Add(Etiket("Her banka icin:"));
        satir.Controls.Add(_bankaGorevi);

        _acilisGorevi.SelectedIndexChanged += (_, _) => AyarlariTopla();
        _bankaGorevi.SelectedIndexChanged += (_, _) => AyarlariTopla();

        kutu.Controls.Add(satir);
        return kutu;
    }

    private Control BankaKutusu()
    {
        var kutu = new GroupBox { Text = "Bankalar", Dock = DockStyle.Fill };

        var uyari = new Label
        {
            Dock = DockStyle.Top,
            Height = 32,
            ForeColor = Color.Firebrick,
            Padding = new Padding(4, 2, 0, 0),
            Text = "Hesap kodlari PKF Aday (0001) icin ekran goruntusunden okundu -- " +
                   "ORKA hesap planiyla karsilastirip dogrulayin."
        };

        var dugmeler = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 32,
            FlowDirection = FlowDirection.LeftToRight
        };

        _yukari.Click += (_, _) => Tasi(-1);
        _asagi.Click += (_, _) => Tasi(+1);
        _bankaEkle.Click += (_, _) => BankaEkle();
        _satirCikar.Click += (_, _) => SatirCikar();

        dugmeler.Controls.Add(_yukari);
        dugmeler.Controls.Add(_asagi);
        dugmeler.Controls.Add(_bankaEkle);
        dugmeler.Controls.Add(_satirCikar);

        var karsiKutu = new GroupBox
        {
            Dock = DockStyle.Bottom,
            Height = 110,
            Text = "Karsi hesap kodlari (secili banka)"
        };

        karsiKutu.Controls.Add(_karsiHesaplar);
        karsiKutu.Controls.Add(_karsiBaslik);

        // Satir degisince kutu o satirin listesini gosteriyor; yazilan metin
        // modele ANINDA isleniyor (kullanici kutudan cikmadan START'a basabilir),
        // diske ise odak kutudan ayrilinca yaziliyor -- her tusa basista dosya
        // yazmanin anlami yok.
        _tablo.SelectionChanged += (_, _) => KarsiHesaplariGoster();
        _karsiHesaplar.TextChanged += (_, _) => KarsiHesaplariModeleIsle();
        _karsiHesaplar.Leave += (_, _) => Kaydet();

        kutu.Controls.Add(_tablo);
        kutu.Controls.Add(karsiKutu);
        kutu.Controls.Add(dugmeler);
        kutu.Controls.Add(uyari);

        return kutu;
    }

    /// <summary>Tablodaki ozet hucresi: "3 kod" ya da bos.</summary>
    private static string KarsiOzet(string? metin)
    {
        var adet = KarsiHesapListesi.Coz(metin).Count;
        return adet == 0 ? string.Empty : $"{adet} kod";
    }

    /// <summary>Secili satirin listesini kutuya yazar.</summary>
    private void KarsiHesaplariGoster()
    {
        var oncekiDurum = _dolduruluyor;
        _dolduruluyor = true;

        try
        {
            if (_tablo.CurrentRow?.Tag is BankaSatiri satir)
            {
                _karsiHesaplar.Enabled = true;
                _karsiHesaplar.Text = satir.KarsiHesaplar(SeciliFirma.Kod);
            }
            else
            {
                // Satir yoksa kutu kapali: hangi bankaya yazildigi belirsiz bir
                // metin, yanlis satira kaydedilmekten kotudur.
                _karsiHesaplar.Text = string.Empty;
                _karsiHesaplar.Enabled = false;
            }
        }
        finally
        {
            _dolduruluyor = oncekiDurum;
        }
    }

    /// <summary>Kutudaki metni secili satirin modeline yazar (diske DEGIL).</summary>
    private void KarsiHesaplariModeleIsle()
    {
        if (_dolduruluyor) return;
        if (_tablo.CurrentRow?.Tag is not BankaSatiri satir) return;

        satir.KarsiHesaplarYaz(SeciliFirma.Kod, _karsiHesaplar.Text);

        _dolduruluyor = true;
        _tablo.CurrentRow.Cells[KarsiSutun].Value = KarsiOzet(_karsiHesaplar.Text);
        _dolduruluyor = false;
    }

    private Control AltKisim()
    {
        var alt = new Panel { Dock = DockStyle.Top, Height = 190 };

        var dugmeler = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight
        };

        _start.Click += (_, _) => Baslat();
        _durdur.Click += (_, _) => _denetim.Duraklat();
        _devam.Click += (_, _) => _denetim.Devam();

        var iptal = new Button { Text = "Kuyrugu kes", Width = 90, Height = 30 };
        iptal.Click += (_, _) => KuyruguKes();

        dugmeler.Controls.Add(_start);
        dugmeler.Controls.Add(_durdur);
        dugmeler.Controls.Add(_devam);
        dugmeler.Controls.Add(iptal);

        var logKutusu = new Panel { Dock = DockStyle.Fill };
        logKutusu.Controls.Add(_log);

        alt.Controls.Add(logKutusu);
        alt.Controls.Add(_ilerlemeMetni);
        alt.Controls.Add(_ilerleme);
        alt.Controls.Add(dugmeler);

        return alt;
    }

    private static Label Etiket(string metin) => new()
    {
        Text = metin,
        AutoSize = true,
        Margin = new Padding(6, 7, 2, 0)
    };

    private void SutunlariKur()
    {
        _tablo.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = SeciliSutun,
            HeaderText = string.Empty,
            Width = 28
        });

        _tablo.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = BankaSutun,
            HeaderText = "Banka",
            Width = 110
        });

        _tablo.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = KodSutun,
            HeaderText = "Hesap kodu",
            Width = 130
        });

        _tablo.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = DosyaSutun,
            HeaderText = "Ekstre dosyasi",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 120
        });

        _tablo.Columns.Add(new DataGridViewButtonColumn
        {
            Name = GozatSutun,
            HeaderText = string.Empty,
            Text = "Gozat",
            UseColumnTextForButtonValue = true,
            Width = 60
        });

        _tablo.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = KarsiSutun,
            HeaderText = "Karsi hesap",
            ReadOnly = true,
            Width = 90,
            ToolTipText = "Elle girilen karsi hesap kodu sayisi. Duzenlemek icin satiri secip " +
                          "asagidaki kutuyu kullanin."
        });

        _tablo.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = DurumSutun,
            HeaderText = "Durum",
            ReadOnly = true,
            Width = 80
        });

        // Onay kutusu hucreden cikmadan modele islensin; aksi halde START'a
        // basildiginda son isaretlenen satir hala eski degeriyle okunurdu.
        _tablo.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_tablo.IsCurrentCellDirty)
                _tablo.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        _tablo.CellValueChanged += (_, e) => HucreDegisti(e.RowIndex, e.ColumnIndex);
        _tablo.CellContentClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && _tablo.Columns[e.ColumnIndex].Name == GozatSutun)
                DosyaSec(e.RowIndex);
        };
    }

    // ================== YUKLEME / KAYIT ==================

    private void Yukle()
    {
        _dolduruluyor = true;

        Ayar.VarsayilanlariTamamla();

        FirmaListesiniDoldur();

        _adimBekleme.Value = Sinirla(_adimBekleme, Ayar.AdimBeklemeMs);
        _tusBekleme.Value = Sinirla(_tusBekleme, Ayar.TusBeklemeMs);
        _gridTusBekleme.Value = Sinirla(_gridTusBekleme, Ayar.GridTusBeklemeMs);
        _pencereTimeout.Value = Sinirla(_pencereTimeout, Ayar.PencereTimeoutSn);
        _acilisTimeout.Value = Sinirla(_acilisTimeout, Ayar.OrkaAcilisTimeoutSn);
        _hizCarpani.Value = Sinirla(_hizCarpani, (decimal)Hizlandirici.Kirp(Ayar.HizCarpani));

        GorevListesiniDoldur();

        _dolduruluyor = false;

        TabloyuDoldur();
        SifreleriYukle();
        DugmeDurumu();
    }

    private static decimal Sinirla(NumericUpDown kutu, decimal deger)
        => Math.Clamp(deger, kutu.Minimum, kutu.Maximum);

    private void GorevListesiniDoldur()
    {
        _acilisGorevi.Items.Clear();
        _bankaGorevi.Items.Clear();

        _acilisGorevi.Items.Add("(yok - ORKA zaten acik)");

        var dosyalar = new List<string>();
        try
        {
            if (Directory.Exists(_baglam.GorevlerKlasoru))
                dosyalar = Directory.GetFiles(_baglam.GorevlerKlasoru, "*.json")
                                    .Select(Path.GetFileName)
                                    .Where(a => a is not null)
                                    .Select(a => a!)
                                    .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
                                    .ToList();
        }
        catch (Exception ex)
        {
            Yaz($"Gorev dosyalari okunamadi: {ex.Message}");
        }

        foreach (var dosya in dosyalar)
        {
            _acilisGorevi.Items.Add(dosya);
            _bankaGorevi.Items.Add(dosya);
        }

        AcilisGorevGocu(dosyalar);

        _acilisGorevi.SelectedItem = string.IsNullOrWhiteSpace(Ayar.AcilisGorevi)
            ? _acilisGorevi.Items[0]
            : dosyalar.FirstOrDefault(d => d.Equals(Ayar.AcilisGorevi, StringComparison.OrdinalIgnoreCase))
              ?? _acilisGorevi.Items[0];

        _bankaGorevi.SelectedItem =
            dosyalar.FirstOrDefault(d => d.Equals(Ayar.BankaGorevi, StringComparison.OrdinalIgnoreCase));

        if (_bankaGorevi.SelectedItem is null && _bankaGorevi.Items.Count > 0)
            _bankaGorevi.SelectedIndex = 0;
    }

    /// <summary>
    /// Diskte kalmis ESKI acilis gorevini yenisiyle degistirir.
    ///
    /// Eski varsayilan (01-orka-ac-firma-sec.json) modul ekraninda BITIYOR,
    /// TRANSFERLER'e girmiyordu; dongu govdesi bu yuzden yanlis ekranda
    /// basliyordu. Ayar bir kez kaydedildigi icin yeni varsayilan tek basina
    /// yetmiyor -- ayarlar.json'daki eski deger okunmaya devam ederdi.
    ///
    /// Yalniz ESKI VARSAYILAN degistiriliyor: kullanici bilerek baska bir dosya
    /// sectiyse ona dokunulmuyor. Bir kez calisir, cunku ardindan kaydedilen
    /// deger artik acilis.json.
    /// </summary>
    private void AcilisGorevGocu(IReadOnlyCollection<string> dosyalar)
    {
        const string yeni = "acilis.json";

        if (!Ayar.AcilisGorevi.Equals(CalistirmaAyari.EskiAcilisGorevi,
                                      StringComparison.OrdinalIgnoreCase))
            return;

        if (!dosyalar.Any(d => d.Equals(yeni, StringComparison.OrdinalIgnoreCase)))
            return;

        Ayar.AcilisGorevi = yeni;
        Yaz($"Acilis gorevi '{CalistirmaAyari.EskiAcilisGorevi}' -> '{yeni}' olarak guncellendi: " +
            "eskisi modul ekraninda bitiyordu, TRANSFERLER'e girmiyordu.");
    }

    private void TabloyuDoldur()
    {
        _dolduruluyor = true;
        _tablo.Rows.Clear();

        var firmaKodu = SeciliFirma.Kod;

        foreach (var satir in Ayar.Bankalar)
        {
            var i = _tablo.Rows.Add(
                satir.Secili,
                satir.Banka,
                satir.HesapKodu(firmaKodu),
                DosyaMetni(satir.DosyaYolu),
                "Gozat",
                KarsiOzet(satir.KarsiHesaplar(firmaKodu)),
                string.Empty);

            _tablo.Rows[i].Tag = satir;
            _tablo.Rows[i].Cells[DosyaSutun].ToolTipText = satir.DosyaYolu;
        }

        _dolduruluyor = false;

        // Firma degisince kutu da yenilenmeli: karsi hesap listesi firma basina
        // tutuluyor ve eski firmanin listesi ekranda kalsa yanlis firmaya
        // kaydedilirdi.
        KarsiHesaplariGoster();
    }

    private static string DosyaMetni(string? yol)
        => string.IsNullOrWhiteSpace(yol) ? "(dosya secilmedi)" : Path.GetFileName(yol);

    private void HucreDegisti(int satirNo, int sutunNo)
    {
        if (_dolduruluyor || satirNo < 0 || sutunNo < 0) return;
        if (_tablo.Rows[satirNo].Tag is not BankaSatiri satir) return;

        var sutun = _tablo.Columns[sutunNo].Name;
        var deger = _tablo.Rows[satirNo].Cells[sutunNo].Value;

        switch (sutun)
        {
            case SeciliSutun:
                satir.Secili = deger is true;
                break;

            case BankaSutun:
                satir.Banka = (deger as string ?? string.Empty).Trim();
                break;

            case KodSutun:
                satir.HesapKoduYaz(SeciliFirma.Kod, deger as string);
                break;
        }

        Kaydet();
    }

    private void DosyaSec(int satirNo)
    {
        if (_tablo.Rows[satirNo].Tag is not BankaSatiri satir) return;

        using var secici = new OpenFileDialog
        {
            Title = $"{satir.Banka} ekstresi",
            Filter = "Excel dosyalari (*.xlsx;*.xls)|*.xlsx;*.xls|Tum dosyalar (*.*)|*.*",
            CheckFileExists = true
        };

        if (!string.IsNullOrWhiteSpace(satir.DosyaYolu))
        {
            var klasor = Path.GetDirectoryName(satir.DosyaYolu);
            if (!string.IsNullOrWhiteSpace(klasor) && Directory.Exists(klasor))
                secici.InitialDirectory = klasor;
        }

        if (secici.ShowDialog(this) != DialogResult.OK) return;

        satir.DosyaYolu = secici.FileName;

        // Dosya secmek "bu bankayi isle" demek; kullaniciyi ayrica isaretlemeye
        // zorlamak, unutuldugunda sessizce atlanan bir satir birakirdi.
        satir.Secili = true;

        _dolduruluyor = true;
        _tablo.Rows[satirNo].Cells[DosyaSutun].Value = DosyaMetni(satir.DosyaYolu);
        _tablo.Rows[satirNo].Cells[DosyaSutun].ToolTipText = satir.DosyaYolu;
        _tablo.Rows[satirNo].Cells[SeciliSutun].Value = true;
        _dolduruluyor = false;

        Kaydet();
    }

    private void FirmaDegisti()
    {
        if (_dolduruluyor) return;

        Ayar.FirmaKodu = SeciliFirma.Kod;
        TabloyuDoldur();
        SifreleriYukle();
        Kaydet();
    }

    private void Tasi(int yon)
    {
        if (_tablo.CurrentRow?.Tag is not BankaSatiri satir) return;

        var eski = Ayar.Bankalar.IndexOf(satir);
        var yeni = eski + yon;
        if (eski < 0 || yeni < 0 || yeni >= Ayar.Bankalar.Count) return;

        Ayar.Bankalar.RemoveAt(eski);
        Ayar.Bankalar.Insert(yeni, satir);

        TabloyuDoldur();
        _tablo.ClearSelection();
        _tablo.Rows[yeni].Selected = true;
        _tablo.CurrentCell = _tablo.Rows[yeni].Cells[BankaSutun];

        Kaydet();
    }

    private void BankaEkle()
    {
        Ayar.Bankalar.Add(new BankaSatiri { Banka = "Yeni banka" });
        TabloyuDoldur();
        Kaydet();
    }

    private void SatirCikar()
    {
        if (_tablo.CurrentRow?.Tag is not BankaSatiri satir) return;

        var onay = MessageBox.Show(this,
            $"'{satir.Banka}' satiri silinsin mi? Hesap kodu ve dosya secimi de gider.",
            "Satir cikar", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (onay != DialogResult.Yes) return;

        Ayar.Bankalar.Remove(satir);
        TabloyuDoldur();
        Kaydet();
    }

    private void AyarlariTopla()
    {
        if (_dolduruluyor) return;

        Ayar.AdimBeklemeMs = (int)_adimBekleme.Value;
        Ayar.TusBeklemeMs = (int)_tusBekleme.Value;
        Ayar.GridTusBeklemeMs = (int)_gridTusBekleme.Value;
        Ayar.PencereTimeoutSn = (int)_pencereTimeout.Value;
        Ayar.OrkaAcilisTimeoutSn = (int)_acilisTimeout.Value;
        Ayar.HizCarpani = (double)_hizCarpani.Value;

        Ayar.AcilisGorevi = _acilisGorevi.SelectedIndex <= 0
            ? string.Empty
            : _acilisGorevi.SelectedItem as string ?? string.Empty;

        Ayar.BankaGorevi = _bankaGorevi.SelectedItem as string ?? string.Empty;

        Kaydet();
    }

    private void Kaydet()
    {
        if (_dolduruluyor) return;

        try
        {
            _baglam.AyarDeposu.Yaz(_baglam.Ayarlar);
        }
        catch (Exception ex)
        {
            Yaz($"Ayarlar kaydedilemedi: {ex.Message}");
        }
    }

    /// <summary>
    /// Secili firmanin sifreleri kutulara.
    ///
    /// <b>ORKA sifresi de firma bazli.</b> Onceden tek alandi ve firma degistirmek
    /// onu degistirmiyordu; her firmanin kendi ORKA kullanicisi/sifresi oldugunda
    /// bu, ikinci firmaya birincinin sifresiyle girmeyi denemek demekti.
    /// </summary>
    private void SifreleriYukle()
    {
        _dolduruluyor = true;
        _orkaSifre.Text = _baglam.Sifreler.OrkaSifresiAl(SeciliFirma.Kod);
        _firmaSifre.Text = _baglam.Sifreler.FirmaSifresiAl(SeciliFirma.Kod);
        _dolduruluyor = false;

        _sifreKaydet.Enabled = false;
    }

    private void SifreleriKaydet()
    {
        SifreleriKaydet(SeciliFirma.Kod, _orkaSifre.Text, _firmaSifre.Text);
        _sifreKaydet.Enabled = false;

        Yaz($"Sifreler kaydedildi ({SeciliFirma}). Diskte DPAPI ile sifreli duruyor, " +
            "sunucuya gonderilmiyor.");
    }

    private void SifreleriKaydet(string firmaKodu, string orkaSifresi, string firmaSifresi)
    {
        var sifreler = _baglam.Sifreler;
        sifreler.OrkaSifresiYaz(firmaKodu, orkaSifresi);
        sifreler.FirmaSifresiYaz(firmaKodu, firmaSifresi);

        // Tek alanlik eski degerler de guncelleniyor: yalnizca YEDEK olarak
        // duruyorlar (firma bazli kayit yoksa devreye giriyorlar) ama tek firmayla
        // calisan eski kurulumun davranisi degismesin.
        sifreler.OrkaSifresi = orkaSifresi;
        sifreler.FirmaSifresi = firmaSifresi;

        _baglam.SifreleriKaydet(sifreler);
    }

    // ================== FIRMA LISTESI ==================

    /// <summary>Acilir listeyi ayarlardaki firmalarla doldurur.</summary>
    private void FirmaListesiniDoldur()
    {
        var onceki = _dolduruluyor;
        _dolduruluyor = true;

        _firma.Items.Clear();
        foreach (var firma in Ayar.Firmalar) _firma.Items.Add(firma);

        _firma.SelectedItem = Firmalar.Bul(Ayar.Firmalar, Ayar.FirmaKodu);
        if (_firma.SelectedItem is null && _firma.Items.Count > 0) _firma.SelectedIndex = 0;

        _dolduruluyor = onceki;
        _firmaCikar.Enabled = _firma.Items.Count > 1;
    }

    /// <summary>
    /// Yeni firma: kod + aciklama + iki sifre, tek pencerede.
    ///
    /// <b>Neden sifreler de burada soruluyor:</b> sifresiz eklenen bir firma
    /// secildiginde START "sifre yok" deyip duruyor ve kullanici neyin eksik
    /// oldugunu ancak deneyerek ogreniyor. Eklerken sorulunca firma dogdugu anda
    /// calisabilir oluyor. Bos birakilabiliyor -- sonradan ustteki kutulardan da
    /// girilebilir.
    /// </summary>
    private void FirmaEkle()
    {
        using var pencere = new FirmaEklePenceresi();
        if (pencere.ShowDialog(this) != DialogResult.OK) return;

        var hata = FirmaListesi.EkleHatasi(Ayar.Firmalar, pencere.Kod);
        if (hata is not null)
        {
            MessageBox.Show(this, hata, "Firma eklenemedi",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var firma = new OrkaFirmasi(pencere.Kod, pencere.Ad);
        Ayar.Firmalar.Add(firma);
        Ayar.FirmaKodu = firma.Kod;

        SifreleriKaydet(firma.Kod, pencere.OrkaSifresi, pencere.FirmaSifresi);

        FirmaListesiniDoldur();
        TabloyuDoldur();
        SifreleriYukle();
        Kaydet();

        Yaz($"Firma eklendi: {firma}. Hesap kodlari bu firma icin BOS geliyor, doldurun.");
    }

    /// <summary>
    /// Firmayi listeden cikarir ve <b>sifrelerini de siler</b>.
    ///
    /// Banka satirlarindaki firma bazli hesap kodlari duruyor: kullanici yanlislikla
    /// cikarip geri eklerse elle girdigi kodlar geri gelsin. Sifre icin ayni sey
    /// gecerli degil -- artik calisilmayan bir firmanin sifresi diskte durmamali.
    /// </summary>
    private void FirmaCikar()
    {
        var firma = SeciliFirma;

        var hata = FirmaListesi.CikarHatasi(Ayar.Firmalar, firma.Kod);
        if (hata is not null)
        {
            MessageBox.Show(this, hata, "Firma cikarilamadi",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var onay = MessageBox.Show(this,
            $"{firma} listeden cikarilsin mi?" + Environment.NewLine + Environment.NewLine +
            "Bu firmanin kayitli ORKA ve firma sifresi de silinecek.",
            "Firma cikar", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (onay != DialogResult.Yes) return;

        Ayar.Firmalar.RemoveAll(f => string.Equals(f.Kod, firma.Kod, StringComparison.OrdinalIgnoreCase));

        var sifreler = _baglam.Sifreler;
        sifreler.FirmaKayitlariniSil(firma.Kod);
        _baglam.SifreleriKaydet(sifreler);

        Ayar.FirmaKodu = Ayar.Firmalar.FirstOrDefault()?.Kod ?? string.Empty;

        FirmaListesiniDoldur();
        TabloyuDoldur();
        SifreleriYukle();
        Kaydet();

        Yaz($"Firma cikarildi: {firma}. Sifreleri de silindi.");
    }

    // ================== CALISTIRMA ==================

    private void Baslat()
    {
        if (_calisan is { IsCompleted: false }) return;

        var firma = SeciliFirma;

        var eksikler = BankaKuyrugu.Eksikler(Ayar.Bankalar, firma.Kod);
        if (eksikler.Count > 0)
        {
            MessageBox.Show(this,
                "Baslamadan once su eksikler giderilmeli:" + Environment.NewLine + Environment.NewLine +
                string.Join(Environment.NewLine, eksikler),
                "Eksik var", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Sifreler: ekrandaki kutular > firma bazli kayit > tek alanlik eski kayit
        // > appsettings. Ayni siralama ajan yolunda da gecerli, karar TEK YERDE
        // (bkz. FirmaSifreCozucu) -- iki yolun ayri cevap vermesi, ofiste "arayuzden
        // calisiyor ama ajandan calismiyor" demek olurdu.
        var cozum = FirmaSifreCozucu.Coz(_baglam.Sifreler, firma.Kod,
                                         _baglam.Config.Giris.Sifre,
                                         _baglam.Config.Giris.FirmaSifresi);

        var orkaSifre = _orkaSifre.Text.Length > 0 ? _orkaSifre.Text : cozum.Orka.Deger;
        var firmaSifre = _firmaSifre.Text.Length > 0 ? _firmaSifre.Text : cozum.Firma.Deger;

        if (orkaSifre.Length == 0 || firmaSifre.Length == 0)
        {
            MessageBox.Show(this, cozum.EksikMesaji, "Sifre yok",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var bankaGorevi = GorevYolu(_bankaGorevi.SelectedItem as string);
        if (bankaGorevi is null)
        {
            MessageBox.Show(this, "Her banka icin calisacak gorev dosyasi secilmedi.",
                "Gorev yok", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var acilisGorevi = _acilisGorevi.SelectedIndex <= 0
            ? null
            : GorevYolu(_acilisGorevi.SelectedItem as string);

        var isler = BankaKuyrugu.Isler(Ayar.Bankalar, firma.Kod);

        // Form degerleri UI ipliginde okunup kopyalaniyor; arka plan ipligi
        // kontrollere dokunmasin.
        var cfg = _baglam.Config.CalismaKopyasi();
        cfg.Zamanlama.AdimBeklemeMs = (int)_adimBekleme.Value;
        cfg.Zamanlama.TusBeklemeMs = (int)_tusBekleme.Value;
        cfg.Grid.GridTusBeklemeMs = (int)_gridTusBekleme.Value;
        cfg.Zamanlama.PencereTimeoutSn = (int)_pencereTimeout.Value;
        cfg.Zamanlama.OrkaAcilisTimeoutSn = (int)_acilisTimeout.Value;
        cfg.Giris.Sifre = orkaSifre;
        cfg.Giris.FirmaSifresi = firmaSifre;
        cfg.Firma.Kod = firma.Kod;

        var hiz = (double)_hizCarpani.Value;

        var ortak = new Dictionary<string, string>
        {
            ["firmaKodu"] = firma.Kod,
            ["sifre"] = orkaSifre,
            ["firmaSifre"] = firmaSifre,
            ["donem"] = DateTime.Now.ToString("yyyyMM")
        };

        _log.Clear();
        DurumlariSifirla();
        _denetim.Sifirla();

        _bitenBanka = 0;
        _toplamBanka = isler.Count;
        _ozelDurum = string.Empty;
        Volatile.Write(ref _adimBaslangici, 0);
        Volatile.Write(ref _kosuBaslangici, DateTime.UtcNow.Ticks);
        _sureSaati.Start();

        Yaz($"Baslatiliyor: {firma} · {isler.Count} banka · hiz carpani {hiz:0.##}");
        Yaz("ORKA giris sifresi: " + SifreKaynagi(_orkaSifre.Text, cozum.Orka));
        Yaz("Firma sifresi: " + SifreKaynagi(_firmaSifre.Text, cozum.Firma));
        if (cfg.DryRun) Yaz("DryRun acik: Kaydet/OnayGerekir adimlari ATLANACAK.");

        _calisan = Task.Run(() => Dongu(cfg, hiz, ortak, isler, acilisGorevi, bankaGorevi));
        DugmeDurumu();
    }

    /// <summary>
    /// Log'a "hangi sifre nereden geldi" satiri. <b>Sifrenin kendisi yazilmiyor.</b>
    ///
    /// appsettings'teki tek alan yedek olarak duruyor ve sessizce devreye girerse
    /// robot BASKA bir firmanin sifresiyle ORKA'ya girmeyi dener; yanlis sifre
    /// ORKA'yi kilitliyor.
    /// </summary>
    private static string SifreKaynagi(string ekrandaki, SifreDegeri cozulen)
        => ekrandaki.Length > 0 ? "ekrandaki kutu (secili firmanin kaydi)" : cozulen.KaynakAdi;

    /// <summary>
    /// Bir banka satiri icin <c>GridDoldur</c> girdisi; elle liste bos ise null.
    ///
    /// <b>Neden gerekli:</b> arayuzden calistirmada grid verisi HER ZAMAN null
    /// gidiyordu, yani zincirin ORKA'ya gercekten veri yazan tek adimi
    /// (GridDoldur) elle hic denenemiyordu -- adim "liste bos" deyip atliyordu.
    /// Alan bos birakilirsa eski davranis aynen suruyor.
    ///
    /// Hedef satir sayisi ekstre dosyasindan okunuyor: grid'in satir sayisi
    /// ekrandan okunamiyor (ORKA UIA'ya kapali) ama ayni sayi ekstrede duruyor.
    /// Okunamazsa null kaliyor ve GridDoldur "liste bitene kadar" yaziyor.
    /// </summary>
    private GridDoldurVerisi? GridVerisi(BankaIsi isim)
    {
        var satirlar = KarsiHesapListesi.Satirlar(isim.KarsiHesaplar);
        if (satirlar.Count == 0) return null;

        var hedef = EkstreSatirSayisi(isim.DosyaYolu);

        Yaz($"{isim.Banka}: elle girilen {satirlar.Count} karsi hesap kodu kullanilacak" +
            (hedef is null
                ? " (ekstredeki satir sayisi okunamadi)."
                : $" (ekstrede {hedef} satir)."));

        return new GridDoldurVerisi(satirlar, hedef, "elle girilen liste");
    }

    /// <summary>Ekstredeki veri satiri sayisi; okunamazsa null (bilinmiyor).</summary>
    private static int? EkstreSatirSayisi(string dosyaYolu)
    {
        try
        {
            // Sunucu tarafinin kullandigi okuyucunun aynisi: "kac satir var"
            // sorusunun iki yolda ayni yaniti olsun.
            return PkfRobot.Ajan.OrkayaAktarCalistirici.EkstreSatirSayisi(dosyaYolu);
        }
        catch (Exception)
        {
            // Dosya elle secilmis herhangi bir Excel olabilir; okunamamasi
            // calistirmayi engellemez, yalnizca sayi bilinmez.
            return null;
        }
    }

    private void Dongu(RobotConfig cfg, double hiz, Dictionary<string, string> ortak,
                       IReadOnlyList<BankaIsi> isler, string? acilisGorevi, string bankaGorevi)
    {
        var toplam = Math.Max(1, isler.Count);
        var biten = 0;

        var kosucu = new GorevKosucusu(cfg, _denetim, hiz, Yaz,
            adimIlerledi: ilerleme =>
            {
                // Ekrana YAZMIYOR, yalnizca durumu birakiyor: satiri zamanlayici
                // ciziyor ve canli sayac boylece uzun adimlarda da isliyor.
                _adimMetni = ilerleme.ToString();
                _ozelDurum = string.Empty;
                Volatile.Write(ref _adimBaslangici, DateTime.UtcNow.Ticks);
            },
            onayBekleniyor: _ =>
            {
                _ozelDurum = "KAYDET bekleniyor - ORKA'da kaydedip DEVAM'a basin.";
                UiIsle(IlerlemeyiTazele);
            });

        var calistirici = new KuyrukCalistirici(
            _denetim,
            isim =>
            {
                var degiskenler = new Dictionary<string, string>(ortak)
                {
                    ["dosyaYolu"] = isim.DosyaYolu,
                    ["hesapKodu"] = isim.HesapKodu
                };

                kosucu.Calistir(bankaGorevi, degiskenler, GridVerisi(isim));

                biten++;
                _bitenBanka = biten;
                UiIsle(() => _ilerleme.Value = Math.Clamp(biten * 100 / toplam, 0, 100));
            },
            acilis: acilisGorevi is null
                ? null
                : () =>
                {
                    Yaz("Acilis gorevi calisiyor (ORKA aciliyor, firmaya giriliyor)...");
                    kosucu.Calistir(acilisGorevi, ortak);
                },
            durumDegisti: (isim, durum, mesaj) => UiIsle(() => SatirDurumu(isim, durum, mesaj)),
            log: Yaz);

        KuyrukOzeti ozet;
        try
        {
            ozet = calistirici.Calistir(isler);
        }
        catch (Exception ex)
        {
            Yaz($"Kuyruk beklenmedik sekilde durdu: {ex.Message}");
            UiIsle(() => { _sureSaati.Stop(); _calisan = null; DugmeDurumu(); });
            return;
        }

        Yaz($"Bitti: {ozet.Biten} basarili, {ozet.Hatali} hatali, {ozet.Atlanan} atlandi." +
            (ozet.Durduruldu ? " (kullanici durdurdu)" : string.Empty));

        var gecen = SureBicimi.Kisa(TimeSpan.FromTicks(
            Math.Max(0, DateTime.UtcNow.Ticks - Volatile.Read(ref _kosuBaslangici))));

        UiIsle(() =>
        {
            // Once zamanlayici duruyor: yoksa asagidaki ozet satiri bir sonraki
            // tikta canli ilerleme metniyle eziliyordu.
            _sureSaati.Stop();
            Volatile.Write(ref _adimBaslangici, 0);
            _adimMetni = string.Empty;

            _ilerleme.Value = ozet.Hatali == 0 && !ozet.Durduruldu ? 100 : _ilerleme.Value;
            _ilerlemeMetni.Text = $"{ozet.Biten}/{isler.Count} banka bitti · " +
                                  $"{ozet.Hatali} hata · {ozet.Atlanan} atlandi · " +
                                  $"toplam {gecen}";
            _calisan = null;
            DugmeDurumu();
        });
    }

    private void KuyruguKes()
    {
        if (_calisan is null or { IsCompleted: true })
        {
            _denetim.Sifirla();
            return;
        }

        var onay = MessageBox.Show(this,
            "Kuyruk kesilsin mi? Calisan adim bitince durulur, kalan bankalar islenmez. " +
            "ORKA acik kalir ve hangi ekranda kaldigini kontrol etmeniz gerekir.",
            "Kuyrugu kes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (onay == DialogResult.Yes) _denetim.Durdur();
    }

    private string? GorevYolu(string? dosyaAdi)
    {
        if (string.IsNullOrWhiteSpace(dosyaAdi)) return null;

        var yol = Path.Combine(_baglam.GorevlerKlasoru, dosyaAdi);
        return File.Exists(yol) ? yol : null;
    }

    // ================== EKRAN ==================

    private void DugmeDurumu()
    {
        var calisiyor = _calisan is { IsCompleted: false };

        _start.Enabled = !calisiyor;
        _durdur.Enabled = calisiyor && _denetim.Hal == CalismaHali.Calisiyor;
        _devam.Enabled = calisiyor && _denetim.Hal == CalismaHali.Duraklatildi;

        _firma.Enabled = !calisiyor;
        _firmaEkle.Enabled = !calisiyor;
        _firmaCikar.Enabled = !calisiyor && _firma.Items.Count > 1;
        _tablo.Enabled = !calisiyor;
        _bankaEkle.Enabled = !calisiyor;
        _satirCikar.Enabled = !calisiyor;
        _yukari.Enabled = !calisiyor;
        _asagi.Enabled = !calisiyor;

        if (calisiyor && _denetim.Hal == CalismaHali.Duraklatildi)
        {
            // Ozel durum uzerinden: dogrudan yazilan metin, sayacin bir sonraki
            // tikinda kaybolurdu.
            if (_ozelDurum.Length == 0) _ozelDurum = "DURAKLATILDI - DEVAM'a basin.";
            IlerlemeyiTazele();
        }
    }

    /// <summary>
    /// Ilerleme satirini tazeler: hangi adim, o adim ne kadar surdu, kuyruk ne
    /// kadardir calisiyor.
    ///
    /// <b>Neden zamanlayiciyla:</b> canli sayac icin ekranin adim ICINDE de
    /// tazelenmesi gerekiyor. Uzun bir <c>AltPencereDogrula</c> boyunca satir
    /// kipirdamayinca robotun asili kalip kalmadigi anlasilmiyordu.
    /// </summary>
    private void IlerlemeyiTazele()
    {
        var kosuBasi = Volatile.Read(ref _kosuBaslangici);
        var toplamSure = kosuBasi == 0
            ? TimeSpan.Zero
            : TimeSpan.FromTicks(Math.Max(0, DateTime.UtcNow.Ticks - kosuBasi));

        if (_ozelDurum.Length > 0)
        {
            _ilerlemeMetni.Text = $"{_ozelDurum} · toplam {SureBicimi.Kisa(toplamSure)}";
            return;
        }

        var adimBasi = Volatile.Read(ref _adimBaslangici);
        var adimSuresi = adimBasi == 0
            ? TimeSpan.Zero
            : TimeSpan.FromTicks(Math.Max(0, DateTime.UtcNow.Ticks - adimBasi));

        _ilerlemeMetni.Text = IlerlemeMetni.Yaz(_bitenBanka, Math.Max(1, _toplamBanka),
                                                adimBasi == 0 ? null : _adimMetni,
                                                adimSuresi, toplamSure);
    }

    private void DurumlariSifirla()
    {
        foreach (DataGridViewRow satir in _tablo.Rows)
            satir.Cells[DurumSutun].Value = string.Empty;

        _ilerleme.Value = 0;
        _ilerlemeMetni.Text = string.Empty;

        _sureSaati.Stop();
        _adimMetni = string.Empty;
        _ozelDurum = string.Empty;
        Volatile.Write(ref _adimBaslangici, 0);
        Volatile.Write(ref _kosuBaslangici, 0);
    }

    private void SatirDurumu(BankaIsi isim, IsDurumu durum, string? mesaj)
    {
        foreach (DataGridViewRow satir in _tablo.Rows)
        {
            if (satir.Tag is not BankaSatiri banka) continue;
            if (!string.Equals(banka.Banka, isim.Banka, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(banka.DosyaYolu, isim.DosyaYolu, StringComparison.OrdinalIgnoreCase)) continue;

            satir.Cells[DurumSutun].Value = durum switch
            {
                IsDurumu.Calisiyor => "calisiyor",
                IsDurumu.Bitti => "bitti",
                IsDurumu.Hata => "HATA",
                IsDurumu.Atlandi => "atlandi",
                _ => "bekliyor"
            };

            satir.Cells[DurumSutun].ToolTipText = mesaj ?? string.Empty;
            satir.DefaultCellStyle.BackColor = durum switch
            {
                IsDurumu.Hata => Color.MistyRose,
                IsDurumu.Bitti => Color.Honeydew,
                _ => Color.White
            };

            return;
        }
    }

    /// <summary>Log satiri: ekrandaki kutuya ve ajan log'una (Durum sekmesi) birlikte.</summary>
    private void Yaz(string mesaj)
    {
        _baglam.Log.Bilgi(mesaj);

        UiIsle(() =>
        {
            _log.AppendText($"{DateTime.Now:HH:mm:ss}  {mesaj}{Environment.NewLine}");
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
        });
    }

    /// <summary>Arka plan ipliginden ekrana dokunmanin tek yolu.</summary>
    /// <summary>
    /// Sayac zamanlayicisi <see cref="Control.Controls"/> icinde degil; pencere
    /// kapanirken kendiliginden durmaz ve kapali bir kontrole tik gonderirdi.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sureSaati.Stop();
            _sureSaati.Dispose();
        }

        base.Dispose(disposing);
    }

    private void UiIsle(Action is_)
    {
        if (IsDisposed || !IsHandleCreated) return;

        try
        {
            if (InvokeRequired) BeginInvoke(is_);
            else is_();
        }
        catch (ObjectDisposedException)
        {
            // Pencere kapanirken gelen gec bildirim; yok sayilir.
        }
    }
}
