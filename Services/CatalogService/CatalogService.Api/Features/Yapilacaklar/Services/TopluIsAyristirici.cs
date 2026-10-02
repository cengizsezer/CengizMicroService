using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CatalogService.Api.Features.Sistemler.Domain;
using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;

namespace CatalogService.Api.Features.Yapilacaklar.Services
{
    /// <summary>
    /// Firma işleri kartına Ctrl+V ile yapıştırılan tabloyu ayrıştırır. Saf fonksiyon: veritabanı
    /// yok, sistem listesi ve firmanın mevcut başlıkları parametre.
    ///
    /// Felsefe mizan yüklemenin aynısı: YAPIŞTIR → NE ANLADIĞINI GÖSTER → ONAYLAT → SONRA EKLE.
    /// Bu sınıf yalnız "ne anladığını" üretir; hiçbir şey eklemez.
    ///
    /// Excel panosu: <c>text/plain</c>, satırlar <c>\r\n</c>, hücreler <c>\t</c>. Tek kolon da
    /// geçerli — her satır bir iş başlığı.
    ///
    /// Satır durumu: kırmızı (okunamadı) EKLENMEZ — başlık yok ya da yazılmış tekrar/gün
    /// anlaşılmadı. Amber EKLENİR ama işaretlenir — kullanıcı on işi birden giriyor, eksiği sonra
    /// prosedür panelinden doldurur: kullanılamayan değer boş kalır (program listede yok, e-posta
    /// okunamadı, sınırı aşan metin kesildi) ya da yazılmamış tekrar/gün için varsayılan
    /// ("her ay · dönem sonu") uygulanır, tabloda "(varsayılan — kontrol et)" görünür (Prompt 11B).
    /// Program listede yoksa yeni Sistem kaydı OTOMATİK AÇILMAZ (ortak liste kirlenmesin, bkz. Prompt 9).
    /// </summary>
    public static class TopluIsAyristirici
    {
        private static readonly CultureInfo Tr = new("tr-TR");

        /// <summary>Kolon başlığı eşanlamlıları (katlanmış: Türkçe karakter, büyük/küçük, boşluk farksız).</summary>
        private static readonly (TopluAlan Alan, string[] Adlar)[] AlanAdlari =
        {
            (TopluAlan.Baslik, new[] { "baslik", "is", "is adi", "isin adi", "gorev" }),
            (TopluAlan.Tekrar, new[] { "tekrar", "siklik", "periyot", "donem" }),
            (TopluAlan.Gun, new[] { "gun", "son gun", "tarih", "ayin gunu" }),
            (TopluAlan.Program, new[] { "program", "sistem", "nerede" }),
            (TopluAlan.MenuYolu, new[] { "menu yolu", "menu", "yol" }),
            (TopluAlan.Alici, new[] { "alici", "alicilar", "kime" }),
            (TopluAlan.AliciEposta, new[] { "eposta", "e posta", "mail", "alici eposta", "alici e posta" }),
            (TopluAlan.Aciklama, new[] { "aciklama", "not" })
        };

        /// <summary>Başlıksız çok kolonlu yapıştırmada varsayılan sıra = boş şablonun sırası.</summary>
        public static readonly TopluAlan[] SablonSirasi =
        {
            TopluAlan.Baslik, TopluAlan.Tekrar, TopluAlan.Gun, TopluAlan.Program,
            TopluAlan.MenuYolu, TopluAlan.Alici, TopluAlan.Aciklama
        };

        private static readonly (IsTekrari Tekrar, string[] Yazimlar)[] TekrarYazimlari =
        {
            (IsTekrari.Aylik, new[] { "her ay", "aylik", "ayda bir", "her ay bir", "1 ayda bir" }),
            (IsTekrari.UcAylik, new[] { "uc ayda bir", "3 ayda bir", "3 aylik", "uc aylik", "ceyreklik", "her ceyrek" }),
            (IsTekrari.Yillik, new[] { "yilda bir", "yillik", "senede bir", "her yil", "senelik" }),
            (IsTekrari.TekSefer, new[] { "tek sefer", "bir kez", "tek", "bir defa", "tek seferlik" })
        };

        private static readonly Regex AliciDeseni = new(@"^(?<ad>.*?)\s*<\s*(?<eposta>[^<>]+?)\s*>\s*$", RegexOptions.Compiled);

