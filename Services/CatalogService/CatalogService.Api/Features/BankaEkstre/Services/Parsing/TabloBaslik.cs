namespace CatalogService.Api.Features.BankaEkstre.Services.Parsing
{
    /// <summary>
    /// Bir kolonun aranış biçimi: hangi başlık adlarıyla, bulunamazsa hangi indekse
    /// düşüleceği ve satırın başlık sayılması için şart olup olmadığı.
    /// </summary>
    public sealed class KolonTanimi
    {
        public KolonTanimi(string anahtar, int varsayilanIndeks, bool zorunlu, params string[] adaylar)
        {
            Anahtar = anahtar;
            VarsayilanIndeks = varsayilanIndeks;
            Zorunlu = zorunlu;
            Adaylar = adaylar;
        }

        /// <summary>Ayrıştırıcının kolonu çağırdığı ad ("tarih", "tutar", "aciklama").</summary>
        public string Anahtar { get; }

        /// <summary>Ölçülen 1 tabanlı kolon numarası; başlık bulunamazsa buraya düşülür.</summary>
        public int VarsayilanIndeks { get; }

        /// <summary>Bu kolon bulunamazsa satır başlık satırı sayılmaz.</summary>
        public bool Zorunlu { get; }

        /// <summary>Sırayla denenen başlık adları; ilk bulunan kazanır.</summary>
        public IReadOnlyList<string> Adaylar { get; }
    }

    /// <summary>Kolon adı → 1 tabanlı kolon numarası. Tanımsız anahtar 0 verir (= kolon yok).</summary>
    public sealed class KolonHaritasi
    {
        private readonly IReadOnlyDictionary<string, int> _kolonlar;

        public KolonHaritasi(IReadOnlyDictionary<string, int> kolonlar) => _kolonlar = kolonlar;

        public int this[string anahtar] => _kolonlar.TryGetValue(anahtar, out var kolon) ? kolon : 0;
    }

    /// <summary>
    /// Başlık satırını <b>isimle</b> bulur; bulamazsa ölçülen sabit indekslere düşer ve
    /// taranan satırlarda ne görüldüğünü uyarı olarak yazar.
    ///
    /// Karşılaştırma <see cref="Services.Normalizasyon.MetinNormalize"/> üzerinden yapılır.
    /// Ordinal karşılaştırma yetmiyor: invariant kültür 'ı' → 'I' ve 'i' → 'İ' dönüşümünü
    /// yapmadığı için "AÇIKLAMA" ile "Açıklama" <c>OrdinalIgnoreCase</c> altında bile
    /// eşleşmiyor. Vakıfbank ayrıştırıcısı bu yüzden bir dönem sessizce sabit indekslere
    /// düşüyordu; aynı tuzağa üç yeni banka da girmesin diye kural burada tek yerde durur.
    /// </summary>
    public static class TabloBaslik
    {
        /// <summary>Başlık satırı, ölçülen veri başlangıcının en fazla bu kadar altında/üstünde aranır.</summary>
        private const int TaramaPayi = 4;

        public static (KolonHaritasi Kolonlar, int IlkVeriSatiri) Bul(
            EkstreTablosu tablo,
            IReadOnlyList<KolonTanimi> tanimlar,
            int varsayilanIlkVeriSatiri,
            EkstreParseSonuc sonuc)
        {
            var sonTaranan = Math.Min(tablo.SonSatirNo, varsayilanIlkVeriSatiri + TaramaPayi);
            var gorulenler = new List<string>();

            foreach (var satir in tablo.Satirlar.Where(s => s.SatirNo <= sonTaranan))
            {
                var harita = Esle(satir, tanimlar);
                if (harita is not null) return (harita, satir.SatirNo + 1);

                gorulenler.Add($"  satır {satir.SatirNo}: {satir.Ozet()}");
            }

            var zorunlular = string.Join(", ", tanimlar.Where(t => t.Zorunlu).Select(t => t.Anahtar));
            var varsayilanlar = string.Join(", ", tanimlar.Select(t => $"{t.Anahtar}={t.VarsayilanIndeks}"));

            sonuc.Uyarilar.Add(
                "Başlık satırı bulunamadı; ölçülen sabit kolon indekslerine düşüldü " +
                $"({varsayilanlar}). Başlık satırı sayılması için şu kolonların tanınması " +
                $"gerekiyor: {zorunlular}. Taranan satırlarda görülen metinler:" + Environment.NewLine +
                string.Join(Environment.NewLine, gorulenler));

            var varsayilan = tanimlar.ToDictionary(t => t.Anahtar, t => t.VarsayilanIndeks, StringComparer.Ordinal);
            return (new KolonHaritasi(varsayilan), varsayilanIlkVeriSatiri);
        }

        /// <summary>Satırı başlık satırı olarak yorumlamayı dener; zorunlu kolonlar eksikse null.</summary>
        private static KolonHaritasi? Esle(TabloSatiri satir, IReadOnlyList<KolonTanimi> tanimlar)
        {
            var basliklar = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var (kolon, hucre) in satir.DoluHucreler())
            {
                var metin = Services.Normalizasyon.MetinNormalize(hucre.Metin);
                if (metin.Length == 0) continue;

                // İlk yazan kazanır: aynı ad iki kez geçerse soldaki kolon kullanılır.
                basliklar.TryAdd(metin, kolon);
            }

            if (basliklar.Count == 0) return null;

            var harita = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var tanim in tanimlar)
            {
                var bulunan = Ara(basliklar, tanim.Adaylar);
                if (bulunan is null && tanim.Zorunlu) return null;

                harita[tanim.Anahtar] = bulunan ?? tanim.VarsayilanIndeks;
            }

            return new KolonHaritasi(harita);
        }

        private static int? Ara(Dictionary<string, int> basliklar, IReadOnlyList<string> adaylar)
        {
            foreach (var ad in adaylar)
                if (basliklar.TryGetValue(Services.Normalizasyon.MetinNormalize(ad), out var kolon))
                    return kolon;

            return null;
        }
    }
}
