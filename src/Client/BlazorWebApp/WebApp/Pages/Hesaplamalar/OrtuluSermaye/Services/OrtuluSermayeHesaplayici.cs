using WebApp.Pages.Hesaplamalar.OrtuluSermaye.Model;

namespace WebApp.Pages.Hesaplamalar.OrtuluSermaye.Services
{
    /// <summary>Sayfa 1'in sonucu: zirve ve satır bazında kümülatif bakiye.</summary>
    public sealed class EnYuksekBorcSonucu
    {
        public decimal Tutar { get; init; }
        public DateTime? Tarih { get; init; }

        /// <summary>Zirve gününün SON hareketi (ekranda vurgulanan satır); zirve yoksa null.</summary>
        public MuavinSatiri? ZirveSatiri { get; init; }

        /// <summary>Dahil edilen her satırdan sonraki toplam ilişkili borç. Hariç hesapların satırları burada yok.</summary>
        public IReadOnlyDictionary<MuavinSatiri, decimal> Kumulatif { get; init; } = new Dictionary<MuavinSatiri, decimal>();
    }

    /// <summary>
    /// Örtülü sermaye hesabı (KVK 12, KVK 11/1-b). Saf hesap: durum tutmaz, sunucuya gitmez
    /// (KARARLAR §156).
    /// </summary>
    public static class OrtuluSermayeHesaplayici
    {
        /// <summary>320 Satıcılar varsayılan olarak hariç: ticari borç, olağan vadeyi aşmadıkça örtülü sermaye sayılmaz.</summary>
        public static bool VarsayilanHaricMi(string hesapKodu) => hesapKodu.Trim().StartsWith("320");

        public static HashSet<string> VarsayilanHaricler(IEnumerable<MuavinSatiri> satirlar)
            => satirlar.Select(s => s.HesapKodu).Where(VarsayilanHaricMi).ToHashSet();

        /// <summary>
        /// Seçili hesapların TOPLAM alacak bakiyesinin dönem içindeki zirvesi. Hareketler
        /// tarihe göre sıralanır (aynı gün içinde yapıştırma sırası korunur), kümülatif
        /// (Alacak − Borç) tutulur ve zirve yalnız bir günün TÜM hareketleri işlendikten
        /// sonra kontrol edilir. Hesap bazında maksimumlar toplanmaz.
        /// </summary>
        public static EnYuksekBorcSonucu EnYuksekBorc(IEnumerable<MuavinSatiri> satirlar, IReadOnlySet<string> haricHesaplar)
        {
            var sirali = satirlar.Where(s => !haricHesaplar.Contains(s.HesapKodu))
                                 .OrderBy(s => s.Tarih.Date)
                                 .ToList();

            var kumulatif = new Dictionary<MuavinSatiri, decimal>();
            decimal toplam = 0, zirve = 0;
            MuavinSatiri? zirveSatiri = null;

            for (var i = 0; i < sirali.Count; i++)
            {
                var s = sirali[i];
                toplam += s.Alacak - s.Borc;
                kumulatif[s] = toplam;

                var gunBitti = i == sirali.Count - 1 || sirali[i + 1].Tarih.Date != s.Tarih.Date;
                if (gunBitti && toplam > zirve)
                {
                    zirve = toplam;
                    zirveSatiri = s;
                }
            }

            return new EnYuksekBorcSonucu
            {
                Tutar = zirve,
                Tarih = zirveSatiri?.Tarih.Date,
                ZirveSatiri = zirveSatiri,
                Kumulatif = kumulatif
            };
        }

        /// <summary>Sınıf bazında oransız toplamlar. Giderlerde Borç − Alacak; kur farkı geliri ve stopajda Alacak − Borç.</summary>
        public static SinifToplamlari Toplamlar(IEnumerable<MuavinSatiri> giderSatirlari)
        {
            var t = new SinifToplamlari();
            foreach (var s in giderSatirlari)
            {
                var gider = s.Borc - s.Alacak;
                var gelir = s.Alacak - s.Borc;
                switch (s.Sinif)
                {
                    case MuavinSinifi.KurFarkiGideri: t.KurFarkiGideri += gider; break;
                    case MuavinSinifi.KurFarkiGeliri: t.KurFarkiGeliri += gelir; break;
                    case MuavinSinifi.FaizGideri: t.FaizGideri += gider; break;
                    case MuavinSinifi.FaizKdv: t.FaizKdv += gider; break;
                    case MuavinSinifi.Stopaj: t.Stopaj += gelir; break;
                    case MuavinSinifi.DamgaVergisi: t.DamgaVergisi += gider; break;
                }
            }
            return t;
        }

        public static OrtuluSermayeSonuc Hesapla(
            OrtuluSermayeParametre parametre,
            IEnumerable<MuavinSatiri> borcSatirlari,
            IReadOnlySet<string> haricHesaplar,
            IEnumerable<MuavinSatiri> giderSatirlari)
        {
            var zirve = EnYuksekBorc(borcSatirlari, haricHesaplar);
            var toplamlar = Toplamlar(giderSatirlari);

            var dikkate = parametre.IliskiliBankaMi ? zirve.Tutar / 2 : zirve.Tutar;
            // Negatif öz sermayede sınır 0: borcun tamamı örtülüdür, oran 1'i aşmaz.
            var sinir = Math.Max(0, parametre.DonemBasiOzSermaye * 3);
            var asan = Math.Max(0, dikkate - sinir);
            var oran = dikkate > 0 ? asan / dikkate : 0;

            var faiz = toplamlar.FaizGideri * oran;
            var sonuc = new OrtuluSermayeSonuc
            {
                EnYuksekBorc = zirve.Tutar,
                EnYuksekBorcTarihi = zirve.Tarih,
                DikkateAlinanBorc = dikkate,
                Sinir = sinir,
                AsanKisim = asan,
                OrtuluOran = oran,
                KurFarkiGideri = toplamlar.KurFarkiGideri * oran,
                KurFarkiGeliri = toplamlar.KurFarkiGeliri * oran,
                FaizGideri = faiz,
                // Kur farkı stopaja konu değil (KVK 12/7): yalnız faiz.
                FaizStopaji = parametre.BorcVerenTipi == BorcVerenTipi.GercekKisiVeyaDarMukellef
                    ? faiz * parametre.StopajOrani
                    : 0,
                FaizKdv = toplamlar.FaizKdv * oran,
                DamgaVergisi = toplamlar.DamgaVergisi * oran,
                Toplamlar = toplamlar
            };
            // Kur farkı geliri gidere mahsup EDİLMEZ; ayrı döner.
            sonuc.KkegToplam = sonuc.KurFarkiGideri + sonuc.FaizGideri + sonuc.FaizKdv + sonuc.DamgaVergisi;
            return sonuc;
        }
    }
}
