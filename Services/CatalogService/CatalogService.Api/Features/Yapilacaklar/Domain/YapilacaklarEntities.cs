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
    }

    /// <summary>
    /// Bir işin bir dönemde yapıldığı. Hem yasal hem özel iş için tek tablo; işaret
    /// kaldırılınca satır <b>silinir</b>. Böylece "bu dönem yapıldı mı" ile "geçen yıl
    /// hangi ay yapmışım" aynı tablodan çıkar.
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

        /// <summary>UTC.</summary>
        public DateTime TamamlanmaZamani { get; set; } = DateTime.UtcNow;

        public string? KullaniciId { get; set; }
        public string? KullaniciAdi { get; set; }
    }
}
