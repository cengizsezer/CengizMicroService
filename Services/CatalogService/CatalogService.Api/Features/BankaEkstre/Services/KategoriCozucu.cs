using CatalogService.Api.Features.BankaEkstre.Domain;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    /// <summary>
    /// Ekstre satırının hangi işlem kategorisine düştüğü. Karar değil <b>sonuç</b>:
    /// satırın önerilen (yoksa onaylanan) hesap kodunun ana grubu, kategorinin
    /// <see cref="IslemKategorisi.VarsayilanAnaGrup"/> değerine eşitse satır o kategoridedir.
    ///
    /// <b>Neden koddan türetiliyor da satıra yazılmıyor?</b> Kategori yalnız etiket ve
    /// görünüm; eşleştirme mantığına girmiyor. Satıra kolon eklenseydi (a) hangi katmanın
    /// çözdüğünü eşleştiriciden dışarı taşımak, (b) kullanıcı kodu düzeltince etiketi de
    /// güncel tutmak gerekirdi. Koddan türetince etiket her zaman satırın <b>güncel</b>
    /// kodunu anlatır ve kategori tablosu değişince geçmiş satırlar da doğru etiketlenir.
    ///
    /// Kuralların kategorisi de aynı yoldan atanıyor (bkz. <c>BankaEkstreSeed</c>), böylece
    /// aynı hesap kuralda ve satırda aynı kategoriyi gösteriyor.
    /// </summary>
    public sealed class KategoriCozucu
    {
        /// <summary>Hiç kategori tanımlı değilken kullanılan boş çözücü.</summary>
        public static readonly KategoriCozucu Bos = new(new Dictionary<string, IslemKategorisi>(StringComparer.Ordinal));

        private readonly IReadOnlyDictionary<string, IslemKategorisi> _anaGruplar;

        private KategoriCozucu(IReadOnlyDictionary<string, IslemKategorisi> anaGruplar) => _anaGruplar = anaGruplar;

        /// <summary>
        /// Ana grup → kategori indeksi. Aynı ana gruba iki kategori tanımlanmışsa sırası
        /// küçük olan kazanır: etiket tek olmalı ve kullanıcı sırayı zaten yönetiyor.
        /// </summary>
        public static KategoriCozucu Kur(IEnumerable<IslemKategorisi>? kategoriler)
        {
            if (kategoriler is null) return Bos;

            var indeks = kategoriler
                .Where(k => k.Aktif && !string.IsNullOrWhiteSpace(k.VarsayilanAnaGrup))
                .OrderBy(k => k.Sira).ThenBy(k => k.Id)
                .GroupBy(k => Normalizasyon.AnaGrup(k.VarsayilanAnaGrup), StringComparer.Ordinal)
                .Where(g => g.Key.Length > 0)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            return new KategoriCozucu(indeks);
        }

        /// <summary>Hesap kodunun kategorisi; eşleşme yoksa (null, null).</summary>
        public (int? Id, string? Ad) Coz(string? hesapKodu)
        {
            var anaGrup = Normalizasyon.AnaGrup(hesapKodu);
            if (anaGrup.Length == 0) return (null, null);

            return _anaGruplar.TryGetValue(anaGrup, out var kategori)
                ? (kategori.Id, kategori.Ad)
                : (null, null);
        }
    }
}
