namespace CatalogService.Api.Features.Parolalar.Domain
{
    /// <summary>Parolalar sayfasının üç sekmesi.</summary>
    public enum ParolaTuru : byte
    {
        Entegrator = 1,
        DijitalVergiDairesi = 2,
        Sgk = 3
    }

    /// <summary>
    /// Yönetim › Parolalar: bir müşteri firmanın Entegratör / Dijital VD / SGK giriş bilgisi. Üç tür TEK tabloda (Tur):
    /// liste, arama, firma süzgeci, şifreleme ve erişim kaydı üçünde aynı; türe özgü alanlar az ve isteğe bağlı
    /// (Entegrator/PortalAdresi yalnız Entegratör'de, Sube/KullaniciKoduEk yalnız SGK'da). Hangi alanın hangi türde
    /// zorunlu olduğu ParolaKurallari'nda.
    ///
    /// Şifre alanları (…Sifreli) Data Protection ile ŞİFRELİ ("Parolalar.v1"); düz metin veritabanına hiç yazılmaz.
    /// Kullanıcı adı / kodu, portal ve not düz metin. Global — tenant'a bakmaz, firma veri boyutudur.
    ///
    /// Firma: FirmaId (Firmalarım) ya da FirmaId boş + elle yazılan FirmaAdi. FirmaId doluyken de FirmaAdi kayıt
    /// anındaki ad olarak saklanır: firma silinirse FK SetNull olur ve kayıt bu adla "elle yazılmış" gibi kalır.
    /// </summary>
    public class Parola
    {
        public int Id { get; set; }
        public ParolaTuru Tur { get; set; }

        public int? FirmaId { get; set; }

        /// <summary>Elle yazılan ad (FirmaId boşsa) ya da Firmalarım'daki adın kayıt anındaki kopyası.</summary>
        public string FirmaAdi { get; set; } = string.Empty;

        /// <summary>Yalnız Entegratör: Digital Planet, eLogo, … , Diğer.</summary>
        public string? Entegrator { get; set; }

        /// <summary>Yalnız Entegratör, isteğe bağlı: giriş sayfasının adresi (http/https).</summary>
        public string? PortalAdresi { get; set; }

        /// <summary>Entegratör: kullanıcı adı · Dijital VD: kullanıcı kodu · SGK: kullanıcı adı (TCKN).</summary>
        public string KullaniciAdi { get; set; } = string.Empty;

        /// <summary>Yalnız SGK, isteğe bağlı: kullanıcı kodu eki ("2", "22").</summary>
        public string? KullaniciKoduEk { get; set; }

        /// <summary>Yalnız SGK, isteğe bağlı: şube / işyeri. Aynı firma + şube bir kez.</summary>
        public string? Sube { get; set; }

        /// <summary>Entegratör ve Dijital VD "Şifre" — şifreli.</summary>
        public string? SifreSifreli { get; set; }

        /// <summary>Dijital VD "Parola" — şifreli.</summary>
        public string? ParolaSifreli { get; set; }

        /// <summary>SGK "Sistem şifresi" — şifreli.</summary>
        public string? SistemSifresiSifreli { get; set; }

        /// <summary>SGK "İşyeri şifresi" — şifreli.</summary>
        public string? IsyeriSifresiSifreli { get; set; }

        public string? Not { get; set; }

        /// <summary>
        /// Yalnız SGK: firma + şube anahtarı ("F12|BURSA" ya da "E:ABC LTD|"). Filtreli unique index; diğer türlerde null.
        /// </summary>
        public string? TekillikAnahtari { get; set; }

        public DateTime OlusturmaTarihi { get; set; }
        public DateTime GuncellemeTarihi { get; set; }
        public int? GuncelleyenKullaniciId { get; set; }
        public string? GuncelleyenKullanici { get; set; }
    }

    /// <summary>
    /// Parola erişim / değişiklik kaydı: görüntüleme, kopyalama, ekleme, düzenleme, silme. ŞİFRE METNİ İÇERMEZ (yalnız
    /// alan adı). Parola'ya FK yok: kayıt silinince erişim kayıtları kalır (FirmaAdi ve Tur kopyası bu yüzden burada).
    /// </summary>
    public class ParolaErisimKaydi
    {
        public long Id { get; set; }
        public int ParolaId { get; set; }
        /// <summary>"reddedildi" kaydında id bilinmiyorsa (liste / ekle) ya da kayıt yoksa 0.</summary>
        public ParolaTuru Tur { get; set; }

        public string FirmaAdi { get; set; } = string.Empty;

        /// <summary>Görüntüle/kopyala: tek alan ("sifre"); düzenle: değişen şifre alanları ("sifre,parola") ya da null.</summary>
        public string? Alan { get; set; }

        /// <summary><see cref="ParolaIslemleri"/>.</summary>
        public string Islem { get; set; } = string.Empty;

        public int? KullaniciId { get; set; }
        public string? Kullanici { get; set; }
        public DateTime Tarih { get; set; }
    }

    public static class ParolaIslemleri
    {
        public const string Goruntule = "goruntule";
        public const string Kopyala = "kopyala";
        public const string Ekle = "ekle";
        public const string Duzenle = "duzenle";
        public const string Sil = "sil";

        /// <summary>Yetkisiz (403) istek — pkf rolü olmayan kullanıcı.</summary>
        public const string Reddedildi = "reddedildi";
    }
}
