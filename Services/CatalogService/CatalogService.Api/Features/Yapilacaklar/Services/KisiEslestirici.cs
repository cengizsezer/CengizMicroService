using CatalogService.Api.Features.Yapilacaklar.Domain;

namespace CatalogService.Api.Features.Yapilacaklar.Services
{
    /// <summary>
    /// Alıcı → kişi eşleştirmesi (Prompt 14) — saf, veritabanı yok. Seed'deki taşıma ve her alıcı yazması
    /// AYNI kuralı kullanır. Kapsam hep TEK FİRMA: farklı firmadaki aynı e-posta ayrı kişidir.
    ///
    /// <list type="number">
    /// <item>E-posta doluysa: aynı e-postalı kişi (büyük/küçük harf ve boşluk farkı yok) → o kişi. Aynı e-posta TEK kişidir.</item>
    /// <item>E-posta doluysa ve eşleşen yoksa: aynı adlı TEK kişi var ve e-postası boşsa → o kişiye e-posta yazılır
    /// (aynı kişinin adresi sonradan girildi). Yoksa yeni kişi.</item>
    /// <item>E-posta boşsa: ada göre. Aynı adlı e-postasız kişi varsa o; aynı adlı tek kişi varsa o; birden çok
    /// (farklı e-postalı) aday varsa SEÇİLMEZ — yeni e-postasız kişi açılır ve şüpheli raporlanır.</item>
    /// </list>
    /// Şüpheli eşleşme otomatik BİRLEŞTİRİLMEZ; <see cref="Supheliler"/> raporlar, kullanıcı birleştirir.
    /// </summary>
    public static class KisiEslestirici
    {
        public enum Sonuc : byte
        {
            EpostaIleBulundu = 1,
            AdIleBulundu = 2,
            EpostaEklendi = 3,
            YeniKisi = 4,

            /// <summary>E-postasız ad birden çok kişiye uyuyordu: seçilmedi, yeni kişi açıldı (şüpheli).</summary>
            YeniKisiBelirsiz = 5
        }

        public static string EpostaAnahtari(string? eposta)
            => string.IsNullOrWhiteSpace(eposta) ? string.Empty : eposta.Trim().ToLowerInvariant();

        /// <summary>Ad karşılaştırması: tarif başlığıyla aynı Türkçe normalize (İ/I, boşluk, noktalama).</summary>
        public static string AdAnahtari(string? ad) => IsTarifiKurucu.Normalize(ad);

        /// <summary>
        /// Firmanın kişi listesinde (<paramref name="firmaKisileri"/>, yeni açılanlar da eklenir) alıcıyı
        /// eşler. Yeni kişi listeye eklenir; veritabanına eklemek çağıranın işi.
        /// </summary>
        public static (Kisi Kisi, Sonuc Sonuc) Esle(List<Kisi> firmaKisileri, int firmaId, string ad, string? eposta, string? rol)
        {
            var e = EpostaAnahtari(eposta);
            var a = AdAnahtari(ad);

            if (e.Length > 0)
            {
                var ayniEposta = firmaKisileri.FirstOrDefault(k => EpostaAnahtari(k.Eposta) == e);
                if (ayniEposta is not null) return (RolTamamla(ayniEposta, rol), Sonuc.EpostaIleBulundu);

                var ayniAd = firmaKisileri.Where(k => AdAnahtari(k.Ad) == a).ToList();
                if (a.Length > 0 && ayniAd.Count == 1 && EpostaAnahtari(ayniAd[0].Eposta).Length == 0)
                {
                    ayniAd[0].Eposta = eposta!.Trim();
                    return (RolTamamla(ayniAd[0], rol), Sonuc.EpostaEklendi);
                }
            }
            else
            {
                var adaylar = firmaKisileri.Where(k => a.Length > 0 && AdAnahtari(k.Ad) == a).ToList();
                var epostasiz = adaylar.FirstOrDefault(k => EpostaAnahtari(k.Eposta).Length == 0);
                if (epostasiz is not null) return (RolTamamla(epostasiz, rol), Sonuc.AdIleBulundu);
                if (adaylar.Count == 1) return (RolTamamla(adaylar[0], rol), Sonuc.AdIleBulundu);
                if (adaylar.Count > 1) return (Yeni(firmaKisileri, firmaId, ad, null, rol), Sonuc.YeniKisiBelirsiz);
            }

            return (Yeni(firmaKisileri, firmaId, ad, e.Length > 0 ? eposta!.Trim() : null, rol), Sonuc.YeniKisi);
        }

        private static Kisi Yeni(List<Kisi> liste, int firmaId, string ad, string? eposta, string? rol)
        {
            var k = new Kisi
            {
                FirmaId = firmaId,
                Ad = Kes(ad.Trim(), 150),
                Eposta = eposta is null ? null : Kes(eposta, 200),
                Rol = string.IsNullOrWhiteSpace(rol) ? null : Kes(rol.Trim(), 100)
            };
            liste.Add(k);
            return k;
        }

