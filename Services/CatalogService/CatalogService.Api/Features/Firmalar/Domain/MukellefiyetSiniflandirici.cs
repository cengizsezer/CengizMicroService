using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace CatalogService.Api.Features.Firmalar.Domain
{
    /// <summary>
    /// Mükellefiyet <b>kodlarından</b> vergi türü ve defter usulü önerisi. Saf fonksiyon.
    ///
    /// Eşleştirme KODLA yapılır, adla DEĞİL: 0003'ün adı "GELİR VERGİSİ S. (MUHTASAR)" —
    /// ad üzerinden "gelir vergisi" arayan bir kural, hem 0003 hem 0010 taşıyan bir
    /// kurumu gelir vergisi mükellefi sanar. 0003 bu yüzden sınıflandırmada HİÇ
    /// kullanılmaz.
    ///
    /// Mükellefiyet türleri serbest metin olarak saklanıyor. Kod ayıklama <see cref="Ayikla"/>'da:
    /// metin virgül / noktalı virgül / satır sonundan parçalara bölünür; bir parça
    /// <c>0\d{3}</c> ile <b>başlıyorsa</b> kod sayılır. Ardından tire, boşluk, nokta, "·"
    /// gelebilir ya da hiçbir şey gelmeyebilir ("0010 - KURUMLAR", "0010-KURUMLAR",
    /// "0010 KURUMLAR", "0010"). Parçalama + parça başı şartı birlikte metindeki yılları
    /// ("2021 yılından itibaren") ve cümle içindeki numaraları ("... 0010 numaralı yazı")
    /// zaten eliyor; tire şartı (6B) fazladandı ve tiresiz yazılmış firmaları dışarıda
    /// bırakıyordu, 6C'de kaldırıldı. Kalan parçalar <see cref="AyiklamaSonucu.Kalan"/>'da
    /// döner — sessizce yutulmaz, ekranda ve logda görünür.
    ///
    /// Bilinmeyen kod reddedilmez: listede taşınır ve gösterilir, yalnız sınıflandırmada
    /// kullanılmaz (sınıflandırma yalnız 0010 / 0001'e bakar).
    /// </summary>
    public static class MukellefiyetSiniflandirici
    {
        public const string KurumlarVergisiKodu = "0010";
        public const string GelirVergisiKodu = "0001";

        /// <summary>Parça başında kod; ardından (varsa) ayıraçlar ve ad. Kodun hemen ardından rakam gelemez.</summary>
        private static readonly Regex ParcaDeseni = new(
            @"^(?<kod>0\d{3})(?!\d)[\s\-–—·.:]*(?<ad>.*?)\s*$", RegexOptions.Compiled);

        private static readonly char[] Ayiraclar = { ',', ';', '\n', '\r' };

        public sealed record Oneri(VergiTuru VergiTuru, DefterUsulu DefterUsulu, string? KaynakKod);

        /// <summary>Ayıklanan tek kod ve metinde yanında yazan ad (yoksa <c>null</c>).</summary>
        public sealed record MukellefiyetKodu(string Kod, string? Ad);

        /// <summary>Ayıklanan kodlar (geliş sırasıyla, tekil) ve kod sayılamayan kalan parçalar.</summary>
        public sealed record AyiklamaSonucu(IReadOnlyList<MukellefiyetKodu> Kodlar, string? Kalan);

        public static AyiklamaSonucu Ayikla(string? mukellefiyetTurleri)
        {
            if (string.IsNullOrWhiteSpace(mukellefiyetTurleri))
                return new AyiklamaSonucu(Array.Empty<MukellefiyetKodu>(), null);

            var kodlar = new List<MukellefiyetKodu>();
            var kalan = new List<string>();

            foreach (var ham in mukellefiyetTurleri.Split(Ayiraclar, StringSplitOptions.RemoveEmptyEntries))
            {
                var parca = ham.Trim();
                if (parca.Length == 0) continue;

                var m = ParcaDeseni.Match(parca);
                if (!m.Success)
                {
                    kalan.Add(parca);
                    continue;
                }

                var kod = m.Groups["kod"].Value;
                var ad = m.Groups["ad"].Success && m.Groups["ad"].Value.Trim().Length > 0
                    ? m.Groups["ad"].Value.Trim()
                    : null;

                if (kodlar.All(k => k.Kod != kod)) kodlar.Add(new MukellefiyetKodu(kod, ad));
            }

            return new AyiklamaSonucu(kodlar, kalan.Count == 0 ? null : string.Join(", ", kalan));
        }

        /// <summary>
        /// Ayıklama sonucunu loglar: kodlar bilgi, kod sayılamayan kalan metin uyarı
        /// seviyesinde (yasal işler bu listeden türeyecek; kaçan kod fark edilsin).
        /// </summary>
        public static AyiklamaSonucu AyiklaVeLogla(ILogger? log, int firmaId, string? mukellefiyetTurleri)
        {
            var sonuc = Ayikla(mukellefiyetTurleri);
            if (log is null || string.IsNullOrWhiteSpace(mukellefiyetTurleri)) return sonuc;

            var kodlar = string.Join(", ", sonuc.Kodlar.Select(k => k.Kod));
            if (sonuc.Kalan is null)
                log.LogInformation("Mükellefiyet ayıklandı (firma {FirmaId}): [{Kodlar}]", firmaId, kodlar);
            else
                log.LogWarning("Mükellefiyet ayıklandı (firma {FirmaId}): [{Kodlar}]; ayıklanamayan metin: \"{Kalan}\"",
                               firmaId, kodlar, sonuc.Kalan);

            return sonuc;
        }

        /// <summary>Ayıklanan mükellefiyet kodları, geliş sırasıyla ve tekil.</summary>
        public static List<string> Kodlar(string? mukellefiyetTurleri)
            => Ayikla(mukellefiyetTurleri).Kodlar.Select(k => k.Kod).ToList();

        /// <summary>Kesin bildiğimiz kodların kısa adları; gerisinde metindeki ad kısaltılır.</summary>
        private static readonly Dictionary<string, string> BilinenKisaAdlar = new()
        {
            ["0001"] = "Gelir vergisi",
            ["0003"] = "Muhtasar",
            ["0010"] = "Kurumlar vergisi",
            ["0015"] = "KDV",
            ["0040"] = "Damga vergisi"
        };

        private static readonly System.Globalization.CultureInfo Tr = new("tr-TR");

        /// <summary>Çip için kısa ad: "0003" → "Muhtasar". Bilinmeyen kodda metindeki ad, kısaltılmış.</summary>
        public static string? KisaAd(MukellefiyetKodu kod)
        {
            if (BilinenKisaAdlar.TryGetValue(kod.Kod, out var bilinen)) return bilinen;
            if (string.IsNullOrWhiteSpace(kod.Ad)) return null;

            var ad = kod.Ad;
            var parantez = ad.IndexOf('(');
            if (parantez > 0) ad = ad[..parantez];
            ad = ad.Trim();
            if (ad.Length == 0) return null;

            ad = ad.ToLower(Tr);
            ad = char.ToUpper(ad[0], Tr) + ad[1..];
            return ad.Length > 28 ? ad[..27].TrimEnd() + "…" : ad;
        }

        /// <summary>
        /// 0010 → kurumlar + bilanço esası. 0010 yok, 0001 var → gelir vergisi, defter
        /// usulü belirsiz (şahıs hem bilanço hem işletme olabilir, kullanıcı seçer).
        /// İkisi de yoksa ikisi de belirsiz.
        /// </summary>
        public static Oneri Oner(string? mukellefiyetTurleri)
        {
            var kodlar = Kodlar(mukellefiyetTurleri);

            if (kodlar.Contains(KurumlarVergisiKodu))
                return new Oneri(VergiTuru.KurumlarVergisi, DefterUsulu.BilancoEsasi, KurumlarVergisiKodu);

            if (kodlar.Contains(GelirVergisiKodu))
                return new Oneri(VergiTuru.GelirVergisi, DefterUsulu.Belirsiz, GelirVergisiKodu);

            return new Oneri(VergiTuru.Belirsiz, DefterUsulu.Belirsiz, null);
        }

        /// <summary>
        /// Öneriyi firmaya uygular — <b>yalnız Belirsiz olan alanı doldurur</b>.
        ///
        /// Dolu bir alan (otomatik dolmuş olsa bile) hiçbir koşulda Belirsiz'e çevrilmez ya
        /// da değiştirilmez; elle seçilmiş alana (kullanıcının kendisinin Belirsiz seçtiği
        /// dahil) hiç dokunulmaz. Eskiden (6/6B) otomatik değer, öneri belirsizleşince geri
        /// alınıyordu: bir ayrıştırıcı değişikliği açılış seed'inde bütün firmaların
        /// sınıflandırmasını SESSİZCE silebiliyordu. Öneri dolu değerle çelişirse değer korunur
        /// ve uyarı loglanır.
        ///
        /// Bir şey değiştiyse <c>true</c>.
        /// </summary>
        public static bool Uygula(Firma firma, string? mukellefiyetTurleri, ILogger? log = null)
        {
            var oneri = Oner(mukellefiyetTurleri);
            var degisti = false;

            if (firma.VergiTuruKaynagi != SiniflandirmaKaynagi.Elle && oneri.VergiTuru != VergiTuru.Belirsiz)
            {
                if (firma.VergiTuru == VergiTuru.Belirsiz)
                {
                    // Kısıt: kullanıcı işletme hesabı seçmişse kurumlar vergisi doldurulmaz.
                    if (KisitIhlali(oneri.VergiTuru, firma.DefterUsulu))
                    {
                        log?.LogWarning("Firma {FirmaId}: öneri kurumlar vergisi ama defter usulü işletme hesabı; vergi türü doldurulmadı.",
                                        firma.Id);
                    }
                    else
                    {
                        firma.VergiTuru = oneri.VergiTuru;
                        firma.VergiTuruKaynagi = SiniflandirmaKaynagi.Otomatik;
                        degisti = true;
                    }
                }
                else if (firma.VergiTuru != oneri.VergiTuru)
                {
                    log?.LogWarning("Firma {FirmaId}: mükellefiyet {Kod} → {Oneri} öneriyor, kayıtlı vergi türü {Mevcut} korundu.",
                                    firma.Id, oneri.KaynakKod, oneri.VergiTuru, firma.VergiTuru);
                }
            }

            if (firma.DefterUsuluKaynagi != SiniflandirmaKaynagi.Elle && oneri.DefterUsulu != DefterUsulu.Belirsiz)
            {
                // Öneri hiçbir zaman işletme hesabı değil; kurumlar/işletme kısıtı burada doğmaz.
                if (firma.DefterUsulu == DefterUsulu.Belirsiz)
                {
                    firma.DefterUsulu = oneri.DefterUsulu;
                    firma.DefterUsuluKaynagi = SiniflandirmaKaynagi.Otomatik;
                    degisti = true;
                }
                else if (firma.DefterUsulu != oneri.DefterUsulu)
                {
                    log?.LogWarning("Firma {FirmaId}: mükellefiyet {Kod} → {Oneri} öneriyor, kayıtlı defter usulü {Mevcut} korundu.",
                                    firma.Id, oneri.KaynakKod, oneri.DefterUsulu, firma.DefterUsulu);
                }
            }

            return degisti;
        }

        /// <summary>
        /// Kayıtlı sınıflandırma mükellefiyet kodlarıyla uyuşmuyor mu; uyuşmuyorsa kullanıcıya
        /// yazılacak cümle, uyuşuyorsa <c>null</c>.
        ///
        /// Yalnız kaynağı <b>otomatik</b> olan dolu alana bakılır. Elle seçilmiş ya da elle
        /// onaylanmış değer kullanıcı kararıdır, uyarı vermez. Kayıtlı değer hiçbir durumda
        /// değiştirilmez (<see cref="Uygula"/>); bu fonksiyon yalnız çelişkiyi görünür kılar —
        /// log tek başına yetmez, çünkü kimse okumaz.
        ///
        /// Uyarı kendiliğinden düşer: kullanıcı değeri düzeltince (kaynak Elle olur), elle
        /// onaylayınca (kaynak Elle olur) ya da mükellefiyet metnini düzeltince (öneri tutar).
        /// </summary>
        public static string? Uyusmazlik(Firma firma, string? mukellefiyetTurleri)
        {
            var oneri = Oner(mukellefiyetTurleri);
            var kodlar = Kodlar(mukellefiyetTurleri);
            var parcalar = new List<string>();

            if (firma.VergiTuruKaynagi == SiniflandirmaKaynagi.Otomatik
                && firma.VergiTuru != VergiTuru.Belirsiz
                && firma.VergiTuru != oneri.VergiTuru)
            {
                var gerekenKod = firma.VergiTuru == VergiTuru.KurumlarVergisi ? KurumlarVergisiKodu : GelirVergisiKodu;
                parcalar.Add(kodlar.Contains(gerekenKod)
                    ? $"kodlar {Ad(oneri.VergiTuru)} gösteriyor ({oneri.KaynakKod}), kayıtlı değer {Ad(firma.VergiTuru)}"
                    : $"kodlarda {gerekenKod} yok, kayıtlı değer {Ad(firma.VergiTuru)}");
            }

            if (firma.DefterUsuluKaynagi == SiniflandirmaKaynagi.Otomatik
                && firma.DefterUsulu != DefterUsulu.Belirsiz
                && firma.DefterUsulu != oneri.DefterUsulu)
            {
                // Otomatik defter usulü yalnız 0010'dan (bilanço esası) gelir.
                parcalar.Add(oneri.DefterUsulu == DefterUsulu.Belirsiz
                    ? $"kodlarda {KurumlarVergisiKodu} yok, kayıtlı defter usulü {Ad(firma.DefterUsulu)}"
                    : $"kodlar {Ad(oneri.DefterUsulu)} gösteriyor, kayıtlı defter usulü {Ad(firma.DefterUsulu)}");
            }

            return parcalar.Count == 0
                ? null
                : "Mükellefiyet kodları sınıflandırmayla uyuşmuyor — " + string.Join("; ", parcalar);
        }

        private static string Ad(VergiTuru v) => v switch
        {
            VergiTuru.KurumlarVergisi => "Kurumlar vergisi",
            VergiTuru.GelirVergisi => "Gelir vergisi",
            _ => "belirsiz"
        };

        private static string Ad(DefterUsulu d) => d switch
        {
            DefterUsulu.BilancoEsasi => "Bilanço esası",
            DefterUsulu.IsletmeHesabi => "İşletme hesabı",
            _ => "belirsiz"
        };

        /// <summary>Kurumlar vergisi mükellefi işletme hesabı esasına göre defter tutamaz.</summary>
        public static bool KisitIhlali(VergiTuru vergiTuru, DefterUsulu defterUsulu)
            => vergiTuru == VergiTuru.KurumlarVergisi && defterUsulu == DefterUsulu.IsletmeHesabi;

        public const string KisitMesaji = "Kurumlar vergisi mükellefi işletme hesabı esasına göre defter tutamaz.";
    }
}
