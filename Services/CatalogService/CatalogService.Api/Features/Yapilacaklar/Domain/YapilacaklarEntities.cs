namespace CatalogService.Api.Features.Yapilacaklar.Domain
{
    /// <summary>İşin ne sıklıkla tekrarladığı. Sayısal değerler istemciyle ortak.</summary>
    public enum IsTekrari : byte
    {
        TekSefer = 1,
        Aylik = 2,
        UcAylik = 3,
        Yillik = 4
    }

    /// <summary>
    /// Son günün dönem içinde nereye düştüğü. "Ay" dönemin <b>ilk ayıdır</b>: aylık işte
    /// dönemin kendisi, üç aylıkta çeyreğin ilk ayı, yıllıkta ocak.
    /// </summary>
    public enum IsGunKurali : byte
    {
        /// <summary>Ayın <see cref="FirmaIsi.AyinGunu"/>'ncü günü; ay o kadar gün çekmiyorsa son gününe düşer.</summary>
        AyinGunu = 1,

        /// <summary>Ayın son günü.</summary>
        AySonu = 2,

        /// <summary>Dönemin son günü: aylıkta ay sonu, üç aylıkta çeyrek sonu, yıllıkta 31 aralık.</summary>
        DonemSonu = 3
    }

    /// <summary>Tamamlama kaydının hangi tabloya ait olduğu.</summary>
    public enum IsKaynagi : byte
    {
        /// <summary><see cref="VergiTakvimi"/> satırı — mükellefiyetten türetilen yasal iş.</summary>
        Yasal = 1,

        /// <summary><see cref="FirmaIsi"/> — firmaya özel, elle eklenen iş.</summary>
        Ozel = 2
    }

    /// <summary>
    /// Firmaya özel tekrarlayan iş ("Yönetim raporu e-postası · her ay · ayın 1'i").
    /// Uygulama bu işi <b>yapmaz</b> (e-posta atmaz); yalnız hatırlatır ve yapıldı
    /// işaretini tutar.
    /// </summary>
    public class FirmaIsi
    {
        public int Id { get; set; }

        public int FirmaId { get; set; }

        public string Baslik { get; set; } = string.Empty;
        public string? Aciklama { get; set; }

        public IsTekrari Tekrar { get; set; } = IsTekrari.Aylik;
        public IsGunKurali GunKurali { get; set; } = IsGunKurali.AyinGunu;

        /// <summary>Yalnız <see cref="IsGunKurali.AyinGunu"/> ise dolu, 1–31.</summary>
        public int? AyinGunu { get; set; }

        /// <summary>Yalnız <see cref="IsTekrari.TekSefer"/> ise dolu; gün kuralı o zaman kullanılmaz.</summary>
        public DateTime? TekSeferTarih { get; set; }

        /// <summary>Boş = firmanın sorumlusu (<c>Firma.SorumluKullaniciId</c>).</summary>
        public int? SorumluKullaniciId { get; set; }
        public string? SorumluKullaniciAdi { get; set; }

        public bool Aktif { get; set; } = true;

        /// <summary>JWT <c>sub</c>.</summary>
        public string? OlusturanKullaniciId { get; set; }

        /// <summary>
        /// UTC. Tekrarlayan işin <b>ilk dönemi</b> bu anın düştüğü dönemdir: ayın 28'inde
        /// eklenen "ayın 1'i" işi o ayın 1'ini kaçırmış sayılır (gecikti), daha eski aylar
        /// hiç üretilmez.
        /// </summary>
        public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;

        // ---- Prosedür (Prompt 8): nerede yapılır, kime gider, nasıl yapılır ----

        /// <summary>
        /// Dolu ise bu satır özel iş <b>değildir</b>: firmanın o yasal işi (kod + <see cref="Tekrar"/>)
        /// için tuttuğu prosedürdür. Tarih ve ad takvimden gelmeye devam eder; satır yalnız
        /// program, alıcı, adımlar ve ekleri taşır. Liste/kuyruk bu satırları özel iş saymaz.
        /// </summary>
        public string? YasalMukellefiyetKodu { get; set; }

        /// <summary>
        /// İşin yapıldığı sistem (<c>Sistemler.Id</c>); boş = belirtilmemiş. Prompt 8'deki kapalı
        /// <c>IsProgrami</c> enum'unun yerine geldi — DijitalPlanet, TURMOB gibi sistemler
        /// enum'da yoktu, ilk hafta "Diğer · serbest metin"e düşerdi. Firmanın mizan formatından
        /// ve kendi sistemlerinden bağımsız: iş başka yerde yapılıyorsa başka sistem seçilir.
        /// </summary>
        public int? SistemId { get; set; }

        /// <summary>"Bordro > Raporlar > Ücret Bordrosu".</summary>
        public string? MenuYolu { get; set; }

        /// <summary>Çok satırlı serbest metin; her dolu satır bir adım.</summary>
        public string? NasilYapilir { get; set; }

        /// <summary>Bu iş başka bir işin hazırlığıysa o iş (<see cref="FirmaIsi.Id"/>, aynı firma).</summary>
        public int? OnAdimiOlduguIsId { get; set; }

        public List<FirmaIsiAlicisi> Alicilar { get; set; } = new();
        public List<FirmaIsiEki> Ekler { get; set; } = new();

        /// <summary>
        /// İŞ BAZINDA tarif (Prompt 14, <see cref="IsTarifi"/>). Boş = tarife bağlı değil; iş kendi
        /// <see cref="NasilYapilir"/>/<see cref="MenuYolu"/> metniyle çalışır. Bu iki alan SİLİNMEDİ:
        /// tarif varken firmaya özel istisna olarak "Bu firmada farklı" başlığıyla gösterilir.
        /// </summary>
        public int? IsTarifiId { get; set; }
    }

    /// <summary>
    /// İşin tarifi — FİRMA BAZINDA DEĞİL İŞ BAZINDA (Prompt 14). "Bordro" tarifi dört firmada dört
    /// kez yazılınca biri güncellenir, diğerleri eskide kalıyordu. Global tablo: firmaya değil işe
    /// aittir; firmalar <see cref="FirmaIsi.IsTarifiId"/> ile bağlanır. Yasal işlerin tarifi de ortaktır.
    /// </summary>
    public class IsTarifi
    {
        public int Id { get; set; }

        public string Ad { get; set; } = string.Empty;

        /// <summary>İşin yapıldığı sistem (<c>Sistemler.Id</c>); boş = belirtilmemiş.</summary>
        public int? SistemId { get; set; }

        public string? MenuYolu { get; set; }

        /// <summary>Çok satırlı; her dolu satır bir adım.</summary>
        public string? NasilYapilir { get; set; }

        /// <summary>UTC.</summary>
        public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;

        public List<IsTarifiEki> Ekler { get; set; } = new();
    }

    /// <summary>
    /// Tarifin eki: PROSEDÜR türünden, kalıcıdır, her ay aynıdır. <see cref="FirmaIsiEki"/> ve
    /// <see cref="IsTamamlamaEki"/> ile aynı depo (FileApiService) ve aynı sınırlar; AYRI kayıt.
    /// (FirmaIsiEki'lerin tarife taşınması bu promptta yapılmadı.)
    /// </summary>
    public class IsTarifiEki
    {
        public int Id { get; set; }
        public int IsTarifiId { get; set; }

        public int FileId { get; set; }
        public string DosyaAdi { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public long Boyut { get; set; }

        /// <summary>UTC.</summary>
        public DateTime YuklemeZamani { get; set; } = DateTime.UtcNow;

        public string? YukleyenKullaniciId { get; set; }
        public string? YukleyenKullaniciAdi { get; set; }
    }

    public enum AliciTipi : byte
    {
        Kime = 1,
        Bilgi = 2
    }

    /// <summary>
    /// İşin çıktısının gittiği kişi. E-posta zorunlu değil: kişi var, mail gitmiyor olabilir.
    ///
    /// Prompt 14: kişinin kendisi <see cref="Kisi"/>dedir (<see cref="KisiId"/>). Aynı kişi birçok işe
    /// alıcıdır ama e-postası tek yerde durur; adres değişince bağlı işler birden düzelir.
    /// <see cref="AdSoyad"/>/<see cref="Eposta"/> kişinin OKUMA KOPYASIDIR: her yazmada ve kişi
    /// düzenlenince kişiden eşitlenir (anasayfa kartı ve dialog bu alanları okumaya devam eder).
    /// Kime/Bilgi ayrımı (<see cref="AliciTipi"/>) burada, işe göre.
    /// </summary>
    public class FirmaIsiAlicisi
    {
        public int Id { get; set; }
        public int FirmaIsiId { get; set; }

        /// <summary>Kişi (aynı firmada). Taşıma öncesi kayıtlarda boş; seed doldurur, yazma her zaman doldurur.</summary>
        public int? KisiId { get; set; }
        public Kisi? Kisi { get; set; }

        public string AdSoyad { get; set; } = string.Empty;
        public string? Eposta { get; set; }

        /// <summary>Serbest metin: "Finans, Hollanda".</summary>
        public string? Rol { get; set; }

        public AliciTipi AliciTipi { get; set; } = AliciTipi.Kime;
        public int Sira { get; set; }
    }

    /// <summary>
    /// Firmanın bir kişisi (Prompt 14): kim, hangi e-posta. Takip panosunun "Kişiler ve mailler"
    /// sekmesinde düzenlenir. Aynı firmada aynı e-posta TEK kişidir.
    ///
    /// <b>Şifre, kullanıcı adı, erişim bilgisi TUTULMAZ</b> — bu tablo kim / nereye / ne zaman tutar.
    /// </summary>
    public class Kisi
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }

        public string Ad { get; set; } = string.Empty;
        public string? Eposta { get; set; }

        /// <summary>Serbest metin: "Finans, Hollanda".</summary>
        public string? Rol { get; set; }

        public string? Notu { get; set; }

        public bool Aktif { get; set; } = true;
    }

    /// <summary>
    /// İşe ait dosya. Firma belgelerinden (Belgeler kartı) ayrı tablo, AYNI depo: dosya
    /// FileApiService'te, burada <see cref="FileId"/> + metadata. Ek firmaya değil İŞE
    /// bağlıdır — "hangi belge hangi işe aitti" sorusu buradan cevaplanır.
    /// </summary>
    public class FirmaIsiEki
    {
        public int Id { get; set; }
        public int FirmaIsiId { get; set; }

        /// <summary>FileApiService kaydının Id'si (Belgeler kartındaki <c>FirmaBelgesi.FileId</c> ile aynı).</summary>
        public int FileId { get; set; }

        public string DosyaAdi { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public long Boyut { get; set; }

        /// <summary>UTC.</summary>
        public DateTime YuklemeZamani { get; set; } = DateTime.UtcNow;

        public string? YukleyenKullaniciId { get; set; }
        public string? YukleyenKullaniciAdi { get; set; }
    }

    /// <summary>
    /// Yasal beyanname takvimi: hangi mükellefiyet kodu, hangi dönem, son gün.
    ///
    /// <b>Tarihler koda gömülmez.</b> Yasal süreler yıl içinde tebliğle değişiyor ve
    /// uzatılıyor; gömülü bir tarih bir yıl sonra sessizce yanlış olur. Satırlar bu tabloda
    /// durur, Yönetim → Vergi Takvimi ekranından düzenlenir. Tablo global: takvim ülke
    /// çapında aynı, firmadan ve tenant'tan bağımsız.
    ///
    /// Yasal iş kaydı <b>saklanmaz</b>; firmanın mükellefiyet kodları × bu tablonun aktif
    /// satırları her açılışta hesaplanır. Yalnız tamamlama (<see cref="IsTamamlama"/>)
    /// saklanır — firma bir mükellefiyetten çıkınca liste kendiliğinden düzelir.
    /// </summary>
    public class VergiTakvimi
    {
        public int Id { get; set; }

        /// <summary>Mükellefiyet kodu, ör. "0015". Firmanın kodlarıyla bu alan eşleşir.</summary>
        public string MukellefiyetKodu { get; set; } = string.Empty;

        public string Ad { get; set; } = string.Empty;

        public IsTekrari Tekrar { get; set; }

        /// <summary>Beyanın ait olduğu dönemin yılı (son günün yılı değil).</summary>
        public int Yil { get; set; }

        /// <summary>Aylıkta 1–12, üç aylıkta 1–4, yıllıkta 1.</summary>
        public int DonemNo { get; set; }

        public DateTime DonemBas { get; set; }
        public DateTime DonemBit { get; set; }
        public DateTime SonGun { get; set; }

        /// <summary>
        /// Pasif satır iş üretmez. Seed, son günü seed anında geçmiş olan satırları pasif
        /// yazar: geçmişin tamamlama kaydı olmadığı için hepsi "gecikti" görünürdü.
        /// </summary>
        public bool Aktif { get; set; } = true;

        /// <summary>
        /// Kullanıcı Vergi Takvimi ekranından eklemiş ya da adını/son gününü değiştirmiş (Prompt 7B). Seed bu
        /// satıra ASLA dokunmaz; seed'in ürettiği (işaretsiz) satırlar nominal kurala göre yeniden üretilebilir.
        /// </summary>
        public bool ElleDuzenlendi { get; set; }
    }

    /// <summary>
    /// Bir işin bir dönemdeki kaydı. Hem yasal hem özel iş için tek tablo; "bu dönem yapıldı
    /// mı" ile "geçen yıl hangi ay yapmışım" aynı tablodan çıkar.
    ///
    /// <b>Yapıldı = <see cref="TamamlanmaZamani"/> dolu</b> (Prompt 10B). Zamanı boş kayıt: iş
    /// yapılmadı ama o döneme not ya da kanıt yazılmış ("müşteri veriyi geç verdi"). Kuyruk,
    /// geçmiş ve sayılar yalnız zamanı dolu kaydı "yapıldı" sayar. İşaret kaldırılınca not/kanıt
    /// yoksa satır silinir, varsa yalnız zaman boşalır.
    ///
    /// <see cref="KaynakId"/> iki tabloya bakabildiği için FK yok; özel iş silinince
    /// tamamlamaları servis siler.
    /// </summary>
    public class IsTamamlama
    {
        public long Id { get; set; }

        public IsKaynagi KaynakTip { get; set; }

        /// <summary><see cref="VergiTakvimi.Id"/> ya da <see cref="FirmaIsi.Id"/>.</summary>
        public int KaynakId { get; set; }

        public int FirmaId { get; set; }

        /// <summary>"2026-09", "2026-Q3", "2026"; tek seferlik işte "tek".</summary>
        public string DonemAnahtari { get; set; } = string.Empty;

        /// <summary>UTC. Boş = yapılmadı (kayıt yalnız not/kanıt taşıyor).</summary>
        public DateTime? TamamlanmaZamani { get; set; }

        /// <summary>İşaretleyen; zaman boşalınca boşalır.</summary>
        public string? KullaniciId { get; set; }
        public string? KullaniciAdi { get; set; }

        public bool Yapildi => TamamlanmaZamani is not null;

        /// <summary>
        /// O döneme özel serbest not (Dönem panosu): "Luca'dan gelir tablosunu alıp şablonun 2.
        /// sayfasına yapıştırdım" ya da yapılmamışsa sebebi. Ertesi dönem "Geçen dönem" bloğunda
        /// gösterilir. İşaret kaldırılınca KORUNUR; yalnız "Bu dönem kaydını sil" siler.
        /// </summary>
        public string? Not { get; set; }

        public List<IsTamamlamaEki> Ekler { get; set; } = new();
    }

    /// <summary>
    /// DÖNEM eki: "bu ay yaptım, çıktı bu". O dönemin tamamlamasına bağlıdır.
    ///
    /// <see cref="FirmaIsiEki"/> (PROSEDÜR eki: işe ait, kalıcı, her dönem aynı) ile
    /// BİRLEŞTİRİLMEDİ — iki ayrı kayıt, aynı depo (FileApiService) ve aynı sınırlar.
    /// İşaret geri alınınca ek de silinir; dosyayı FileApi'den istemci siler.
    /// </summary>
    public class IsTamamlamaEki
    {
        public int Id { get; set; }
        public long IsTamamlamaId { get; set; }

        /// <summary>FileApiService kaydının Id'si (<see cref="FirmaIsiEki.FileId"/> ile aynı anlam).</summary>
        public int FileId { get; set; }

        public string DosyaAdi { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public long Boyut { get; set; }

        /// <summary>UTC.</summary>
        public DateTime YuklemeZamani { get; set; } = DateTime.UtcNow;

        public string? YukleyenKullaniciId { get; set; }
        public string? YukleyenKullaniciAdi { get; set; }
    }
}
