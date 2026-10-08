using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WebApp.Pages.Hesaplamalar.OrtuluSermaye.Model;

namespace WebApp.Pages.Hesaplamalar.OrtuluSermaye.Services
{
    /// <summary>Yapıştırılan muavinin ayrıştırma sonucu.</summary>
    public sealed class MuavinAyristirmaSonucu
    {
        /// <summary>Hata varsa BOŞ: yarım yüklenmiş muavin sessizce yanlış sonuç üretmesin.</summary>
        public IReadOnlyList<MuavinSatiri> Satirlar { get; init; } = Array.Empty<MuavinSatiri>();

        /// <summary>Satır numaralı hata mesajları (yapıştırılan metnin satırı, 1'den).</summary>
        public IReadOnlyList<string> Hatalar { get; init; } = Array.Empty<string>();

        /// <summary>Hesap kodu boş olduğu için atlanan satırlar (ORKA ara toplam / "Toplam" satırları).</summary>
        public int AtlananSatir { get; init; }

        public bool BaslikVardi { get; init; }

        public bool Basarili => Hatalar.Count == 0;
    }

    /// <summary>
    /// ORKA muavininin Excel'den kopyalanan (TSV) hâlini okur.
    /// <list type="bullet">
    /// <item>İlk dolu satır başlıksa sütunlar ADA göre eşlenir (sıra serbest); değilse
    /// 9 sütun maketteki sırayla okunur: Acc. No · Acc Name · Date · Slip Type · Slip No ·
    /// Description · Debit · Credit · Balance.</item>
    /// <item>Sütun sayısı tutmayan satır, okunamayan sayı/tarih satır numarasıyla hata döner;
    /// hiçbir hücre sessizce 0 sayılmaz (boş tutar hücresi hariç: o gerçekten 0'dır).</item>
    /// <item>Hesap kodu boş satırlar hata sayılmadan atlanır ve sayılır.</item>
    /// </list>
    /// </summary>
    public static class MuavinTsvParser
    {
        private enum Sutun { HesapKodu, HesapAdi, Tarih, FisTuru, FisNo, Aciklama, Borc, Alacak, Bakiye }

        /// <summary>Başlıksız yapıştırmada sütun sırası (maketteki A–I).</summary>
        private static readonly Sutun[] VarsayilanSira =
        {
            Sutun.HesapKodu, Sutun.HesapAdi, Sutun.Tarih, Sutun.FisTuru, Sutun.FisNo,
            Sutun.Aciklama, Sutun.Borc, Sutun.Alacak, Sutun.Bakiye
        };

        private static readonly Sutun[] ZorunluSutunlar = { Sutun.HesapKodu, Sutun.Tarih, Sutun.Borc, Sutun.Alacak };

        /// <summary>Normalize edilmiş başlık adı → sütun. İngilizce (ORKA) ve Türkçe adlar.</summary>
        private static readonly Dictionary<string, Sutun> BaslikAdlari = new()
        {
            ["accno"] = Sutun.HesapKodu, ["accountno"] = Sutun.HesapKodu, ["accountcode"] = Sutun.HesapKodu,
            ["hesapkodu"] = Sutun.HesapKodu, ["hesapno"] = Sutun.HesapKodu,
            ["accname"] = Sutun.HesapAdi, ["accountname"] = Sutun.HesapAdi, ["hesapadi"] = Sutun.HesapAdi,
            ["date"] = Sutun.Tarih, ["tarih"] = Sutun.Tarih,
            ["sliptype"] = Sutun.FisTuru, ["fisturu"] = Sutun.FisTuru, ["fistipi"] = Sutun.FisTuru,
            ["slipno"] = Sutun.FisNo, ["fisno"] = Sutun.FisNo,
            ["description"] = Sutun.Aciklama, ["aciklama"] = Sutun.Aciklama,
            ["debit"] = Sutun.Borc, ["borc"] = Sutun.Borc,
            ["credit"] = Sutun.Alacak, ["alacak"] = Sutun.Alacak,
            ["balance"] = Sutun.Bakiye, ["bakiye"] = Sutun.Bakiye
        };

        private static readonly string[] TarihBicimleri = { "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy" };

        private const int AzamiHata = 20;

