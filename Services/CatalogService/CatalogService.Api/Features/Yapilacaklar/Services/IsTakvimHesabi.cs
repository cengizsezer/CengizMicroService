using System.Globalization;
using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;

namespace CatalogService.Api.Features.Yapilacaklar.Services
{
    /// <summary>Bir tekrar dönemi: yıl + dönem no (aylıkta ay, üç aylıkta çeyrek, yıllıkta 1).</summary>
    public readonly record struct IsDonemi(IsTekrari Tekrar, int Yil, int No)
    {
        public const string TekSeferAnahtari = "tek";

        public DateTime Bas => Tekrar switch
        {
            IsTekrari.Aylik => new DateTime(Yil, No, 1),
            IsTekrari.UcAylik => new DateTime(Yil, (No - 1) * 3 + 1, 1),
            IsTekrari.Yillik => new DateTime(Yil, 1, 1),
            _ => throw new InvalidOperationException("Tek seferlik işin dönemi yok.")
        };

        public DateTime Bit => Tekrar switch
        {
            IsTekrari.Aylik => Bas.AddMonths(1).AddDays(-1),
            IsTekrari.UcAylik => Bas.AddMonths(3).AddDays(-1),
            IsTekrari.Yillik => new DateTime(Yil, 12, 31),
            _ => throw new InvalidOperationException("Tek seferlik işin dönemi yok.")
        };

        /// <summary>"2026-09", "2026-Q3", "2026".</summary>
        public string Anahtar => Tekrar switch
        {
            IsTekrari.Aylik => $"{Yil:0000}-{No:00}",
            IsTekrari.UcAylik => $"{Yil:0000}-Q{No}",
            IsTekrari.Yillik => $"{Yil:0000}",
            _ => TekSeferAnahtari
        };

        public string Etiket => Tekrar switch
        {
            IsTekrari.Aylik => IsTakvimHesabi.Tr.DateTimeFormat.GetMonthName(No) + " " + Yil,
            IsTekrari.UcAylik => $"{Yil} / {No}. çeyrek",
            IsTekrari.Yillik => Yil.ToString(CultureInfo.InvariantCulture),
            _ => string.Empty
        };

        public IsDonemi Sonraki() => Tekrar switch
        {
            IsTekrari.Aylik => No == 12 ? new(Tekrar, Yil + 1, 1) : new(Tekrar, Yil, No + 1),
            IsTekrari.UcAylik => No == 4 ? new(Tekrar, Yil + 1, 1) : new(Tekrar, Yil, No + 1),
            _ => new(Tekrar, Yil + 1, 1)
        };

        public IsDonemi Onceki() => Tekrar switch
        {
            IsTekrari.Aylik => No == 1 ? new(Tekrar, Yil - 1, 12) : new(Tekrar, Yil, No - 1),
            IsTekrari.UcAylik => No == 1 ? new(Tekrar, Yil - 1, 4) : new(Tekrar, Yil, No - 1),
            _ => new(Tekrar, Yil - 1, 1)
        };

        /// <summary>
        /// Bu ayda KAPANAN dönemler (Dönem panosu): her ay aylık dönem; mart, haziran, eylül ve
        /// aralıkta çeyrek; aralıkta yıl. Dönem ve sonu <see cref="Icin"/>/<see cref="Bit"/>'ten
        /// okunur — ayrı bir takvim hesabı yok.
        /// </summary>
        public static IEnumerable<IsDonemi> AydaKapananlar(int yil, int ay)
        {
            var ayBasi = new DateTime(yil, ay, 1);
            foreach (var tekrar in new[] { IsTekrari.Aylik, IsTekrari.UcAylik, IsTekrari.Yillik })
            {
                var donem = Icin(tekrar, ayBasi);
                if (donem.Bit.Year == yil && donem.Bit.Month == ay) yield return donem;
            }
        }

        /// <summary>Tarihin düştüğü dönem.</summary>
        public static IsDonemi Icin(IsTekrari tekrar, DateTime tarih) => tekrar switch
        {
            IsTekrari.Aylik => new(tekrar, tarih.Year, tarih.Month),
            IsTekrari.UcAylik => new(tekrar, tarih.Year, (tarih.Month - 1) / 3 + 1),
            IsTekrari.Yillik => new(tekrar, tarih.Year, 1),
            _ => throw new InvalidOperationException("Tek seferlik işin dönemi yok.")
        };

        /// <summary>Anahtarı dönem olarak çözer; biçim tekrara uymuyorsa <c>null</c>.</summary>
        public static IsDonemi? Coz(IsTekrari tekrar, string? anahtar)
        {
            if (string.IsNullOrWhiteSpace(anahtar)) return null;

            var parca = anahtar.Trim().Split('-');
            if (!int.TryParse(parca[0], NumberStyles.None, CultureInfo.InvariantCulture, out var yil)
                || yil is < 2000 or > 2100)
                return null;

            switch (tekrar)
            {
                case IsTekrari.Yillik when parca.Length == 1:
                    return new IsDonemi(tekrar, yil, 1);
                case IsTekrari.Aylik when parca.Length == 2
                                          && int.TryParse(parca[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ay)
                                          && ay is >= 1 and <= 12:
                    return new IsDonemi(tekrar, yil, ay);
                case IsTekrari.UcAylik when parca.Length == 2 && parca[1].Length == 2 && parca[1][0] == 'Q'
                                            && parca[1][1] is >= '1' and <= '4':
                    return new IsDonemi(tekrar, yil, parca[1][1] - '0');
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// Firma işlerinin tarih kuralları. Saf fonksiyonlar; "bugün" her zaman parametre —
    /// gerçek saate bağlı test yazılmasın diye burada <c>DateTime.Now</c> yok.
    /// </summary>
    public static class IsTakvimHesabi
    {
        internal static readonly CultureInfo Tr = new("tr-TR");

        /// <summary>Bu gün sayısına kadar (dahil) kalan iş "Bu hafta".</summary>
        public const int BuHaftaGun = 7;

        /// <summary>
        /// Ayın <paramref name="gun"/>'ncü günü; ay o kadar gün çekmiyorsa son günü
        /// (31 → nisanda 30, şubatta 28/29).
        /// </summary>
        public static DateTime AyinGunu(int yil, int ay, int gun)
            => new(yil, ay, Math.Clamp(gun, 1, DateTime.DaysInMonth(yil, ay)));

        /// <summary>Özel işin bir dönemdeki son günü. "Ay" dönemin ilk ayıdır.</summary>
        public static DateTime SonGun(IsGunKurali kural, int? ayinGunu, IsDonemi donem)
        {
            var bas = donem.Bas;
            return kural switch
            {
                IsGunKurali.AyinGunu => AyinGunu(bas.Year, bas.Month, ayinGunu ?? 1),
                IsGunKurali.AySonu => AyinGunu(bas.Year, bas.Month, 31),
                _ => donem.Bit
            };
        }

        public static (IsDurumu Durum, int Gun) Durum(DateTime sonGun, DateTime bugun, bool tamam)
        {
            if (tamam) return (IsDurumu.Tamam, 0);

            var fark = (sonGun.Date - bugun.Date).Days;
            if (fark < 0) return (IsDurumu.Gecikti, -fark);
            if (fark <= BuHaftaGun) return (IsDurumu.BuHafta, fark);
            if (sonGun.Year == bugun.Year && sonGun.Month == bugun.Month) return (IsDurumu.BuAy, 0);
            return (IsDurumu.Sonra, 0);
        }

        /// <summary>"her ay · ayın 1'i" gibi okunur kural.</summary>
        public static string KuralMetni(IsTekrari tekrar, IsGunKurali kural, int? ayinGunu, DateTime? tekSefer)
        {
            if (tekrar == IsTekrari.TekSefer)
                return tekSefer is { } t ? $"tek sefer · {t:dd.MM.yyyy}" : "tek sefer";

            var siklik = TekrarAdi(tekrar);
            var ilkAy = tekrar == IsTekrari.Aylik ? "ayın" : tekrar == IsTekrari.UcAylik ? "çeyreğin ilk ayının" : "ocak ayının";
            var gun = kural switch
            {
                IsGunKurali.AyinGunu => $"{ilkAy} {ayinGunu ?? 1}'{Ek(ayinGunu ?? 1)}",
                IsGunKurali.AySonu => $"{ilkAy} son günü",
                _ => "dönem sonu"
            };
            return $"{siklik} · {gun}";
        }

        public static string TekrarAdi(IsTekrari tekrar) => tekrar switch
        {
            IsTekrari.Aylik => "her ay",
            IsTekrari.UcAylik => "üç ayda bir",
            IsTekrari.Yillik => "her yıl",
            _ => "tek sefer"
        };

        /// <summary>Sayının Türkçe iyelik/belirtme eki: 1'i, 2'si, 3'ü, 6'sı, 9'u, 10'u …</summary>
        private static string Ek(int n)
        {
            var son = n % 10;
            if (son == 0)
                return (n % 100) switch
                {
                    10 or 30 => "u",
                    20 => "si",
                    _ => "ı"
                };
            return son switch
            {
                1 or 5 or 8 => "i",
                2 or 7 => "si",
                3 or 4 => "ü",
                6 => "sı",
                _ => "u"
            };
        }
    }
}