        /// <param name="sistemler">Ortak sistem listesi (program adı buradan çözülür).</param>
        /// <param name="firmaninSistemleri">Aynı adlı iki kayıt varsa (Luca muhasebe/bordro) firmanınki seçilir.</param>
        /// <param name="mevcutIsler">Firmanın özel işleri: başlık → Id (üzerine yazma kararı için).</param>
        public static TopluOnizlemeDto Coz(TopluOnizleIstekDto istek, IReadOnlyList<Sistem> sistemler,
                                           IReadOnlyCollection<int> firmaninSistemleri,
                                           IReadOnlyDictionary<string, int> mevcutIsler)
        {
            var hucreler = istek.Hucreler is { Count: > 0 }
                ? istek.Hucreler.Select(s => s.Select(h => h ?? string.Empty).ToList()).ToList()
                : Bol(istek.Metin);

            var dto = new TopluOnizlemeDto { Hucreler = hucreler };
            if (hucreler.Count == 0) return dto;

            dto.KolonSayisi = hucreler.Max(s => s.Count);
            dto.IlkSatirBaslikOnerisi = BaslikSatiriMi(hucreler[0]);
            dto.IlkSatirBaslik = istek.IlkSatirBaslik ?? dto.IlkSatirBaslikOnerisi;

            var eslesme = Eslestir(istek.Eslesme, dto.IlkSatirBaslik ? hucreler[0] : null, dto.KolonSayisi);
            dto.Eslesme = Enum.GetValues<TopluAlan>()
                .Select(a => new TopluEslesmeDto
                {
                    Alan = a,
                    Kolon = eslesme.GetValueOrDefault(a),
                    DosyadakiBaslik = dto.IlkSatirBaslik && eslesme.GetValueOrDefault(a) is { } k && k < hucreler[0].Count
                        ? hucreler[0][k] : null
                })
                .ToList();

            var gorulen = new Dictionary<string, int>();
            for (var i = dto.IlkSatirBaslik ? 1 : 0; i < hucreler.Count; i++)
            {
                var satir = SatirCoz(i, hucreler[i], eslesme, sistemler, firmaninSistemleri);

                // Aynı yapıştırmada iki kez aynı başlık: ikincisi eklenmez.
                if (satir.Is is { } isi)
                {
                    var anahtar = BaslikAnahtari(isi.Baslik);
                    if (gorulen.TryGetValue(anahtar, out var ilk))
                    {
                        satir.Durum = TopluSatirDurumu.Okunamadi;
                        satir.Sorunlar.Add($"aynı başlık bu yapıştırmada {ilk + 1}. satırda da var");
                        satir.Is = null;
                    }
                    else
                    {
                        gorulen[anahtar] = i;
                        if (mevcutIsler.TryGetValue(anahtar, out var mevcutId)) satir.MevcutIsId = mevcutId;
                    }
                }

                dto.Satirlar.Add(satir);
            }

            return dto;
        }

        /// <summary>"Aynı başlık" anahtarı: kırpılmış, Türkçe büyük harf (Yapılacaklar gruplamasıyla aynı kural).</summary>
        public static string BaslikAnahtari(string? baslik) => (baslik ?? string.Empty).Trim().ToUpper(Tr);

        // ---- Bölme ----

        /// <summary>Satırlar \r\n / \n, hücreler \t. Tamamen boş satırlar atlanır; Excel'in tırnaklı hücresi açılır.</summary>
        public static List<List<string>> Bol(string? metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return new();

            return metin.Replace("\r\n", "\n").Replace('\r', '\n')
                .Split('\n')
                .Select(satir => satir.Split('\t').Select(Hucre).ToList())
                .Where(satir => satir.Any(h => h.Length > 0))
                .ToList();
        }

        private static string Hucre(string ham)
        {
            var h = ham.Trim();
            if (h.Length >= 2 && h[0] == '"' && h[^1] == '"') h = h[1..^1].Replace("\"\"", "\"").Trim();
            return h;
        }

        // ---- Başlık satırı ve eşleme ----

        private static TopluAlan? AlanAdi(string? hucre)
        {
            var k = Katla(hucre);
            if (k.Length == 0) return null;
            foreach (var (alan, adlar) in AlanAdlari)
                if (adlar.Contains(k)) return alan;
            return null;
        }

        /// <summary>Dolu hücrelerin en az yarısı (ve en az biri) bilinen bir alan adıysa ilk satır başlıktır.</summary>
        private static bool BaslikSatiriMi(IReadOnlyList<string> satir)
        {
            var dolu = satir.Where(h => !string.IsNullOrWhiteSpace(h)).ToList();
            var taninan = dolu.Count(h => AlanAdi(h) is not null);
            return taninan >= 1 && taninan * 2 >= dolu.Count;
        }