        public static MuavinAyristirmaSonucu Ayristir(string? metin)
        {
            var hatalar = new List<string>();
            var satirlar = new List<MuavinSatiri>();
            var atlanan = 0;

            var tumSatirlar = (metin ?? "").Split('\n');
            var ilkDolu = Array.FindIndex(tumSatirlar, s => !string.IsNullOrWhiteSpace(s));
            if (ilkDolu < 0)
                return new MuavinAyristirmaSonucu { Hatalar = new[] { "Yapıştırılan metin boş." } };

            // Sütun eşlemesi: başlık varsa adına göre, yoksa varsayılan sıra.
            var ilkHucreler = Hucreler(tumSatirlar[ilkDolu]);
            var baslikVar = BaslikMi(ilkHucreler);
            Dictionary<Sutun, int> eslem;
            int beklenenSutun;
            int veriBaslangic;

            if (baslikVar)
            {
                eslem = new Dictionary<Sutun, int>();
                for (var i = 0; i < ilkHucreler.Length; i++)
                {
                    if (!BaslikAdlari.TryGetValue(Normalize(ilkHucreler[i]), out var sutun)) continue;
                    if (!eslem.TryAdd(sutun, i))
                        hatalar.Add($"Satır {ilkDolu + 1}: \"{ilkHucreler[i].Trim()}\" sütunu başlıkta iki kez var.");
                }
                foreach (var z in ZorunluSutunlar.Where(z => !eslem.ContainsKey(z)))
                    hatalar.Add($"Satır {ilkDolu + 1}: başlıkta {SutunAdi(z)} sütunu bulunamadı.");
                if (hatalar.Count > 0)
                    return new MuavinAyristirmaSonucu { Hatalar = hatalar, BaslikVardi = true };

                beklenenSutun = ilkHucreler.Length;
                veriBaslangic = ilkDolu + 1;
            }
            else
            {
                eslem = VarsayilanSira.Select((s, i) => (s, i)).ToDictionary(x => x.s, x => x.i);
                beklenenSutun = VarsayilanSira.Length;
                veriBaslangic = ilkDolu;
            }

            for (var i = veriBaslangic; i < tumSatirlar.Length; i++)
            {
                var ham = tumSatirlar[i];
                if (string.IsNullOrWhiteSpace(ham)) continue;
                var no = i + 1;

                var h = Hucreler(ham);
                if (h.Length != beklenenSutun)
                {
                    hatalar.Add($"Satır {no}: {h.Length} sütun var, {beklenenSutun} bekleniyor"
                                + (baslikVar ? " (başlık satırındaki kadar)." : " (Acc. No … Balance)."));
                    continue;
                }

                string Al(Sutun s) => eslem.TryGetValue(s, out var idx) ? h[idx].Trim() : "";

                var hesapKodu = Al(Sutun.HesapKodu);
                if (hesapKodu.Length == 0)
                {
                    atlanan++;
                    continue;
                }

                var satir = new MuavinSatiri
                {
                    HesapKodu = hesapKodu,
                    HesapAdi = Al(Sutun.HesapAdi),
                    FisTuru = Al(Sutun.FisTuru),
                    FisNo = Al(Sutun.FisNo),
                    Aciklama = Al(Sutun.Aciklama)
                };

                if (TarihOku(Al(Sutun.Tarih), out var tarih)) satir.Tarih = tarih;
                else hatalar.Add($"Satır {no}: tarih okunamadı (\"{Al(Sutun.Tarih)}\"). Beklenen: gg.aa.yyyy veya gg/aa/yyyy.");

                satir.Borc = TutarAl(Sutun.Borc);
                satir.Alacak = TutarAl(Sutun.Alacak);
                satir.Bakiye = TutarAl(Sutun.Bakiye);

                satir.Sinif = MuavinSiniflandirici.Sinifla(satir.HesapKodu, satir.Aciklama);
                satirlar.Add(satir);

                decimal TutarAl(Sutun s)
                {
                    var deger = Al(s);
                    if (SayiOku(deger, out var tutar)) return tutar;
                    hatalar.Add($"Satır {no}: {SutunAdi(s)} okunamadı (\"{deger}\").");
                    return 0;
                }
            }

            if (hatalar.Count == 0 && satirlar.Count == 0)
                hatalar.Add("Hesap kodu dolu satır bulunamadı.");

            if (hatalar.Count > AzamiHata)
            {
                var fazla = hatalar.Count - AzamiHata;
                hatalar = hatalar.Take(AzamiHata).Append($"… ve {fazla} hata daha.").ToList();
            }

            return new MuavinAyristirmaSonucu
            {
                Satirlar = hatalar.Count == 0 ? satirlar : Array.Empty<MuavinSatiri>(),
                Hatalar = hatalar,
                AtlananSatir = atlanan,
                BaslikVardi = baslikVar
            };
        }

