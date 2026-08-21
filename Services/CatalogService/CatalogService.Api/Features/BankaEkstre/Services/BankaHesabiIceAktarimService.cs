using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    public interface IBankaHesabiIceAktarimService
    {
        Task<BankaHesabiIceAktarimSonucDto> IceAktarAsync(Stream excel, CancellationToken ct = default);

        /// <summary>Doğru başlıklara sahip boş şablon; kullanıcı kolon adlarını tahmin etmesin.</summary>
        byte[] SablonUret();
    }

    /// <summary>
    /// Banka hesaplarının xlsx ile toplu içe aktarımı. Hesap planı içe aktarımıyla aynı
    /// kalıp: kolonlar başlık <b>adıyla</b> bulunur (sıraya güvenilmez), doğrulama satır
    /// bazlıdır (bir hatalı satır dosyanın tamamını düşürmez) ve anahtar
    /// <c>OrkaHesapKodu</c> + firma (TenantNo, global sorgu filtresinden gelir).
    ///
    /// Hesap planından tek ayrım: dosyada olmayan mevcut hesaplara <b>dokunulmaz</b>.
    /// Hesap planı ORKA'nın tam listesidir, banka hesabı listesi değildir — kullanıcı bir
    /// bankayı bilerek dosya dışında bırakmış olabilir.
    /// </summary>
    public class BankaHesabiIceAktarimService : IBankaHesabiIceAktarimService
    {
        private readonly CatalogContext _db;
        private readonly IEkstreParserSecici _parserSecici;

        public BankaHesabiIceAktarimService(CatalogContext db, IEkstreParserSecici parserSecici)
        {
            _db = db;
            _parserSecici = parserSecici;
        }

        /// <summary>Banka hesaplarının ORKA'daki ana grubu; başka grup uyarı üretir.</summary>
        private const string BankaAnaGrubu = "102";

        /// <summary>Rapor listelerinin üst sınırı; bozuk bir dosya ekranı doldurmasın.</summary>
        private const int EnFazlaSorun = 100;

        private static readonly string[] KodBasliklari = { "Orka Hesap Kodu", "ORKA Kodu", "Hesap Kodu", "OrkaHesapKodu", "Kod" };
        private static readonly string[] HesapAdiBasliklari = { "Hesap Adı", "Hesap Adi", "HesapAdi" };
        private static readonly string[] BankaAdiBasliklari = { "Banka Adı", "Banka Adi", "BankaAdi", "Banka" };
        private static readonly string[] HesapTipiBasliklari = { "Hesap Tipi", "HesapTipi", "Tip" };
        private static readonly string[] ParaBirimiBasliklari = { "Para Birimi", "ParaBirimi", "Döviz", "Doviz", "Kur" };
        private static readonly string[] ParserBasliklari = { "Parser Tipi", "ParserTipi", "Ayrıştırıcı", "Ayristirici", "Parser" };
        private static readonly string[] IbanBasliklari = { "IBAN", "Iban" };

        /// <summary>Şablonun ve hata mesajlarının kullandığı kanonik başlık sırası.</summary>
        private static readonly string[] SablonBasliklari =
            { "Orka Hesap Kodu", "Hesap Adı", "Banka Adı", "Hesap Tipi", "Para Birimi", "Parser Tipi", "IBAN" };

        public async Task<BankaHesabiIceAktarimSonucDto> IceAktarAsync(Stream excel, CancellationToken ct = default)
        {
            var sonuc = new BankaHesabiIceAktarimSonucDto();

            using var kitap = new XLWorkbook(excel);
            var sayfa = kitap.Worksheets.FirstOrDefault()
                        ?? throw new InvalidDataException("Excel dosyasında sayfa bulunamadı.");

            var kolonlar = BasliklariBul(sayfa);

            // Kod duplikasyonuna karşı sözlük yerine elle doldurma: veritabanında (teorik
            // olarak) aynı kodun iki kez bulunması içe aktarımı patlatmasın.
            var mevcutlar = new Dictionary<string, BankaHesabi>(StringComparer.Ordinal);
            foreach (var hesap in await _db.EkstreBankaHesaplari.ToListAsync(ct))
                mevcutlar.TryAdd(hesap.OrkaHesapKodu, hesap);

            // Hesap planı da firma bazlı; sorgu filtresi tenant'ı zaten daraltıyor.
            var plan = await _db.EkstreHesapPlani
                .AsNoTracking()
                .Select(h => new { h.Kod, h.Aktif })
                .ToListAsync(ct);

            var planKodlari = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var kayit in plan) planKodlari[kayit.Kod] = kayit.Aktif;

            var dosyadaGorulen = new HashSet<string>(StringComparer.Ordinal);
            var sonSatir = sayfa.LastRowUsed()?.RowNumber() ?? 0;

            for (var satirNo = kolonlar.BaslikSatiri + 1; satirNo <= sonSatir; satirNo++)
            {
                ct.ThrowIfCancellationRequested();

                var satir = sayfa.Row(satirNo);
                if (satir.IsEmpty()) continue;

                var kodHam = Hucre(satir, kolonlar.Kod);
                var hesapAdi = Hucre(satir, kolonlar.HesapAdi);
                var bankaAdi = Hucre(satir, kolonlar.BankaAdi);
                var tipHam = Hucre(satir, kolonlar.HesapTipi);
                var paraHam = Hucre(satir, kolonlar.ParaBirimi);
                var parserHam = Hucre(satir, kolonlar.Parser);
                var ibanHam = Hucre(satir, kolonlar.Iban);

                if (kodHam.Length == 0 && hesapAdi.Length == 0 && bankaAdi.Length == 0 &&
                    tipHam.Length == 0 && paraHam.Length == 0) continue;

                sonuc.Okunan++;

                var hatalar = new List<IceAktarimSatirSorunuDto>();
                var uyarilar = new List<IceAktarimSatirSorunuDto>();

                var kod = Normalizasyon.HesapKoduNormalize(kodHam);

                if (kod.Length == 0)
                {
                    hatalar.Add(Sorun(satirNo, nameof(BankaHesabi.OrkaHesapKodu), "ORKA hesap kodu boş."));
                }
                else
                {
                    if (!dosyadaGorulen.Add(kod))
                        hatalar.Add(Sorun(satirNo, nameof(BankaHesabi.OrkaHesapKodu),
                            $"'{kod}' dosyada birden fazla satırda geçiyor; ilk satır işlendi."));

                    if (!planKodlari.TryGetValue(kod, out var planAktif))
                        hatalar.Add(Sorun(satirNo, nameof(BankaHesabi.OrkaHesapKodu),
                            $"'{kod}' hesap planında yok. Önce hesap planını içe aktarın."));
                    else if (!planAktif)
                        hatalar.Add(Sorun(satirNo, nameof(BankaHesabi.OrkaHesapKodu),
                            $"'{kod}' hesap planında pasif. ORKA'da hesap kapalıysa banka hesabı tanımlanmaz."));

                    // Banka hesabı 102'de durur; başka grup büyük olasılıkla yanlış kod ama
                    // firmanın hesap planı farklı kurulmuş olabilir, satır yine de işlenir.
                    if (Normalizasyon.AnaGrup(kod) != BankaAnaGrubu)
                        uyarilar.Add(Sorun(satirNo, nameof(BankaHesabi.OrkaHesapKodu),
                            $"'{kod}' {BankaAnaGrubu} ile başlamıyor; banka hesabı kodu olmayabilir. Yine de eklendi."));
                }

                if (hesapAdi.Length == 0)
                    hatalar.Add(Sorun(satirNo, nameof(BankaHesabi.HesapAdi), "Hesap adı boş."));

                if (bankaAdi.Length == 0)
                    hatalar.Add(Sorun(satirNo, nameof(BankaHesabi.BankaAdi), "Banka adı boş."));

                var tip = HesapTipiCoz(tipHam);
                if (tip is null)
                    hatalar.Add(Sorun(satirNo, nameof(BankaHesabi.HesapTipi),
                        $"Tanınmayan hesap tipi: '{tipHam}'. Geçerli değerler: Vadesiz, Vadeli."));

                var paraBirimi = ParaBirimiCoz(paraHam);
                if (paraBirimi is null)
                    hatalar.Add(Sorun(satirNo, nameof(BankaHesabi.ParaBirimi),
                        $"Tanınmayan para birimi: '{paraHam}'. Üç harfli ISO kodu bekleniyor (TL/TRY, USD, EUR)."));

                // Parser boş bırakılabilir: hesap tanımlanır, ekstresi yüklenemez.
                string? parserTipi = null;
                var parser = parserHam.Trim();
                if (parser.Length > 0)
                {
                    var secilen = _parserSecici.Sec(parser);
                    if (secilen is null)
                        hatalar.Add(Sorun(satirNo, nameof(BankaHesabi.ParserTipi),
                            $"Tanımsız ayrıştırıcı: '{parser}'. Seçilebilir tipler: {ParserListesi()}."));
                    else
                        parserTipi = secilen.ParserTipi;
                }

                if (hatalar.Count > 0)
                {
                    sonuc.Atlanan++;
                    Ekle(sonuc.Hatalar, hatalar);
                    continue;
                }

                Ekle(sonuc.Uyarilar, uyarilar);

                var iban = ibanHam.Replace(" ", string.Empty).ToUpperInvariant();

                if (mevcutlar.TryGetValue(kod, out var mevcut))
                {
                    mevcut.HesapAdi = Normalizasyon.Kirp(hesapAdi, 200);
                    mevcut.BankaAdi = Normalizasyon.Kirp(bankaAdi, 100);
                    mevcut.HesapTipi = tip!.Value;
                    mevcut.ParaBirimi = paraBirimi!;
                    if (iban.Length > 0) mevcut.Iban = Normalizasyon.Kirp(iban, 34);
                    // Boş parser hücresi çalışan bir tanımı silmesin.
                    if (parserTipi is not null) mevcut.ParserTipi = parserTipi;
                    // Aktif ve katman bayrakları dosyada yok: kullanıcının ekrandaki
                    // kararı korunur, içe aktarım pasif hesabı geri açmaz.
                    sonuc.Guncellenen++;

                    if (mevcut.ParserTipi.Length == 0)
                        Ekle(sonuc.Uyarilar, new[] { ParsersizUyari(satirNo) });
                }
                else
                {
                    _db.EkstreBankaHesaplari.Add(new BankaHesabi
                    {
                        OrkaHesapKodu = kod,
                        HesapAdi = Normalizasyon.Kirp(hesapAdi, 200),
                        BankaAdi = Normalizasyon.Kirp(bankaAdi, 100),
                        HesapTipi = tip!.Value,
                        ParaBirimi = paraBirimi!,
                        Iban = iban.Length == 0 ? null : Normalizasyon.Kirp(iban, 34),
                        ParserTipi = parserTipi ?? string.Empty,
                        Aktif = true
                    });
                    sonuc.Eklenen++;

                    if (parserTipi is null)
                        Ekle(sonuc.Uyarilar, new[] { ParsersizUyari(satirNo) });
                }
            }

            await _db.SaveChangesAsync(ct);
            return sonuc;
        }

        public byte[] SablonUret()
        {
            using var kitap = new XLWorkbook();
            var sayfa = kitap.Worksheets.Add("Banka Hesapları");

            for (var i = 0; i < SablonBasliklari.Length; i++)
            {
                var hucre = sayfa.Cell(1, i + 1);
                hucre.Value = SablonBasliklari[i];
                hucre.Style.Font.SetBold();
                hucre.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#EDF2F7"));
            }

            sayfa.Column(1).Width = 18;
            sayfa.Column(2).Width = 34;
            sayfa.Column(3).Width = 18;
            sayfa.Column(4).Width = 12;
            sayfa.Column(5).Width = 12;
            sayfa.Column(6).Width = 22;
            sayfa.Column(7).Width = 30;
            sayfa.SheetView.FreezeRows(1);

            // İkinci sayfa yalnız açıklama; içe aktarım her zaman ilk sayfayı okur.
            var bilgi = kitap.Worksheets.Add("Açıklama");
            var satirlar = new (string Alan, string Aciklama)[]
            {
                ("Orka Hesap Kodu", "Zorunlu. Boşluklu yazın, format değiştirilmez: 102 1 32 87. Hesap planında kayıtlı olmalı."),
                ("Hesap Adı", "Zorunlu. Hesabın ORKA'daki adı."),
                ("Banka Adı", "Zorunlu. Vakıfbank, Ziraat, Akbank, İş Bankası, TEB…"),
                ("Hesap Tipi", "Zorunlu. Vadesiz veya Vadeli."),
                ("Para Birimi", "Zorunlu. TL (TRY), USD, EUR."),
                ("Parser Tipi", $"İsteğe bağlı. Boşsa hesap tanımlanır ama ekstresi yüklenemez. Geçerli değerler: {ParserListesi()}."),
                ("IBAN", "İsteğe bağlı."),
                (string.Empty, string.Empty),
                ("Not", "Kolonlar başlık adıyla bulunur; sıraları değiştirilebilir. Aynı ORKA kodu varsa kayıt güncellenir, yoksa eklenir; dosyada olmayan hesaplara dokunulmaz.")
            };

            for (var i = 0; i < satirlar.Length; i++)
            {
                bilgi.Cell(i + 1, 1).Value = satirlar[i].Alan;
                bilgi.Cell(i + 1, 1).Style.Font.SetBold();
                bilgi.Cell(i + 1, 2).Value = satirlar[i].Aciklama;
            }

            bilgi.Column(1).Width = 18;
            bilgi.Column(2).Width = 110;

            using var bellek = new MemoryStream();
            kitap.SaveAs(bellek);
            return bellek.ToArray();
        }

        // ---- Yardımcılar ----

        private string ParserListesi()
            => string.Join(", ", _parserSecici.Hepsi.Select(p => p.ParserTipi));

        private static IceAktarimSatirSorunuDto ParsersizUyari(int satirNo)
            => Sorun(satirNo, nameof(BankaHesabi.ParserTipi),
                     "Ayrıştırıcı boş; hesap tanımlandı ama ekstresi yüklenemez.");

        private static IceAktarimSatirSorunuDto Sorun(int satirNo, string alan, string mesaj)
            => new() { SatirNo = satirNo, Field = alan, Message = mesaj };

        private static void Ekle(List<IceAktarimSatirSorunuDto> hedef, IEnumerable<IceAktarimSatirSorunuDto> yeniler)
        {
            foreach (var sorun in yeniler)
            {
                if (hedef.Count >= EnFazlaSorun) return;
                hedef.Add(sorun);
            }
        }

        private static string Hucre(IXLRow satir, int? kolon)
            => kolon is null ? string.Empty : satir.Cell(kolon.Value).GetString().Trim();

        private static HesapTipi? HesapTipiCoz(string ham) => Anahtar(ham) switch
        {
            "VADESIZ" => HesapTipi.Vadesiz,
            "VADELI" => HesapTipi.Vadeli,
            _ => null
        };

        /// <summary>TL yazımı ORKA/banka dilinde yaygın; veritabanı ISO kodu tutar.</summary>
        private static string? ParaBirimiCoz(string ham)
        {
            var anahtar = Anahtar(ham);
            if (anahtar.Length == 0) return null;
            if (anahtar is "TL" or "TRL" or "TRY") return "TRY";
            return anahtar.Length == 3 ? anahtar : null;
        }

        /// <summary>
        /// Başlık/değer karşılaştırma anahtarı: Türkçe karakter sadeleştirilir, harf-rakam
        /// dışı her şey atılır. "Hesap Adı", "hesap adi" ve "HESAP_ADI" aynı anahtara düşer.
        /// </summary>
        private static string Anahtar(string? metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return string.Empty;

            var sade = Normalizasyon.TurkceSadelestir(metin.Trim());
            return new string(sade.Where(char.IsLetterOrDigit).ToArray());
        }

        private record Kolonlar(int BaslikSatiri, int Kod, int HesapAdi, int BankaAdi,
                                int HesapTipi, int ParaBirimi, int? Parser, int? Iban);

        /// <summary>
        /// Başlık satırını ilk 20 satırda <b>adla</b> arar; kolon sırası önemsizdir.
        /// Zorunlu kolonlardan biri yoksa dosya hiç işlenmez (satır bazlı hata değil,
        /// dosya sözleşmesi hatası).
        /// </summary>
        private static Kolonlar BasliklariBul(IXLWorksheet sayfa)
        {
            var sonTaranan = Math.Min(sayfa.LastRowUsed()?.RowNumber() ?? 0, 20);

            for (var satirNo = 1; satirNo <= sonTaranan; satirNo++)
            {
                var harita = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var hucre in sayfa.Row(satirNo).CellsUsed())
                {
                    var anahtar = Anahtar(hucre.GetString());
                    if (anahtar.Length > 0) harita.TryAdd(anahtar, hucre.Address.ColumnNumber);
                }

                var kod = Ara(harita, KodBasliklari);
                var hesapAdi = Ara(harita, HesapAdiBasliklari);
                var bankaAdi = Ara(harita, BankaAdiBasliklari);
                var tip = Ara(harita, HesapTipiBasliklari);
                var para = Ara(harita, ParaBirimiBasliklari);

                if (kod is null || hesapAdi is null || bankaAdi is null || tip is null || para is null) continue;

                return new Kolonlar(satirNo, kod.Value, hesapAdi.Value, bankaAdi.Value, tip.Value, para.Value,
                                    Ara(harita, ParserBasliklari), Ara(harita, IbanBasliklari));
            }

            throw new InvalidDataException(
                "Başlık satırı bulunamadı. Zorunlu kolonlar: " +
                string.Join(", ", SablonBasliklari.Take(5)) +
                ". Örnek şablonu indirip kullanabilirsiniz.");
        }

        private static int? Ara(Dictionary<string, int> harita, string[] adaylar)
        {
            foreach (var ad in adaylar)
                if (harita.TryGetValue(Anahtar(ad), out var kolon)) return kolon;
            return null;
        }
    }
}