        private static Dictionary<TopluAlan, int?> Eslestir(List<TopluEslesmeDto>? istek, IReadOnlyList<string>? baslikSatiri,
                                                            int kolonSayisi)
        {
            var sonuc = new Dictionary<TopluAlan, int?>();

            // Kullanıcının seçimi: geçerli kolonlar, bir kolon tek alana.
            if (istek is { Count: > 0 })
            {
                var kullanilan = new HashSet<int>();
                foreach (var e in istek)
                {
                    if (!Enum.IsDefined(e.Alan)) continue;
                    sonuc[e.Alan] = e.Kolon is { } k && k >= 0 && k < kolonSayisi && kullanilan.Add(k) ? k : null;
                }
                return sonuc;
            }

            if (baslikSatiri is not null)
            {
                for (var k = 0; k < baslikSatiri.Count; k++)
                    if (AlanAdi(baslikSatiri[k]) is { } alan && !sonuc.ContainsKey(alan))
                        sonuc[alan] = k;
                return sonuc;
            }

            // Başlıksız: tek kolon = başlık; çok kolon = boş şablonun sırası.
            for (var k = 0; k < Math.Min(kolonSayisi, SablonSirasi.Length); k++)
                sonuc[SablonSirasi[k]] = k;
            return sonuc;
        }

        // ---- Satır ----

