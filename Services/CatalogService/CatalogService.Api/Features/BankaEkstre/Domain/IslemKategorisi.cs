namespace CatalogService.Api.Features.BankaEkstre.Domain
{
    /// <summary>
    /// İşlem kategorisi: kuralların <b>muhasebe</b> sınıflandırması ("Banka gideri",
    /// "Personel iş avansı", "Müşteri tahsilatı"…).
    ///
    /// <b>Neden ayrı bir kavram?</b> Kurallar bugüne kadar mekanizmaya göre ayrılmıştı —
    /// sabit kural, vergi kodu, kişi yönlendirme, açıklama şablonu. Kullanıcı ise
    /// muhasebe kategorisine göre düşünüyor ve yeni banka eklerken "hangi kategoriler
    /// eksik?" diye kontrol ediyor. Kategori bu iki bakışı bağlar.
    ///
    /// <b>Eşleştirme mantığına girmez.</b> Kategori yalnız etiket ve görünüm; katman
    /// sırası, eşikler ve desenler kategoriden habersiz çalışır. Bir satırın hangi
    /// kategoriye düştüğü de karar değil sonuçtur: önerilen/onaylanan hesap kodunun ana
    /// grubundan okunur (bkz. <see cref="Services.KategoriCozucu"/>).
    ///
    /// Tablo <b>global</b>: kategoriler bankadan ve firmadan bağımsızdır — "banka gideri"
    /// her firmada aynı şeydir. Hangi hesaba gittiği zaten kuralın kendi alanında.
    /// </summary>
    public class IslemKategorisi
    {
        public int Id { get; set; }

        /// <summary>Ekranda görünen ad, ör. "Personel iş avansı".</summary>
        public string Ad { get; set; } = string.Empty;

        /// <summary>
        /// Kategorinin varsayılan ana hesap grubu, ör. "195". Ekstre satırının etiketi
        /// buradan bulunur: satırın hesap kodunun ana grubu bu değere eşitse satır bu
        /// kategoridedir. Boş bırakılabilir — o kategori yalnız kural etiketi olur,
        /// satırlarda görünmez.
        /// </summary>
        public string? VarsayilanAnaGrup { get; set; }

        public int Sira { get; set; }

        public bool Aktif { get; set; } = true;
    }
}
