namespace CatalogService.Api.Features.BankaEkstre.Domain
{
    /// <summary>Banka hesabının tipi. Şimdilik yalnız vadesiz TL ekstresi ayrıştırılıyor.</summary>
    public enum HesapTipi : byte
    {
        Vadesiz = 1,
        Vadeli = 2
    }

    /// <summary>Ekstre satırının para yönü. Tutar her zaman pozitif saklanır, işaret bu alanda durur.</summary>
    public enum Yon : byte
    {
        Giren = 1,
        Cikan = 2
    }

    /// <summary>Ekstre yüklemesinin durumu.</summary>
    public enum YuklemeDurum : byte
    {
        Isleniyor = 0,
        Tamamlandi = 1,
        Hatali = 2
    }

    /// <summary>
    /// Satırın çözüm durumu. <see cref="DigerBankada"/> bankalar arası transferin karşı
    /// bacağı başka bankanın ekstresinde işlendiğinde kullanıcı tarafından elle işaretlenir
    /// ve satır dışa aktarımdan düşer.
    /// </summary>
    public enum SatirDurum : byte
    {
        Otomatik = 1,
        OnayBekliyor = 2,
        Onaylandi = 3,
        Cozulemedi = 4,
        DigerBankada = 5
    }

    /// <summary>
    /// Karşı hesabı hangi katmanın çözdüğü. Hata ayıklama için kritik: hangi katmanın
    /// yanıldığı bu alandan görülür, onay ekranında etiket olarak gösterilir.
    ///
    /// Sayısal değerler sözleşmenin parçası (istemci DTO'su aynı sırayı kullanıyor).
    /// <see cref="Iban"/> ve <see cref="Vkn"/> katmanları varsayılan olarak kapalıdır
    /// (<see cref="BankaHesabi.IbanKatmaniAktif"/> / <see cref="BankaHesabi.VknKatmaniAktif"/>);
    /// enum değerleri korundu ki başka bankada açıldığında etiketler değişmesin.
    /// </summary>
    public enum KaynakKatman : byte
    {
        Yok = 0,
        Iban = 1,
        Vkn = 2,
        GecmisOnay = 3,
        BankaKayitDefteri = 4,
        SabitKural = 5,
        UnvanBenzerligi = 6,
        Kullanici = 7,

        /// <summary>
        /// Benzersiz önek: hesap adı çekirdeği açıklamanın bir token dizisiyle başlayan
        /// tek cari (bkz. <see cref="Services.CariOnekIndeksi"/>). Desen tabanlı unvan
        /// benzerliğinden <b>önce</b> denenir; ölçümde isabeti %98.
        /// </summary>
        BenzersizOnek = 8,

        /// <summary>Vergi kodu / anahtar kelime eşleme tablosu veya plaka anahtarı.</summary>
        VergiPlaka = 9
    }

    /// <summary>
    /// Öğrenme anahtarının tipi. Varsayılan <see cref="UnvanCekirdek"/>: ham açıklamanın
    /// hash'i değil, normalize edilmiş unvan çekirdeği. Ham hash asla ikinci kez eşleşmiyordu
    /// (banka her satıra farklı sorgu numarası/tarih/tutar yazıyor).
    /// </summary>
    public enum AnahtarTipi : byte
    {
        /// <summary>Normalize unvan çekirdeği veya unvansız satırlarda "ISLEM:&lt;işlem tipi&gt;".</summary>
        UnvanCekirdek = 1,
        Iban = 2,
        Vkn = 3,

        /// <summary>
        /// Kullanıcının çözdüğü <b>belirsizlik</b>. Anahtar, belirsizliği üreten n-gram
        /// ("PARK PLAZA YONETIMI", "PARDUS PORTFOY YONETIMI"); değer seçilen hesap kodu.
        /// Aynı belirsizlik bir daha sorulmaz — kullanıcı değiştirene kadar.
        ///
        /// Kayıt aday kümesinin özetiyle birlikte saklanır: yeni bir cari açılıp küme
        /// değişirse karar sessizce uygulanmaz, satır tekrar onaya düşer.
        /// </summary>
        Belirsizlik = 4
    }

    /// <summary>
    /// Sabit kuralın deseni hangi metinde aranacak. Varsayılan <see cref="IslemTipi"/>:
    /// eski kurallar (banka masrafı, HGS) işlem tipi kolonuna bakar.
    ///
    /// <see cref="Aciklama"/> kuralları ham banka açıklamasında arar ve <b>öğrenme
    /// katmanından önce</b> çalışır: "iş avansı", "maaş avansı" gibi ifadeler işlemin
    /// niteliğini belirler, karşı tarafın kimliğini değil. Bu satırlarda çıkarılan unvan
    /// bir cari sayılmaz; yalnız kuralın ana grubu içinde kişi muavini aramakta kullanılır.
    /// </summary>
    public enum KuralKapsami : byte
    {
        IslemTipi = 1,
        Aciklama = 2
    }

    /// <summary>Şablon/desen/kural tablolarında işlem tipinin nasıl eşleşeceği.</summary>
    public enum EslesmeTuru : byte
    {
        /// <summary>Tam eşitlik (normalize edilmiş, büyük/küçük harf duyarsız).</summary>
        Tam = 1,
        /// <summary>İşlem tipi metni deseni içeriyorsa.</summary>
        Icerir = 2,
        /// <summary>Desen bir .NET regex'i.</summary>
        Regex = 3
    }
}