        private static TopluSatirDto SatirCoz(int sira, IReadOnlyList<string> hucreler, IReadOnlyDictionary<TopluAlan, int?> eslesme,
                                              IReadOnlyList<Sistem> sistemler, IReadOnlyCollection<int> firmaninSistemleri)
        {
            string Deger(TopluAlan alan)
                => eslesme.GetValueOrDefault(alan) is { } k && k < hucreler.Count ? hucreler[k].Trim() : string.Empty;

            var satir = new TopluSatirDto { Sira = sira };
            var kirmizi = new List<string>();
            var amber = new List<string>();

            // Başlık
            var baslik = string.Join(' ', Deger(TopluAlan.Baslik).Split(' ', StringSplitOptions.RemoveEmptyEntries));
            if (baslik.Length == 0) kirmizi.Add("başlık yok");
            else if (baslik.Length > 200) kirmizi.Add("başlık 200 karakterden uzun");

            // Tekrar
            var tekrarHam = Deger(TopluAlan.Tekrar);
            IsTekrari? tekrar = null;
            if (tekrarHam.Length > 0)
            {
                tekrar = TekrarCoz(tekrarHam);
                if (tekrar is null) kirmizi.Add($"tekrar anlaşılmadı: \"{tekrarHam}\"");
            }

            // Gün
            var gunHam = Deger(TopluAlan.Gun);
            var gun = gunHam.Length > 0 ? GunCoz(gunHam) : null;
            if (gunHam.Length > 0 && gun is null) kirmizi.Add($"gün anlaşılmadı: \"{gunHam}\"");

            // Tarih yalnız tek seferlik işte; tekrar boşsa tarih tek seferlik demektir.
            if (gun?.Tarih is not null)
            {
                if (tekrar is null) tekrar = IsTekrari.TekSefer;
                else if (tekrar != IsTekrari.TekSefer) kirmizi.Add("tarih yalnız tek seferlik işte yazılır");
            }

            // Yazılmamış tekrar/gün (hücre boş ya da kolon eşleşmemiş) AMBER (Prompt 11B): varsayılan
            // yine uygulanır ama satır işaretlenir — tek kolonlu yapıştırmada 17 firmaya sessizce
            // "her ay · dönem sonu" girilmesin. Amber engellemez, yalnız "kontrol et" der.
            if (tekrarHam.Length == 0)
                amber.Add(tekrar == IsTekrari.TekSefer
                    ? "tekrar yazılmamış — tarih var, tek seferlik varsayıldı (kontrol et)"
                    : "tekrar yazılmamış — her ay varsayıldı (kontrol et)");
            if (gunHam.Length == 0 && tekrar != IsTekrari.TekSefer)   // tek seferlikte tarih yoksa zaten kırmızı
                amber.Add("gün yazılmamış — dönem sonu varsayıldı (kontrol et)");

            var tekrarVarsayilan = tekrar is null && tekrarHam.Length == 0;
            if (tekrarVarsayilan) tekrar = IsTekrari.Aylik;

            var isi = new FirmaIsiKaydetDto { Baslik = baslik, Tekrar = tekrar ?? IsTekrari.Aylik };

            if (isi.Tekrar == IsTekrari.TekSefer)
            {
                if (gun?.Tarih is { } t) isi.TekSeferTarih = t;
                else if (gun is not null) kirmizi.Add("tek seferlik işte gün yerine tarih (gg.aa.yyyy) yazılmalı");
                else kirmizi.Add("tek seferlik işte tarih yok");
            }
            else if (gun is { Kural: { } kural })
            {
                isi.GunKurali = kural;
                isi.AyinGunu = gun.AyinGunu;
            }
            else if (gun is null)
            {
                isi.GunKurali = IsGunKurali.DonemSonu;
                isi.AyinGunu = null;
            }

            // Program: ortak listede ADA göre (normalize); yoksa boş + amber. Yeni kayıt AÇILMAZ.
            var programHam = Deger(TopluAlan.Program);
            if (programHam.Length > 0)
            {
                var anahtar = SistemAdi.Normalize(programHam);
                var adaylar = sistemler.Where(s => s.Aktif && SistemAdi.Normalize(s.Ad) == anahtar).ToList();
                var secilen = adaylar.FirstOrDefault(s => firmaninSistemleri.Contains(s.Id))
                              ?? adaylar.OrderBy(s => s.Tur).FirstOrDefault();
                if (secilen is null) amber.Add($"program listede yok: \"{programHam}\" — boş kalacak");
                else
                {
                    isi.SistemId = secilen.Id;
                    satir.ProgramMetni = secilen.Ad;
                }
            }

            // Menü yolu ve açıklama: serbest metin; sınırı aşan kısaltılır.
            var menu = Deger(TopluAlan.MenuYolu);
            if (menu.Length > 300) { menu = menu[..300]; amber.Add("menü yolu 300 karakterde kesildi"); }
            isi.MenuYolu = menu.Length == 0 ? null : menu;

            var aciklama = Deger(TopluAlan.Aciklama);
            if (aciklama.Length > 1000) { aciklama = aciklama[..1000]; amber.Add("açıklama 1000 karakterde kesildi"); }
            isi.Aciklama = aciklama.Length == 0 ? null : aciklama;

            // Alıcı: "Ad Soyad <eposta>" ya da ayrı e-posta kolonu; yalnız ad geçerli.
            var alicilar = AliciCoz(Deger(TopluAlan.Alici), Deger(TopluAlan.AliciEposta), amber);
            isi.Alicilar = alicilar.Count == 0 ? null : alicilar;

            satir.TekrarMetni = tekrar is null ? (tekrarHam.Length > 0 ? tekrarHam : null)
                : IsTakvimHesabi.TekrarAdi(isi.Tekrar) + (tekrarVarsayilan ? " (varsayılan — kontrol et)" : "");
            satir.GunMetni = gunHam.Length == 0
                ? (isi.Tekrar == IsTekrari.TekSefer ? null : "dönem sonu (varsayılan — kontrol et)")
                : isi.Tekrar == IsTekrari.TekSefer ? isi.TekSeferTarih?.ToString("dd.MM.yyyy") ?? gunHam
                : gun is null ? gunHam
                : isi.GunKurali switch
                {
                    IsGunKurali.AyinGunu => $"ayın {isi.AyinGunu}'i",
                    IsGunKurali.AySonu => "ay sonu",
                    _ => "dönem sonu"
                };
            satir.ProgramMetni ??= programHam.Length > 0 ? programHam : null;
            satir.AliciMetni = alicilar.Count == 0 ? null
                : string.Join("; ", alicilar.Select(a => a.Eposta is null ? a.AdSoyad : $"{a.AdSoyad} <{a.Eposta}>"));

            if (kirmizi.Count > 0)
            {
                satir.Durum = TopluSatirDurumu.Okunamadi;
                satir.Sorunlar.AddRange(kirmizi);
                satir.Sorunlar.AddRange(amber);
                return satir;
            }

            satir.Durum = amber.Count > 0 ? TopluSatirDurumu.Eksik : TopluSatirDurumu.Tamam;
            satir.Sorunlar.AddRange(amber);
            satir.Is = isi;
            return satir;
        }

        // ---- Değer çözümleme (Türkçe, esnek) ----

