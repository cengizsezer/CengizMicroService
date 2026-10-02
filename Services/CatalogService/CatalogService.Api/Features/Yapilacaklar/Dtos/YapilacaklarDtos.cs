using CatalogService.Api.Features.Yapilacaklar.Domain;

namespace CatalogService.Api.Features.Yapilacaklar.Dtos
{
    /// <summary>
    /// Bir işin durumu. Aciliyet sırasıdır; Yapılacaklar ekranının grupları bu değerden
    /// kurulur. Renk tek başına anlam taşımaz — rozet metni de <see cref="IsSatiriDto.Gun"/>
    /// ile birlikte yazılır.
    /// </summary>
    public enum IsDurumu : byte
    {
        Gecikti = 1,
        BuHafta = 2,
        BuAy = 3,
        Sonra = 4,
        Tamam = 5
    }

    /// <summary>Yapılacaklar filtresi.</summary>
    public enum IsFiltresi : byte
    {
        Tumu = 0,
        Bende = 1,
        Yasal = 2,
        Ozel = 3
    }

    /// <summary>Bir firmanın bir işinin bir dönemi — kartta ve Yapılacaklar'da aynı satır.</summary>
    public class IsSatiriDto
    {
        public IsKaynagi KaynakTip { get; set; }

        /// <summary>Yasalda <c>VergiTakvimi.Id</c>, özelde <c>FirmaIsi.Id</c>.</summary>
        public int KaynakId { get; set; }

        public int FirmaId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;

        public string Baslik { get; set; } = string.Empty;
        public string? Aciklama { get; set; }

        /// <summary>Yasalda mükellefiyet kodu; özelde boş.</summary>
        public string? MukellefiyetKodu { get; set; }

        public IsTekrari Tekrar { get; set; }

        public string DonemAnahtari { get; set; } = string.Empty;

        /// <summary>"Eylül 2026", "2026 / 3. çeyrek", "2026"; tek seferlikte boş.</summary>
        public string? DonemEtiketi { get; set; }

        public DateTime SonGun { get; set; }

        public IsDurumu Durum { get; set; }

        /// <summary>Gecikti'de kaç gün geçtiği, Bu hafta'da kaç gün kaldığı; diğerlerinde 0.</summary>
        public int Gun { get; set; }

        public bool Tamamlandi { get; set; }
        public DateTime? TamamlanmaZamani { get; set; }
        public string? TamamlayanAdi { get; set; }

        /// <summary>Aynı işin (bu dönem hariç) en son yapıldığı an; hiç yapılmadıysa boş.</summary>
        public DateTime? SonYapilma { get; set; }

        /// <summary>İşin sorumlusu: özel işte kendi alanı, boşsa (ve yasalda) firmanın sorumlusu.</summary>
        public int? SorumluKullaniciId { get; set; }
        public string? SorumluKullaniciAdi { get; set; }
    }

    /// <summary>
    /// İş tanımı — kartın düzenleme formu. Özel işin kendisi ya da (<see cref="YasalMukellefiyetKodu"/>
    /// doluysa) bir yasal işin prosedür satırı.
    /// </summary>
    public class FirmaIsiDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public string Baslik { get; set; } = string.Empty;
        public string? Aciklama { get; set; }
        public IsTekrari Tekrar { get; set; }
        public IsGunKurali GunKurali { get; set; }
        public int? AyinGunu { get; set; }
        public DateTime? TekSeferTarih { get; set; }
        public int? SorumluKullaniciId { get; set; }
        public string? SorumluKullaniciAdi { get; set; }
        public bool Aktif { get; set; }

        /// <summary>"her ay · ayın 1'i" — kartta ve listede tekrar/gün kuralının okunur hali.</summary>
        public string KuralMetni { get; set; } = string.Empty;

        // ---- Prosedür ----

        /// <summary>Dolu ise yasal işin prosedür satırı; özel iş değil.</summary>
        public string? YasalMukellefiyetKodu { get; set; }

