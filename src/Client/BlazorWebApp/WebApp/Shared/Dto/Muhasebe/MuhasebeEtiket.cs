using System.Globalization;

namespace WebApp.Shared.Dto.Muhasebe
{
    /// <summary>Muhasebe enum'larının ekranda gösterilecek Türkçe karşılıkları.</summary>
    public static class MuhasebeEtiket
    {
        /// <summary>
        /// Muhasebe ekranlarının biçimlendirme kültürü. Uygulama genelinde bir kültür
        /// sabitlenmediği (tarayıcı diline göre değişiyor) ve projedeki diğer modüller de
        /// her çağrıda tr-TR verdiği için tutar ve tarihler burada açıkça tr-TR ile yazılır.
        /// Aksi hâlde İngilizce tarayıcıda tutar <c>1,234.56</c>, tarih <c>MM/dd/yyyy</c> olur.
        /// </summary>
        public static readonly CultureInfo Kultur = new("tr-TR");

        /// <summary>Tarih giriş kutularının ve gösterimlerinin ortak biçimi.</summary>
        public const string TarihBicimi = "dd.MM.yyyy";

        public static string Karakter(HesapKarakter k) => k switch
        {
            HesapKarakter.Aktif => "Aktif",
            HesapKarakter.Pasif => "Pasif",
            HesapKarakter.Gelir => "Gelir",
            HesapKarakter.Gider => "Gider",
            HesapKarakter.Maliyet => "Maliyet",
            HesapKarakter.Nazim => "Nazım",
            _ => "—"
        };

        public static string Tur(HesapTuru t) => t switch
        {
            HesapTuru.Sinif => "Sınıf",
            HesapTuru.Grup => "Grup",
            HesapTuru.Kebir => "Kebir",
            HesapTuru.Muavin => "Muavin",
            _ => "—"
        };

        /// <summary>Bakiyenin kaldığı taraf; T cetvelindeki ifadeyle aynı.</summary>
        public static string Yon(BakiyeYonu y) => y switch
        {
            BakiyeYonu.Borc => "Borç kalanı",
            BakiyeYonu.Alacak => "Alacak kalanı",
            _ => "Bakiye yok"
        };

        /// <summary>Parasal gösterim; kural gereği her yerde iki hane, tr-TR ayracıyla.</summary>
        public static string Para(decimal tutar) => tutar.ToString("N2", Kultur);

        /// <summary>Gün.Ay.Yıl — tüm muhasebe ekranlarında aynı sıra.</summary>
        public static string Tarih(DateTime t) => t.ToString(TarihBicimi, Kultur);

        /// <summary>Dar kolonlarda kullanılan kısa biçim (gg.AA.yy).</summary>
        public static string TarihKisa(DateTime t) => t.ToString("dd.MM.yy", Kultur);
    }
}
