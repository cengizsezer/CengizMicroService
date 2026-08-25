using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Kapsam;
using CatalogService.Api.Infrastructure.Context;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    public interface IOgrenilenEslesmeIceAktarimService
    {
        Task<OgrenilenEslesmeIceAktarimSonucDto> IceAktarAsync(Stream excel, CancellationToken ct = default);

        /// <summary>Doğru başlıklara sahip boş şablon; kullanıcı kolon adlarını tahmin etmesin.</summary>
        byte[] SablonUret();
    }

    /// <summary>
    /// Öğrenilen eşleşmelerin xlsx ile toplu içe aktarımı. Öğrenme tablosu şimdiye kadar
    /// yalnız onay ekranından tek tek doluyordu; ORKA yevmiyesinden çıkarılmış doğrulanmış
    /// eşleşmeler (yüzlerce satır) elle girilemez.
    ///
    /// <b>Eşleştirme mantığına dokunmaz.</b> Bu yalnız yeni bir yazma yolu: kayıtlar
    /// <see cref="HesapEslesmeService.OgrenAsync"/>'in yazdığı biçimin aynısıyla
    /// (<see cref="AnahtarTipi.UnvanCekirdek"/>, ayırt edici eksiz, sade çekirdek anahtarı)
    /// yazılır ve sonraki ekstrede geçmiş onay katmanından çözülür.
    ///
    /// Banka hesabı içe aktarımıyla aynı kalıp: kolonlar başlık <b>adıyla</b> bulunur
    /// (sıraya güvenilmez), doğrulama satır bazlıdır (bir hatalı satır dosyanın tamamını
    /// düşürmez) ve kapsam firmadır.
    ///
    /// Banka hesabından tek ayrım: <b>üzerine yazılmaz</b>. Aynı anahtar için kayıt varsa
    /// satır atlanır. Kullanıcının onay ekranında verdiği karar, geçmişten türetilen
    /// kayda göre önceliklidir.
    /// </summary>
    public class OgrenilenEslesmeIceAktarimService : IOgrenilenEslesmeIceAktarimService
    {
        private readonly CatalogContext _db;
        private readonly IBankaFirmaKapsami _kapsam;

        public OgrenilenEslesmeIceAktarimService(CatalogContext db, IBankaFirmaKapsami kapsam)
        {
            _db = db;
            _kapsam = kapsam;
        }

        /// <summary>
        /// Öğrenilecek anahtarın en kısa uzunluğu. Kısa bir çekirdek ("ADAY", "PKF ADAY")
        /// gelecekte çok sayıda alakasız satırı tek cariye bağlar; öğrenme tablosundan
        /// gelen kayıt onaya bile düşmediği için hata sessiz kalırdı.
        /// </summary>
        private const int EnKisaAnahtar = 8;

        /// <summary>Rapor listelerinin üst sınırı; bozuk bir dosya ekranı doldurmasın.</summary>
        private const int EnFazlaSorun = 100;

        private static readonly string[] AnahtarBasliklari =
            { "Anahtar Çekirdek", "Anahtar Cekirdek", "AnahtarCekirdek", "Anahtar", "Unvan Çekirdeği", "Unvan Cekirdegi" };
        private static readonly string[] KodBasliklari =
            { "Hesap Kodu", "HesapKodu", "Orka Hesap Kodu", "ORKA Kodu", "Kod" };
        private static readonly string[] HesapAdiBasliklari = { "Hesap Adı", "Hesap Adi", "HesapAdi" };
        private static readonly string[] YonBasliklari = { "Yön", "Yon" };
        private static readonly string[] KullanimBasliklari =
            { "Kullanım Sayısı", "Kullanim Sayisi", "KullanimSayisi", "Kullanım", "Kullanim" };
        private static readonly string[] SonKullanimBasliklari =
            { "Son Kullanım", "Son Kullanim", "SonKullanim", "Son Tarih" };

        /// <summary>
        /// Şablonun ve hata mesajlarının kullandığı kanonik başlık sırası. İlk ikisi zorunlu
        /// (hata mesajı <c>Take(2)</c> ile bu listeden okunuyor), kalanı isteğe bağlı.
        /// </summary>
        private static readonly string[] SablonBasliklari =
            { "Anahtar Çekirdek", "Hesap Kodu", "Hesap Adı", "Yön", "Kullanım Sayısı", "Son Kullanım" };

        /// <summary>Dosyadaki <c>Yön</c> değerinin karşılığı; boş hücre de buraya düşer.</summary>
        private enum YonSecimi { Giren, Cikan, Farketmez }

        public async Task<OgrenilenEslesmeIceAktarimSonucDto> IceAktarAsync(Stream excel, CancellationToken ct = default)
        {
            var sonuc = new OgrenilenEslesmeIceAktarimSonucDto();

            using var kitap = new XLWorkbook(excel);
            var sayfa = kitap.Worksheets.FirstOrDefault()
                        ?? throw new InvalidDataException("Excel dosyasında sayfa bulunamadı.");

            var kolonlar = BasliklariBul(sayfa);

            // Doğrulama hesap planından yapılır: doğrulanmamış kod öğrenme tablosuna
            // yazılmaz (tekli düzenlemedeki kuralın aynısı).
            var plan = await _db.EkstreHesapPlani
                .Where(h => h.FirmaId == _kapsam.FirmaId)
                .AsNoTracking()
                .Select(h => new { h.Kod, h.Ad })
                .ToListAsync(ct);

            var planKodlari = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kayit in plan) planKodlari.TryAdd(kayit.Kod, kayit.Ad);

            // Firmanın kendi adı asla öğrenilmemeli: karşı taraf sanılırsa açıklamada
            // firmanın unvanı geçen her satır o hesaba eşleşir.
            var kimlik = await KimlikKurAsync(ct);

            // Mevcut kayıtlar: sade çekirdek anahtarı (ayırt edici eksiz) + yön.
            // Ayırt edici ekli kayıtlar ayrı bir anahtardır; eşleştirici önce genişletilmiş,
            // tutmazsa sade anahtarı dener. Bu yüzden onlar "mevcut" saymaz, yalnız uyarır.
            var mevcutlar = new HashSet<string>(StringComparer.Ordinal);
            var ekliCekirdekler = new HashSet<string>(StringComparer.Ordinal);

            var eslesmeler = await _db.EkstreHesapEslesmeleri
                .Where(e => e.FirmaId == _kapsam.FirmaId && e.AnahtarTipi == AnahtarTipi.UnvanCekirdek)
                .AsNoTracking()
                .Select(e => new { e.AnahtarCekirdek, e.AyirtEdiciEk, e.Yon })
                .ToListAsync(ct);

            foreach (var e in eslesmeler)
            {
                if (string.IsNullOrWhiteSpace(e.AyirtEdiciEk)) mevcutlar.Add(Kimlik(e.AnahtarCekirdek, e.Yon));
                else ekliCekirdekler.Add(e.AnahtarCekirdek);
            }

            // Dosya içi tekrar denetimi: aynı çekirdek yalnız yönleri kesişmiyorsa
            // (biri Giren, diğeri Çıkan) iki satırda geçebilir.
            var dosyadaGorulen = new Dictionary<string, int>(StringComparer.Ordinal);
            var sonSatir = sayfa.LastRowUsed()?.RowNumber() ?? 0;

            for (var satirNo = kolonlar.BaslikSatiri + 1; satirNo <= sonSatir; satirNo++)
            {
                ct.ThrowIfCancellationRequested();

                var satir = sayfa.Row(satirNo);
                if (satir.IsEmpty()) continue;

                var anahtarHam = Hucre(satir, kolonlar.Anahtar);
                var kodHam = Hucre(satir, kolonlar.Kod);
                var yonHam = Hucre(satir, kolonlar.Yon);

                if (anahtarHam.Length == 0 && kodHam.Length == 0) continue;

                sonuc.Okunan++;

                var hatalar = new List<IceAktarimSatirSorunuDto>();
                var uyarilar = new List<IceAktarimSatirSorunuDto>();

                // Dosyadaki değer zaten normalize gelse bile sistemin kendi
                // normalizasyonundan geçirilir; aksi hâlde eşleşme "neredeyse" tutar.
                var cekirdek = Normalizasyon.UnvanCekirdek(anahtarHam);

                if (cekirdek.Length == 0)
                    hatalar.Add(Sorun(satirNo, nameof(HesapEslesmesi.AnahtarCekirdek), "Anahtar boş."));
                else if (cekirdek.Length < EnKisaAnahtar)
                    hatalar.Add(Sorun(satirNo, nameof(HesapEslesmesi.AnahtarCekirdek),
                        $"'{cekirdek}' {EnKisaAnahtar} karakterden kısa; bu kadar kısa bir anahtar " +
                        "alakasız satırları da bu hesaba bağlar."));
                else if (kimlik.Kendisi(cekirdek))
                    hatalar.Add(Sorun(satirNo, nameof(HesapEslesmesi.AnahtarCekirdek),
                        $"'{cekirdek}' hesap sahibinin kendi adı. Firmanın kendi unvanı karşı taraf olarak öğrenilemez."));

                var kod = Normalizasyon.HesapKoduNormalize(kodHam);
                string? planAdi = null;

                if (kod.Length == 0)
                    hatalar.Add(Sorun(satirNo, nameof(HesapEslesmesi.HesapKodu), "Hesap kodu boş."));
                else if (!planKodlari.TryGetValue(kod, out planAdi))
                    hatalar.Add(Sorun(satirNo, nameof(HesapEslesmesi.HesapKodu),
                        $"'{kod}' hesap planında yok. Önce hesap planını içe aktarın."));

                var yon = YonCoz(yonHam);
                if (yon is null)
                    hatalar.Add(Sorun(satirNo, nameof(HesapEslesmesi.Yon),
                        $"Tanınmayan yön: '{yonHam}'. Geçerli değerler: Giren, Çıkan, Farketmez (boş bırakılırsa Farketmez)."));

                var yonler = Yonler(yon ?? YonSecimi.Farketmez);

                if (cekirdek.Length > 0 && yon is not null)
                {
                    var oncekiSatir = TekrarBul(dosyadaGorulen, cekirdek, yonler);
                    if (oncekiSatir is not null)
                        hatalar.Add(Sorun(satirNo, nameof(HesapEslesmesi.AnahtarCekirdek),
                            $"'{cekirdek}' dosyada {oncekiSatir} numaralı satırda da geçiyor; hangi kodun " +
                            "geçerli olduğu belirsiz."));
                }

                if (hatalar.Count > 0)
                {
                    sonuc.Hatali++;
                    Ekle(sonuc.Hatalar, hatalar);
                    continue;
                }

                foreach (var y in yonler) dosyadaGorulen[Kimlik(cekirdek, y)] = satirNo;

                if (ekliCekirdekler.Contains(cekirdek))
                    uyarilar.Add(Sorun(satirNo, nameof(HesapEslesmesi.AyirtEdiciEk),
                        $"'{cekirdek}' için ayırt edici kelimeli bir kayıt zaten var; o kayıt önce denenir, " +
                        "bu satır yalnız ona uymayan satırlarda geçerli olur."));

                var kullanim = KullanimCoz(Hucre(satir, kolonlar.Kullanim));
                var sonKullanim = TarihCoz(satir, kolonlar.SonKullanim) ?? DateTime.Now;

                var eklenen = 0;
                var atlanan = 0;

                foreach (var y in yonler)
                {
                    if (!mevcutlar.Add(Kimlik(cekirdek, y)))
                    {
                        atlanan++;
                        continue;
                    }

                    _db.EkstreHesapEslesmeleri.Add(new HesapEslesmesi
                    {
                        FirmaId = _kapsam.FirmaId,
                        AnahtarTipi = AnahtarTipi.UnvanCekirdek,
                        AnahtarCekirdek = Normalizasyon.Kirp(cekirdek, 200),
                        AyirtEdiciEk = null,
                        Yon = y,
                        HesapKodu = kod,
                        // Ad bilgi amaçlı; kaynağı dosya değil hesap planı.
                        HesapAdi = Normalizasyon.Kirp(planAdi, 200) is { Length: > 0 } ad ? ad : null,
                        KullanimSayisi = kullanim,
                        SonKullanim = sonKullanim
                    });

                    eklenen++;
                }

                sonuc.EklenenKayit += eklenen;

                if (eklenen > 0)
                {
                    sonuc.Eklenen++;
                    Ekle(sonuc.Uyarilar, uyarilar);

                    // Farketmez satırın bir yönü zaten kullanıcının kararıyla doluysa
                    // yalnız boş yön yazılır; korunan karar görünür olsun.
                    if (atlanan > 0)
                        Ekle(sonuc.Uyarilar, new[] { Sorun(satirNo, nameof(HesapEslesmesi.Yon),
                            $"'{cekirdek}' için bir yönde kayıt zaten vardı; o yön korundu, diğeri eklendi.") });
                }
                else
                {
                    sonuc.Atlanan++;
                    Ekle(sonuc.Uyarilar, new[] { Sorun(satirNo, nameof(HesapEslesmesi.AnahtarCekirdek),
                        $"'{cekirdek}' için kayıt zaten var; mevcut karar korundu.") });
                }
            }

            await _db.SaveChangesAsync(ct);
            return sonuc;
        }

        public byte[] SablonUret()
        {
            using var kitap = new XLWorkbook();
            var sayfa = kitap.Worksheets.Add("Öğrenilen Eşleşmeler");

            for (var i = 0; i < SablonBasliklari.Length; i++)
            {
                var hucre = sayfa.Cell(1, i + 1);
                hucre.Value = SablonBasliklari[i];
                hucre.Style.Font.SetBold();
                hucre.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#EDF2F7"));
            }

            sayfa.Column(1).Width = 38;
            sayfa.Column(2).Width = 16;
            sayfa.Column(3).Width = 34;
            sayfa.Column(4).Width = 12;
            sayfa.Column(5).Width = 14;
            sayfa.Column(6).Width = 14;
            sayfa.SheetView.FreezeRows(1);

            // İkinci sayfa yalnız açıklama; içe aktarım her zaman ilk sayfayı okur.
            var bilgi = kitap.Worksheets.Add("Açıklama");
            var satirlar = new (string Alan, string Aciklama)[]
            {
                ("Anahtar Çekirdek", "Zorunlu. Karşı tarafın unvan çekirdeği: \"NAOS ISTANBUL KOZMETIK\". " +
                                     $"Değer içe aktarılırken yeniden normalize edilir. En az {EnKisaAnahtar} karakter."),
                ("Hesap Kodu", "Zorunlu. Boşluklu ORKA kodu, aynen saklanır: 120 N15. Hesap planında kayıtlı olmalı."),
                ("Hesap Adı", "İsteğe bağlı, yalnız bilgi. Kaydedilen ad hesap planından okunur."),
                ("Yön", "İsteğe bağlı. Giren, Çıkan veya Farketmez. Boşsa Farketmez — " +
                        "iki yön için de birer kayıt yazılır."),
                ("Kullanım Sayısı", "İsteğe bağlı. Boşsa 1."),
                ("Son Kullanım", "İsteğe bağlı, gg.aa.yyyy. Boşsa içe aktarım tarihi."),
                (string.Empty, string.Empty),
                ("Not", "Kolonlar başlık adıyla bulunur; sıraları değiştirilebilir. Aynı anahtar için " +
                        "kayıt zaten varsa satır ATLANIR — onay ekranından verilen karar korunur. " +
                        "Hatalı satır dosyanın kalanını düşürmez."),
                ("Not", "Firmanın kendi unvanını içeren anahtarlar reddedilir: hesap sahibinin kendi adı " +
                        "karşı taraf olarak öğrenilemez.")
            };

            for (var i = 0; i < satirlar.Length; i++)
            {
                bilgi.Cell(i + 1, 1).Value = satirlar[i].Alan;
                bilgi.Cell(i + 1, 1).Style.Font.SetBold();
                bilgi.Cell(i + 1, 2).Value = satirlar[i].Aciklama;
            }

            bilgi.Column(1).Width = 20;
            bilgi.Column(2).Width = 110;

            using var bellek = new MemoryStream();
            kitap.SaveAs(bellek);
            return bellek.ToArray();
        }

        // ---- Yardımcılar ----

        /// <summary>
        /// Firmanın hesap sahibi kimliği; banka hesabı satırlarında duran unvan ve takma
        /// adlardan kurulur (bkz. <c>BankaHesabiService.HesapSahibiGetAsync</c>).
        /// </summary>
        private async Task<HesapSahibiKimligi> KimlikKurAsync(CancellationToken ct)
        {
            var hesaplar = await _db.EkstreBankaHesaplari
                .Where(h => h.FirmaId == _kapsam.FirmaId)
                .AsNoTracking()
                .Select(h => new { h.HesapSahibiUnvani, h.HesapSahibiTakmaAdlari })
                .ToListAsync(ct);

            return HesapSahibiKimligi.Kur(
                hesaplar.SelectMany(h => HesapSahibiKimligi.Ayikla(h.HesapSahibiUnvani)
                                          .Concat(HesapSahibiKimligi.Ayikla(h.HesapSahibiTakmaAdlari))));
        }

        private static string Kimlik(string cekirdek, Yon yon) => $"{cekirdek}|{(int)yon}";

        /// <summary>Aynı çekirdeği <b>kesişen yönle</b> taşıyan önceki satırın numarası; yoksa null.</summary>
        private static int? TekrarBul(Dictionary<string, int> gorulen, string cekirdek, IReadOnlyList<Yon> yonler)
        {
            foreach (var yon in yonler)
                if (gorulen.TryGetValue(Kimlik(cekirdek, yon), out var satirNo)) return satirNo;

            return null;
        }

        /// <summary>
        /// <see cref="HesapEslesmesi.Yon"/> yalnız Giren/Çıkan tutar (ekstre satırının yönü
        /// her zaman kesindir). "Farketmez" bu yüzden iki kayıt olarak yazılır.
        /// </summary>
        private static IReadOnlyList<Yon> Yonler(YonSecimi secim) => secim switch
        {
            YonSecimi.Giren => new[] { Yon.Giren },
            YonSecimi.Cikan => new[] { Yon.Cikan },
            _ => new[] { Yon.Giren, Yon.Cikan }
        };

        private static YonSecimi? YonCoz(string ham) => Anahtar(ham) switch
        {
            "" or "FARKETMEZ" or "FARKETMIYOR" or "HERIKISI" => YonSecimi.Farketmez,
            "GIREN" => YonSecimi.Giren,
            "CIKAN" => YonSecimi.Cikan,
            _ => null
        };

        /// <summary>Okunamayan sayı geçmişi düşürmesin; öğrenilen karar sayaçtan bağımsız geçerli.</summary>
        private static int KullanimCoz(string ham)
        {
            if (ham.Length == 0) return 1;

            var sade = new string(ham.Where(char.IsDigit).ToArray());
            return int.TryParse(sade, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sayi) && sayi > 0
                ? sayi
                : 1;
        }

        /// <summary>
        /// Tarih hücresi: gerçek Excel tarihi olabilir de metin de. Metin biçimi gg.aa.yyyy;
        /// tanınmayan değer satırı düşürmez, içe aktarım tarihine düşer.
        /// </summary>
        private static DateTime? TarihCoz(IXLRow satir, int? kolon)
        {
            if (kolon is null) return null;

            var hucre = satir.Cell(kolon.Value);
            if (hucre.IsEmpty()) return null;

            if (hucre.DataType == XLDataType.DateTime && hucre.TryGetValue<DateTime>(out var tarih)) return tarih;

            var metin = hucre.GetString().Trim();
            if (metin.Length == 0) return null;

            var kultur = CultureInfo.GetCultureInfo("tr-TR");
            string[] bicimler = { "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yyyy HH:mm", "yyyy-MM-dd" };

            if (DateTime.TryParseExact(metin, bicimler, kultur, DateTimeStyles.None, out var kesin)) return kesin;
            return DateTime.TryParse(metin, kultur, DateTimeStyles.None, out var serbest) ? serbest : null;
        }

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

        /// <summary>
        /// Başlık/değer karşılaştırma anahtarı: Türkçe karakter sadeleştirilir, harf-rakam
        /// dışı her şey atılır. "Anahtar Çekirdek", "anahtar cekirdek" ve "ANAHTAR_CEKIRDEK"
        /// aynı anahtara düşer.
        /// </summary>
        private static string Anahtar(string? metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return string.Empty;

            var sade = Normalizasyon.TurkceSadelestir(metin.Trim());
            return new string(sade.Where(char.IsLetterOrDigit).ToArray());
        }

        private record Kolonlar(int BaslikSatiri, int Anahtar, int Kod,
                                int? HesapAdi, int? Yon, int? Kullanim, int? SonKullanim);

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

                var anahtarKolonu = Ara(harita, AnahtarBasliklari);
                var kod = Ara(harita, KodBasliklari);

                if (anahtarKolonu is null || kod is null) continue;

                return new Kolonlar(satirNo, anahtarKolonu.Value, kod.Value,
                                    Ara(harita, HesapAdiBasliklari), Ara(harita, YonBasliklari),
                                    Ara(harita, KullanimBasliklari), Ara(harita, SonKullanimBasliklari));
            }

            throw new InvalidDataException(
                "Başlık satırı bulunamadı. Zorunlu kolonlar: " +
                string.Join(", ", SablonBasliklari.Take(2)) +
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