        /// <summary>İşin yapıldığı sistem (ortak liste); boş = belirtilmemiş.</summary>
        public int? SistemId { get; set; }
        public string? SistemAdi { get; set; }

        public string? MenuYolu { get; set; }
        public string? NasilYapilir { get; set; }
        public int? OnAdimiOlduguIsId { get; set; }

        /// <summary>Ön adımı olduğu işin adı (aynı firmanın tanımlarından).</summary>
        public string? OnAdimiBaslik { get; set; }

        public List<FirmaIsiAlicisiDto> Alicilar { get; set; } = new();
        public int EkSayisi { get; set; }
    }

    public class FirmaIsiAlicisiDto
    {
        public int Id { get; set; }
        public string AdSoyad { get; set; } = string.Empty;
        public string? Eposta { get; set; }
        public string? Rol { get; set; }
        public AliciTipi AliciTipi { get; set; } = AliciTipi.Kime;
        public int Sira { get; set; }
    }

    /// <summary>
    /// Prosedür alanları. Yasal işte tek başına gönderilir (ad ve tarih kilitli); özel işte
    /// <see cref="FirmaIsiKaydetDto"/> bunu genişletir.
    /// </summary>
    public class IsProsedurKaydetDto
    {
        /// <summary>Ortak listeden sistem; boş = belirtilmemiş. Pasif sistem yeni seçilemez, mevcut kalır.</summary>
        public int? SistemId { get; set; }

        public string? MenuYolu { get; set; }
        public string? NasilYapilir { get; set; }

        /// <summary>Hedef özel işse ya da prosedür satırı zaten varsa Id.</summary>
        public int? OnAdimiOlduguIsId { get; set; }

        /// <summary>
        /// Hedef yasal işse kodu + tekrarı (prosedür satırı henüz yoksa sunucu açar). Doluysa
        /// <see cref="OnAdimiOlduguIsId"/>'ye göre önceliklidir.
        /// </summary>
        public string? OnAdimiYasalKodu { get; set; }
        public IsTekrari? OnAdimiYasalTekrar { get; set; }

        /// <summary><c>null</c> = alıcılara dokunma; liste = bütün alıcılar (gönderilmeyen silinir).</summary>
        public List<FirmaIsiAlicisiDto>? Alicilar { get; set; }
    }

    public class FirmaIsiKaydetDto : IsProsedurKaydetDto
    {
        public string Baslik { get; set; } = string.Empty;
        public string? Aciklama { get; set; }
        public IsTekrari Tekrar { get; set; } = IsTekrari.Aylik;
        public IsGunKurali GunKurali { get; set; } = IsGunKurali.AyinGunu;
        public int? AyinGunu { get; set; }
        public DateTime? TekSeferTarih { get; set; }
        public int? SorumluKullaniciId { get; set; }
        public string? SorumluKullaniciAdi { get; set; }
        public bool Aktif { get; set; } = true;
    }

    public class FirmaIsiEkiDto
    {
        public int Id { get; set; }
        public int FileId { get; set; }
        public string DosyaAdi { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Boyut { get; set; }
        public DateTime YuklemeZamani { get; set; }
        public string? YukleyenKullaniciAdi { get; set; }
    }

    /// <summary>Ek kaydı. Dosya ÖNCE FileApiService'e yüklenir (Belgeler kartıyla aynı akış).</summary>
    public class FirmaIsiEkiOlusturDto
    {
        public int FileId { get; set; }
        public string DosyaAdi { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public long Boyut { get; set; }
    }

    /// <summary>İş dialogunun program seçeneği.</summary>
    public class IsSistemSecenegiDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public Sistemler.Domain.SistemTuru Tur { get; set; }
        public bool Aktif { get; set; }

        /// <summary>Firmanın "Kullanılan sistemler" kartında atanmış mı (listede önde).</summary>
        public bool Firmanin { get; set; }
    }

    /// <summary>Prosedür panelinin "Geçmiş" satırı.</summary>
    public class IsGecmisSatiriDto
    {
        public string DonemAnahtari { get; set; } = string.Empty;
        public string? DonemEtiketi { get; set; }
        public DateTime SonGun { get; set; }
        public bool Yapildi { get; set; }
        public DateTime? TamamlanmaZamani { get; set; }
        public string? TamamlayanAdi { get; set; }

        /// <summary>Son günü geçti ve yapılmadı — panelde kırmızı.</summary>
        public bool Yapilmadi { get; set; }
    }

    /// <summary>Prosedür paneli (tasarım I): nerede, kime, nasıl, ekler, geçmiş.</summary>
    public class IsProsedurDto
    {
        /// <summary>Prosedür satırı; yasal işte henüz açılmadıysa boş (bölümler boş gelir).</summary>
        public int? IsId { get; set; }

        public int FirmaId { get; set; }
        public IsKaynagi KaynakTip { get; set; }
        public string Baslik { get; set; } = string.Empty;
        public string? MukellefiyetKodu { get; set; }
        public IsTekrari Tekrar { get; set; }
        public string KuralMetni { get; set; } = string.Empty;

        public int? SistemId { get; set; }
        public string? SistemAdi { get; set; }
        public string? MenuYolu { get; set; }
        public string? NasilYapilir { get; set; }

        /// <summary><see cref="NasilYapilir"/>'ın dolu satırları, sırayla.</summary>
        public List<string> Adimlar { get; set; } = new();

        public int? OnAdimiOlduguIsId { get; set; }
        public string? OnAdimiBaslik { get; set; }

        public List<FirmaIsiAlicisiDto> Alicilar { get; set; } = new();
        public List<FirmaIsiEkiDto> Ekler { get; set; } = new();

        public string? MizanFormati { get; set; }

        /// <summary>
        /// İşin sistemi bir muhasebe programıysa ve firmanın muhasebe programı başkaysa
        /// firmanınki ("Firmanın muhasebe programı Luca" notu); değilse boş.
        /// </summary>
        public string? FarkliMuhasebeProgrami { get; set; }

        public List<IsGecmisSatiriDto> Gecmis { get; set; } = new();

        /// <summary>Geçmişin kısaltılmadan önceki satır sayısı ("Tüm geçmiş" bağlantısı için).</summary>
        public int GecmisToplam { get; set; }

        // ---- İş tarifi (Prompt 14): panel ÖNCE tarifi okur; firmanın kendi metni yukarıdaki alanlarda ----

        /// <summary>Takip panosunda bu işin tarifi (<c>?sekme=nasil&amp;is=</c>) — "Tarifi düzenle" oraya gider.</summary>
        public string TarifAnahtari { get; set; } = string.Empty;

        /// <summary>Okunan tarif; boş = tarif yok, panel eskisi gibi firmanın metnini gösterir.</summary>
        public int? TarifId { get; set; }
        public string? TarifSistemAdi { get; set; }
        public string? TarifMenuYolu { get; set; }
        public List<string> TarifAdimlar { get; set; } = new();

        /// <summary>Tarif varken firmanın kendi metni tariften farklı → "Bu firmada farklı" (silinmedi).</summary>
        public bool FirmayaOzel { get; set; }
    }

    /// <summary>Anasayfa "Firma işleri" kartı.</summary>
    public class FirmaIsleriKartDto
    {
        public int FirmaId { get; set; }

        /// <summary>Mükellefiyetten türetildi; kilitli, yalnız işaretlenir.</summary>
        public List<IsSatiriDto> Yasal { get; set; } = new();

        /// <summary>Özel işlerin görünen dönemleri.</summary>
        public List<IsSatiriDto> Ozel { get; set; } = new();

        /// <summary>Özel iş tanımları (pasifler dahil) — düzenleme ve silme bunlardan.</summary>
        public List<FirmaIsiDto> OzelTanimlar { get; set; } = new();

        /// <summary>Yasal işlerin açılmış prosedür satırları (kod + tekrar ile satıra bağlanır).</summary>
        public List<FirmaIsiDto> YasalProsedurler { get; set; } = new();

        /// <summary>
        /// Yeni işte sistem önerisi: firmanın muhasebe programı, yoksa mizan formatının
        /// programı. Kilit değil — iş başka yerde yapılıyorsa değiştirilir.
        /// </summary>
        public int? VarsayilanSistemId { get; set; }

        /// <summary>Sistem seçenekleri: aktifler + bu firmanın işlerinde kalan pasifler; firmanınkiler önde.</summary>
        public List<IsSistemSecenegiDto> Sistemler { get; set; } = new();

        public int GeciktiSayisi { get; set; }
        public int BuAySayisi { get; set; }

        /// <summary>Firmanın ayıklanan mükellefiyet kodları; takvimde karşılığı olmayanlar ayrıca.</summary>
        public List<string> MukellefiyetKodlari { get; set; } = new();
        public List<string> TakvimdeOlmayanKodlar { get; set; } = new();
    }

    /// <summary>Yapılacaklar'da tek satır: aynı iş (aynı ad, aynı son gün) birden çok firmada.</summary>
    public class IsGrubuDto
    {
        public string Anahtar { get; set; } = string.Empty;
        public IsKaynagi KaynakTip { get; set; }
        public string Baslik { get; set; } = string.Empty;
        public IsTekrari Tekrar { get; set; }
        public string? DonemEtiketi { get; set; }
        public DateTime SonGun { get; set; }

        /// <summary>Açık firma varsa son güne göre; hepsi yapıldıysa <see cref="IsDurumu.Tamam"/>.</summary>
        public IsDurumu Durum { get; set; }
        public int Gun { get; set; }

        public int FirmaSayisi => Firmalar.Count;
        public int TamamlananSayisi => Firmalar.Count(f => f.Tamamlandi);

        public List<IsSatiriDto> Firmalar { get; set; } = new();
    }

    public class YapilacaklarDto
    {
        public DateTime Bugun { get; set; }
        public IsFiltresi Filtre { get; set; }
        public List<IsGrubuDto> Gruplar { get; set; } = new();
    }

    /// <summary>Anasayfa şeridi: bütün firmalar, filtresiz. Gecikmiş yoksa şerit çizilmez.</summary>
    public class YapilacaklarOzetDto
    {
        public int Gecikti { get; set; }
        public int BuHafta { get; set; }
    }

    /// <summary>İşaretlenecek / işareti kaldırılacak tek firma-dönem.</summary>
    public class IsIsaretDto
    {
        public IsKaynagi KaynakTip { get; set; }
        public int KaynakId { get; set; }
        public int FirmaId { get; set; }
        public string DonemAnahtari { get; set; } = string.Empty;
    }

    public class IsIsaretleDto
    {
        public bool Yapildi { get; set; }
        public List<IsIsaretDto> Isler { get; set; } = new();
    }

    // ---- Vergi takvimi (Yönetim) ----

    public class VergiTakvimiDto
    {
        public int Id { get; set; }
        public string MukellefiyetKodu { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public IsTekrari Tekrar { get; set; }
        public int Yil { get; set; }
        public int DonemNo { get; set; }
        public DateTime DonemBas { get; set; }
        public DateTime DonemBit { get; set; }
        public DateTime SonGun { get; set; }
        public bool Aktif { get; set; }

        /// <summary>Elle eklendi ya da düzenlendi: seed dokunmaz (Prompt 7B).</summary>
        public bool ElleDuzenlendi { get; set; }

        /// <summary>Bu satıra düşülmüş tamamlama sayısı; sıfır değilse silinemez.</summary>
        public int TamamlamaSayisi { get; set; }
    }

    public class VergiTakvimiKaydetDto
    {
        public string MukellefiyetKodu { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public IsTekrari Tekrar { get; set; }
        public int Yil { get; set; }
        public int DonemNo { get; set; }
        public DateTime SonGun { get; set; }
        public bool Aktif { get; set; } = true;
    }
}