        /// <summary>Türkçe karakterleri katlar, küçültür, noktalamayı ve fazla boşluğu atar: "Üç Ayda Bir." → "uc ayda bir".</summary>
        public static string Katla(string? metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return string.Empty;

            var sb = new StringBuilder(metin.Length);
            foreach (var c in metin.Trim().ToLower(Tr).Normalize(NormalizationForm.FormC))
            {
                var d = c switch
                {
                    'ı' => 'i', 'ç' => 'c', 'ğ' => 'g', 'ö' => 'o', 'ş' => 's', 'ü' => 'u', 'â' => 'a', 'î' => 'i', 'û' => 'u',
                    '̇' => '\0',
                    '-' or '_' or '/' or '.' or ',' or '\'' or '’' => ' ',
                    _ => c
                };
                if (d != '\0') sb.Append(d);
            }
            return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        public static IsTekrari? TekrarCoz(string? ham)
        {
            var k = Katla(ham);
            foreach (var (tekrar, yazimlar) in TekrarYazimlari)
                if (yazimlar.Contains(k)) return tekrar;
            return null;
        }

        public sealed record GunSonucu(IsGunKurali? Kural, int? AyinGunu, DateTime? Tarih);

        private static readonly string[] TarihBicimleri = { "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" };

        /// <summary>1–31 → ayın günü; "ay sonu" → ay sonu; "dönem sonu" → dönem sonu; gg.aa.yyyy → tarih.</summary>
        public static GunSonucu? GunCoz(string? ham)
        {
            var temiz = (ham ?? string.Empty).Trim();
            if (temiz.Length == 0) return null;

            if (DateTime.TryParseExact(temiz, TarihBicimleri, CultureInfo.InvariantCulture, DateTimeStyles.None, out var tarih))
                return new GunSonucu(null, null, tarih.Date);

            // "25", "25." ve "25'i" aynı: katlama nokta ve kesme işaretini boşluğa çevirir.
            var k = Katla(temiz);
            var parca = k.Split(' ');
            if (int.TryParse(parca[0], NumberStyles.None, CultureInfo.InvariantCulture, out var gun)
                && (parca.Length == 1 || parca is [_, "i" or "si" or "u" or "su" or "inci" or "nci" or "nde" or "inde"]))
                return gun is >= 1 and <= 31 ? new GunSonucu(IsGunKurali.AyinGunu, gun, null) : null;

            return k switch
            {
                "ay sonu" or "ayin sonu" or "ay sonunda" or "ayin son gunu" or "ay son gunu" => new GunSonucu(IsGunKurali.AySonu, null, null),
                "donem sonu" or "donemin sonu" or "donem sonunda" => new GunSonucu(IsGunKurali.DonemSonu, null, null),
                _ => null
            };
        }

        /// <summary>"Ad Soyad &lt;eposta&gt;" (birden çoksa ";" ile); yalnız ad geçerli; geçersiz e-posta atılır (amber).</summary>
        public static List<FirmaIsiAlicisiDto> AliciCoz(string? aliciHam, string? epostaHam, List<string>? amber = null)
        {
            var sonuc = new List<FirmaIsiAlicisiDto>();
            if (string.IsNullOrWhiteSpace(aliciHam) && string.IsNullOrWhiteSpace(epostaHam)) return sonuc;

            foreach (var parca in (aliciHam ?? string.Empty).Split(';').Select(p => p.Trim()).Where(p => p.Length > 0))
            {
                string ad;
                string? eposta = null;

                var m = AliciDeseni.Match(parca);
                if (m.Success)
                {
                    ad = m.Groups["ad"].Value.Trim().Trim('"');
                    eposta = m.Groups["eposta"].Value.Trim();
                }
                else if (parca.Contains('@') && !parca.Contains(' '))
                {
                    ad = parca;
                    eposta = parca;
                }
                else ad = parca;

                if (ad.Length == 0) ad = eposta ?? string.Empty;
                if (ad.Length == 0) continue;

                sonuc.Add(new FirmaIsiAlicisiDto { AdSoyad = ad.Length > 150 ? ad[..150] : ad, Eposta = eposta, AliciTipi = AliciTipi.Kime });
            }

            // Ayrı e-posta kolonu: e-postası olmayan ilk alıcıya; alıcı adı yoksa e-postanın kendisi ad olur.
            var ayri = epostaHam?.Trim();
            if (!string.IsNullOrEmpty(ayri))
            {
                var hedef = sonuc.FirstOrDefault(a => a.Eposta is null);
                if (hedef is not null) hedef.Eposta = ayri;
                else if (sonuc.Count == 0) sonuc.Add(new FirmaIsiAlicisiDto { AdSoyad = ayri, Eposta = ayri, AliciTipi = AliciTipi.Kime });
            }

            foreach (var a in sonuc.Where(a => a.Eposta is not null))
            {
                if (a.Eposta!.Length <= 200 && YapilacaklarService.EpostaGibi(a.Eposta)) continue;
                amber?.Add($"{a.AdSoyad}: e-posta okunamadı (\"{a.Eposta}\") — boş kalacak");
                if (a.AdSoyad == a.Eposta) a.AdSoyad = a.Eposta.Split('@')[0];
                a.Eposta = null;
            }

            return sonuc;
        }
    }
}