        /// <summary>Kişinin rolü boşsa alıcınınkiyle tamamlanır; doluysa dokunulmaz.</summary>
        private static Kisi RolTamamla(Kisi k, string? rol)
        {
            if (string.IsNullOrWhiteSpace(k.Rol) && !string.IsNullOrWhiteSpace(rol)) k.Rol = Kes(rol.Trim(), 100);
            return k;
        }

        private static string Kes(string s, int n) => s.Length > n ? s[..n] : s;

        /// <summary>
        /// Kaydetmeden önizleme (Prompt 14B): alıcılar SIRAYLA eşlenir (yazma gibi — önceki satırın açacağı
        /// kişi sonrakini etkiler), ama kişilerin KOPYASI üzerinde; girdi listesi değişmez.
        /// </summary>
        public static List<Dtos.AliciOnizlemeDto> Onizle(IEnumerable<Kisi> kayitlilar, int firmaId,
                                                         IEnumerable<(string Ad, string? Eposta)> alicilar)
        {
            var kopya = kayitlilar.Select(k => new Kisi { Id = k.Id, FirmaId = k.FirmaId, Ad = k.Ad, Eposta = k.Eposta, Rol = k.Rol }).ToList();
            var once = kopya.ToDictionary(k => k.Id, k => k.Eposta);
            var sonuc = new List<Dtos.AliciOnizlemeDto>();

            foreach (var (ad, eposta) in alicilar)
            {
                if (string.IsNullOrWhiteSpace(ad))
                {
                    sonuc.Add(new Dtos.AliciOnizlemeDto());
                    continue;
                }

                var (kisi, s) = Esle(kopya, firmaId, ad, eposta, null);
                var kayitli = kisi.Id != 0 && s is Sonuc.EpostaIleBulundu or Sonuc.AdIleBulundu or Sonuc.EpostaEklendi;
                var kayitliEposta = kisi.Id != 0 ? once.GetValueOrDefault(kisi.Id) : null;
                sonuc.Add(new Dtos.AliciOnizlemeDto
                {
                    Kayitli = kayitli,
                    KisiId = kayitli ? kisi.Id : null,
                    KayitliAd = kayitli ? kisi.Ad : null,
                    KayitliEposta = kayitli ? kayitliEposta : null,
                    EpostaKisidenGelir = kayitli && string.IsNullOrWhiteSpace(eposta) && !string.IsNullOrWhiteSpace(kayitliEposta),
                    // Kopya kişiden yazılır: harfiyen farklıysa ekranda değişecek demektir.
                    AdFarkli = kayitli && !string.Equals(ad.Trim(), kisi.Ad, StringComparison.Ordinal)
                });
            }
            return sonuc;
        }

        /// <summary>Olası aynı kişi: aynı firmada aynı ad (farklı kayıt) ya da tek harf farklı ad.</summary>
        public enum SupheTuru : byte
        {
            AyniAd = 1,
            BenzerAd = 2
        }

        public sealed record Suphe(int FirmaId, SupheTuru Tur, List<Kisi> Kisiler);

        /// <summary>
        /// Şüpheli kişi grupları — seed raporu ve Kişiler sekmesi AYNI fonksiyonu kullanır. Birleştirmez.
        /// </summary>
        public static List<Suphe> Supheliler(IEnumerable<Kisi> kisiler)
        {
            var sonuc = new List<Suphe>();
            foreach (var firma in kisiler.GroupBy(k => k.FirmaId))
            {
                var liste = firma.OrderBy(k => k.Id).ToList();
                var kullanilan = new HashSet<Kisi>();

                foreach (var g in liste.GroupBy(k => AdAnahtari(k.Ad)).Where(g => g.Key.Length > 0 && g.Count() > 1))
                {
                    sonuc.Add(new Suphe(firma.Key, SupheTuru.AyniAd, g.ToList()));
                    kullanilan.UnionWith(g);
                }

                for (var i = 0; i < liste.Count; i++)
                    for (var j = i + 1; j < liste.Count; j++)
                    {
                        var (x, y) = (AdAnahtari(liste[i].Ad), AdAnahtari(liste[j].Ad));
                        if (x == y || x.Length == 0 || y.Length == 0) continue;
                        if (Sistemler.Domain.SistemAdi.Benzer(liste[i].Ad, liste[j].Ad)
                            && !(kullanilan.Contains(liste[i]) && kullanilan.Contains(liste[j])))
                            sonuc.Add(new Suphe(firma.Key, SupheTuru.BenzerAd, new List<Kisi> { liste[i], liste[j] }));
                    }
            }
            return sonuc;
        }
    }
}
