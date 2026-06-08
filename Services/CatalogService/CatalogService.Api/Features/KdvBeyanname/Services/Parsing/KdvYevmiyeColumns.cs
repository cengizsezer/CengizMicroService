namespace CatalogService.Api.Features.KdvBeyanname.Services.Parsing
{
    /// <summary>
    /// Yevmiye Excel'inde beklenen tek bir sütun tanımı: kanonik başlık,
    /// kabul edilen alternatif yazımlar ve zorunlu/opsiyonel bilgisi.
    /// </summary>
    public sealed class KdvYevmiyeColumn
    {
        /// <summary>Kanonik (birincil) başlık — alternatiflerin ilki.</summary>
        public string Ad { get; }

        /// <summary>Kabul edilen tüm yazımlar (ilki = <see cref="Ad"/>).</summary>
        public IReadOnlyList<string> Alternatifler { get; }

        /// <summary>True ise bu kolon yoksa dosya parse edilemez.</summary>
        public bool Zorunlu { get; }

        public KdvYevmiyeColumn(bool zorunlu, params string[] alternatifler)
        {
            if (alternatifler is null || alternatifler.Length == 0)
                throw new ArgumentException(
                    "En az bir başlık adı verilmeli.", nameof(alternatifler));
            Zorunlu = zorunlu;
            Alternatifler = alternatifler;
            Ad = alternatifler[0];
        }
    }

    /// <summary>
    /// Yevmiye Excel parser'ının beklediği sütun başlıklarının TEK DOĞRU KAYNAĞI
    /// (single source of truth). Hem <see cref="KdvYevmiyeExcelParser"/> hem de
    /// UI (beklenen başlık paneli + yükleme sonrası geri bildirim) buradan beslenir.
    /// Buradaki string'ler kodun fiilen aradığı başlıklardır; başka yerde
    /// elle ikinci bir liste tutulmamalıdır.
    /// </summary>
    public static class KdvYevmiyeColumns
    {
        // ── Zorunlu kolonlar (yoksa fatura eşleştirilemez / parse başarısız) ──
        public static readonly KdvYevmiyeColumn HesapKodu   = new(true,  "Hesap Kodu");
        public static readonly KdvYevmiyeColumn HesapAdi    = new(true,  "Hesap Adı", "Hesap Adi");
        public static readonly KdvYevmiyeColumn SNo         = new(true,  "S. No", "S.No", "SNo");
        public static readonly KdvYevmiyeColumn BelgeNo     = new(true,  "Belge No", "BelgeNo");
        public static readonly KdvYevmiyeColumn Borc        = new(true,  "Borç Tutar", "Borc Tutar", "Borç");
        public static readonly KdvYevmiyeColumn Alacak      = new(true,  "Alacak Tutar", "Alacak");

        // ── Opsiyonel kolonlar (yoksa ilgili alan null kalır) ────────────────
        public static readonly KdvYevmiyeColumn FisTarihi   = new(false, "Fiş Tarihi", "Fis Tarihi");
        public static readonly KdvYevmiyeColumn FisNo       = new(false, "Fiş No", "Fis No");
        public static readonly KdvYevmiyeColumn FisAciklama = new(false, "Fiş Açıklaması", "Fis Aciklamasi");
        public static readonly KdvYevmiyeColumn BelgeTipi   = new(false, "Belge Tipi");
        public static readonly KdvYevmiyeColumn BelgeTarihi = new(false, "Belge Tarihi");
        public static readonly KdvYevmiyeColumn Aciklama    = new(false, "Açıklama", "Aciklama");

        /// <summary>Tüm kolonlar — Excel'deki tipik soldan-sağa sırayla.</summary>
        public static readonly IReadOnlyList<KdvYevmiyeColumn> All = new[]
        {
            HesapKodu, HesapAdi, SNo, BelgeNo, Borc, Alacak,
            FisTarihi, FisNo, FisAciklama, BelgeTipi, BelgeTarihi, Aciklama
        };

        public static IEnumerable<KdvYevmiyeColumn> Zorunlular   => All.Where(c => c.Zorunlu);
        public static IEnumerable<KdvYevmiyeColumn> Opsiyoneller => All.Where(c => !c.Zorunlu);

        /// <summary>
        /// Verilen başlık listesinde bu kolonun alternatiflerinden herhangi biri
        /// var mı? Karşılaştırma <see cref="HeaderMap"/> ile aynı kuralı izler:
        /// büyük/küçük harf duyarsız, başlıklar trim'li.
        /// </summary>
        public static bool Eslesti(KdvYevmiyeColumn column, IEnumerable<string> bulunanBasliklar)
        {
            var set = new HashSet<string>(
                bulunanBasliklar.Select(b => b.Trim()),
                StringComparer.OrdinalIgnoreCase);
            return column.Alternatifler.Any(a => set.Contains(a));
        }
    }
}