        /// <summary>
        /// Tutar hücresini okur; iki biçim de geçer ("1.234,56" ve "1,234.56").
        /// <list type="bullet">
        /// <item>Boş hücre 0'dır (muavinde boş borç/alacak normaldir).</item>
        /// <item>Nokta ve virgül birlikteyse sağdaki ondalıktır; soldaki yalnız 3'lü binlik
        /// grupları ayırabilir.</item>
        /// <item>Tek tür ayırıcı birden çok kez geçiyorsa binliktir (her grup 3 hane).</item>
        /// <item>Tek ayırıcı bir kez geçiyor ve ardından tam 3 hane geliyorsa binliktir; aksi
        /// hâlde ondalıktır. İstisna: tam kısım "0" ise ("0,125") ondalık sayılır — binlik
        /// okuması baştaki sıfırla anlamsız olur. Tam kısmı 3 haneden uzun ("1234,567")
        /// binlik grubu olamaz ve okunamaz.</item>
        /// <item>Eksi işareti başta ya da parantez ("(1.234,56)") kabul edilir.</item>
        /// </list>
        /// Okunamayan hücrede <c>false</c> döner; çağıran 0 sayMAZ, hata yazar.
        /// </summary>
        public static bool SayiOku(string? metin, out decimal deger)
        {
            deger = 0;
            var s = new string((metin ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray());
            if (s.Length == 0) return true;

            var eksi = false;
            if (s.StartsWith('(') && s.EndsWith(')')) { eksi = true; s = s[1..^1]; }
            else if (s.StartsWith('-')) { eksi = true; s = s[1..]; }

            if (s.Length == 0 || !s.All(c => char.IsAsciiDigit(c) || c == '.' || c == ',')) return false;

            var nokta = s.Count(c => c == '.');
            var virgul = s.Count(c => c == ',');
            string? normal;

            if (nokta > 0 && virgul > 0)
            {
                var ondalik = s.LastIndexOf('.') > s.LastIndexOf(',') ? '.' : ',';
                var binlik = ondalik == '.' ? ',' : '.';
                var desen = $@"^\d{{1,3}}(\{binlik}\d{{3}})+\{ondalik}\d+$";
                normal = Regex.IsMatch(s, desen) ? s.Replace(binlik.ToString(), "").Replace(ondalik, '.') : null;
            }
            else if (nokta + virgul > 1)
            {
                var ayirici = nokta > 0 ? '.' : ',';
                normal = Regex.IsMatch(s, $@"^\d{{1,3}}(\{ayirici}\d{{3}})+$") ? s.Replace(ayirici.ToString(), "") : null;
            }
            else if (nokta + virgul == 1)
            {
                var ayirici = nokta > 0 ? '.' : ',';
                var parca = s.Split(ayirici);
                var tam = parca[0];
                var kesir = parca[1];
                if (tam.Length == 0 || kesir.Length == 0) normal = null;
                else if (kesir.Length == 3 && tam != "0")
                    normal = Regex.IsMatch(tam, @"^[1-9]\d{0,2}$") ? tam + kesir : null;
                else normal = tam + "." + kesir;
            }
            else
            {
                normal = s;
            }

            if (normal is null || !decimal.TryParse(normal, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d))
                return false;

            deger = eksi ? -d : d;
            return true;
        }

        /// <summary>gg.aa.yyyy veya gg/aa/yyyy; Excel'in eklediği saat kısmı ("01.01.2026 00:00:00") yok sayılır.</summary>
        public static bool TarihOku(string? metin, out DateTime tarih)
        {
            var s = (metin ?? "").Trim();
            var bosluk = s.IndexOf(' ');
            if (bosluk > 0) s = s[..bosluk];
            return DateTime.TryParseExact(s, TarihBicimleri, CultureInfo.InvariantCulture, DateTimeStyles.None, out tarih);
        }

        private static string[] Hucreler(string satir) => satir.TrimEnd('\r').Split('\t');

        private static bool BaslikMi(string[] hucreler)
            => hucreler.Count(h => BaslikAdlari.ContainsKey(Normalize(h))) >= 3;

        /// <summary>Başlık karşılaştırması için: küçük harf, Türkçe harfler sadeleşir, yalnız harf/rakam kalır.</summary>
        private static string Normalize(string metin)
        {
            var sb = new StringBuilder();
            foreach (var c in metin.Trim().ToLower(new CultureInfo("tr-TR")))
            {
                var d = c switch { 'ı' => 'i', 'ş' => 's', 'ğ' => 'g', 'ü' => 'u', 'ö' => 'o', 'ç' => 'c', 'â' => 'a', _ => c };
                if (char.IsLetterOrDigit(d)) sb.Append(d);
            }
            return sb.ToString();
        }

        private static string SutunAdi(Sutun s) => s switch
        {
            Sutun.HesapKodu => "Acc. No (hesap kodu)",
            Sutun.HesapAdi => "Acc Name",
            Sutun.Tarih => "Date (tarih)",
            Sutun.FisTuru => "Slip Type",
            Sutun.FisNo => "Slip No",
            Sutun.Aciklama => "Description",
            Sutun.Borc => "Debit (borç)",
            Sutun.Alacak => "Credit (alacak)",
            Sutun.Bakiye => "Balance (bakiye)",
            _ => s.ToString()
        };
    }
}
