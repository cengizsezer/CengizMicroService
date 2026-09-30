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

    /// <summary>Firmaya özel iş tanımı — kartın düzenleme formu.</summary>
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
    }

    public class FirmaIsiKaydetDto
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
